using System;
using System.Collections.Generic;
using System.Threading;
using FwoTelemetry.Abstractions;
using FwoTelemetry.NoOp;
using FwoTelemetry.OpenTelemetry;
using Xunit;

namespace FwoTelemetry.Tests
{
    public sealed class OpenTelemetryAdapterTests
    {
        [Fact]
        public void NoOpAdapter_ForceFlush_ReturnsTrue()
        {
            Assert.True(NoOpTelemetryAdapter.Instance.ForceFlush(1000));
        }

        [Fact]
        public void StrictMetricSchema_RejectsUnknownMetric()
        {
            using (var adapter = new OpenTelemetryAdapter(CreateOptions(strictMetricSchema: true)))
            {
                Assert.Throws<InvalidOperationException>(() => adapter.IncrementCounter("unknown.metric"));
            }
        }

        [Fact]
        public void Propagator_PreservesTraceAcrossCarrier()
        {
            using (var adapter = new OpenTelemetryAdapter(CreateOptions()))
            {
                string producerTraceId;
                string producerSpanId;
                var carrier = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                using (var producerSpan = adapter.StartSpan("producer", TelemetrySpanKind.Producer))
                {
                    producerTraceId = producerSpan.TraceId;
                    producerSpanId = producerSpan.SpanId;
                    adapter.Propagator.Inject(carrier);
                }

                var parentContext = adapter.Propagator.Extract(carrier);

                using (var consumerSpan = adapter.StartSpan("consumer", TelemetrySpanKind.Consumer, parentContext: parentContext))
                {
                    Assert.Equal(producerTraceId, consumerSpan.TraceId);
                    Assert.NotEqual(producerSpanId, consumerSpan.SpanId);
                }
            }
        }

        [Fact]
        public void Logging_RedactsSensitiveProperties()
        {
            using (var adapter = new OpenTelemetryAdapter(CreateOptions()))
            {
                var sink = new CollectingSink();
                adapter.Logging.RegisterSink(sink);

                using (adapter.StartSpan("log-test"))
                {
                    adapter.Logging.Log(
                        TelemetryLogLevel.Information,
                        "hello",
                        properties: new Dictionary<string, object>
                        {
                            { "customer.email", "customer@example.com" },
                            { "operation", "log-test" },
                        });
                }

                Assert.True(sink.Signal.WaitOne(TimeSpan.FromSeconds(2)));
                Assert.Equal("[REDACTED]", sink.Entry.Properties["customer.email"]);
                Assert.True(sink.Entry.Properties.ContainsKey("trace.id"));
                Assert.True(sink.Entry.Properties.ContainsKey("span.id"));
            }
        }

        private static TelemetryOptions CreateOptions(bool strictMetricSchema = false)
        {
            var options = new TelemetryOptions
            {
                ServiceName = "FwoTelemetry.Tests",
                ServiceVersion = "1.0.0",
                TracerName = "FwoTelemetry.Tests.Trace",
                MeterName = "FwoTelemetry.Tests.Metrics",
                EnableConsoleExporter = false,
                EnableAspNetInstrumentation = false,
                EnableHttpClientInstrumentation = false,
                StrictMetricSchema = strictMetricSchema,
            };

            options.Redaction.Headers.AllowedKeys.Add("traceparent");
            options.Redaction.Headers.AllowedKeys.Add("tracestate");
            options.Redaction.Headers.AllowedKeys.Add("baggage");
            options.Redaction.Headers.DropUnknownKeys = true;
            options.Redaction.LogProperties.SensitiveKeys.Add("customer.email");
            options.MetricDefinitions.Add(new TelemetryMetricDefinition
            {
                Name = "known.counter",
                MetricType = TelemetryMetricType.Counter,
                Unit = "request",
                Description = "Known test counter",
            });
            options.MetricDefinitions[0].AllowedTagKeys.Add("operation");

            return options;
        }

        private sealed class CollectingSink : ITelemetryLogSink
        {
            public readonly ManualResetEvent Signal = new ManualResetEvent(false);

            public TelemetryLogEntry Entry { get; private set; }

            public void Write(TelemetryLogEntry entry)
            {
                this.Entry = entry;
                this.Signal.Set();
            }
        }
    }
}
