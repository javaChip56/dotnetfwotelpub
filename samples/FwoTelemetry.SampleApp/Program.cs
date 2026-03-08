using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using FwoTelemetry.Abstractions;
using FwoTelemetry.OpenTelemetry;

namespace FwoTelemetry.SampleApp
{
    internal static class Program
    {
        private static void Main()
        {
            var options = TelemetryOptionsLoader.LoadFromAppSettings();
            options.OtlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
            options.OtlpHeaders = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS");
            options.ResourceAttributes["sample.owner"] = "FwoTelemetry";
            options.Redaction.Headers.AllowedKeys.Add("traceparent");
            options.Redaction.Headers.AllowedKeys.Add("tracestate");
            options.Redaction.Headers.AllowedKeys.Add("baggage");
            options.Redaction.Headers.DropUnknownKeys = true;
            options.Redaction.LogProperties.SensitiveKeys.Add("customer.email");
            options.Redaction.SpanAttributes.SensitiveKeys.Add("customer.email");
            options.MetricDefinitions.Add(new TelemetryMetricDefinition
            {
                Name = "sample.requests",
                MetricType = TelemetryMetricType.Counter,
                Unit = "request",
                Description = "Number of sample requests",
            });
            options.MetricDefinitions[0].AllowedTagKeys.Add("operation");
            options.MetricDefinitions.Add(new TelemetryMetricDefinition
            {
                Name = "sample.duration.ms",
                MetricType = TelemetryMetricType.Histogram,
                Unit = "ms",
                Description = "Execution time for the sample operation",
            });
            options.MetricDefinitions[1].AllowedTagKeys.Add("operation");

            var telemetry = TelemetryRuntime.Initialize(options);

            try
            {
                telemetry.Logging.RegisterSink(new ConsoleLegacyLogSink());
                telemetry.Logging.RegisterSink(new TraceSourceTelemetryLogSink(new TraceSource("FwoTelemetry.SampleApp")));

                using (var producerSpan = telemetry.StartSpan(
                    "sample.outbound",
                    TelemetrySpanKind.Producer,
                    new Dictionary<string, object>
                    {
                        { "app.feature", "demo" },
                        { "app.run_id", Guid.NewGuid().ToString("N") },
                    }))
                {
                    var outboundHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    telemetry.Logging.Log(
                        TelemetryLogLevel.Information,
                        "Publishing sample work item",
                        properties: new Dictionary<string, object>
                        {
                            { "message.kind", "demo" },
                        });

                    telemetry.Propagator.Inject(outboundHeaders);
                    producerSpan.AddEvent("sample.headers.injected");
                    ProcessIncomingMessage(telemetry, outboundHeaders);
                }
            }
            finally
            {
                TelemetryRuntime.ForceFlush(5000);
                TelemetryRuntime.Shutdown();
            }

            Console.WriteLine("Telemetry sample completed.");
        }

        private static void ProcessIncomingMessage(
            ITelemetryAdapter telemetry,
            IDictionary<string, string> inboundHeaders)
        {
            var parentContext = telemetry.Propagator.Extract(inboundHeaders);

            using (var span = telemetry.StartSpan(
                "sample.inbound",
                TelemetrySpanKind.Consumer,
                new Dictionary<string, object>
                {
                    { "messaging.system", "sample" },
                },
                parentContext))
            {
                var stopwatch = Stopwatch.StartNew();
                var legacyProperties = new Dictionary<string, object>
                {
                    { "operation", "sample.inbound" },
                    { "customer.email", "customer@example.com" },
                };

                telemetry.Logging.Enrich(legacyProperties);
                telemetry.Logging.Log(
                    TelemetryLogLevel.Information,
                    "Legacy pipeline received the work item",
                    properties: legacyProperties);

                telemetry.IncrementCounter(
                    "sample.requests",
                    1,
                    new Dictionary<string, object>
                    {
                        { "operation", "sample.inbound" },
                    },
                    unit: "request",
                    description: "Number of sample requests");

                try
                {
                    Thread.Sleep(150);

                    span.SetAttribute("work.duration_hint_ms", 150L);
                    span.SetStatus(TelemetryStatusCode.Ok);
                    span.AddEvent(
                        "sample.completed",
                        new Dictionary<string, object>
                        {
                            { "result", "success" },
                        });
                }
                catch (Exception ex)
                {
                    span.RecordException(ex);
                    span.SetStatus(TelemetryStatusCode.Error, ex.Message);
                    telemetry.Logging.Log(
                        TelemetryLogLevel.Error,
                        "Inbound processing failed",
                        ex,
                        legacyProperties);
                    throw;
                }
                finally
                {
                    stopwatch.Stop();
                    telemetry.RecordHistogram(
                        "sample.duration.ms",
                        stopwatch.Elapsed.TotalMilliseconds,
                        new Dictionary<string, object>
                        {
                            { "operation", "sample.inbound" },
                        },
                        unit: "ms",
                        description: "Execution time for the sample operation");
                }
            }
        }
    }
}
