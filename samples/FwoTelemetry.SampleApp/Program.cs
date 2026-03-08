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
            var options = new TelemetryOptions
            {
                ServiceName = "FwoTelemetry.SampleApp",
                ServiceVersion = "1.0.0",
                TracerName = "FwoTelemetry.SampleApp.Trace",
                MeterName = "FwoTelemetry.SampleApp.Metrics",
                OtlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT"),
                OtlpHeaders = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS"),
                EnableConsoleExporter = true,
            };

            options.ResourceAttributes["deployment.environment"] = "local";

            using (var telemetry = new OpenTelemetryAdapter(options))
            {
                telemetry.Logging.RegisterSink(new ConsoleLegacyLogSink());

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
