using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using FwoTelemetry.Abstractions;

namespace FwoTelemetry.OpenTelemetry
{
    public sealed class OpenTelemetryLogHook : ITelemetryLogHook
    {
        private readonly ConcurrentBag<ITelemetryLogSink> sinks;

        public OpenTelemetryLogHook()
        {
            this.sinks = new ConcurrentBag<ITelemetryLogSink>();
        }

        public void RegisterSink(ITelemetryLogSink sink)
        {
            if (sink == null)
            {
                throw new ArgumentNullException("sink");
            }

            this.sinks.Add(sink);
        }

        public TelemetryLogContext GetCurrentContext()
        {
            var activity = Activity.Current;

            if (activity == null)
            {
                return new TelemetryLogContext();
            }

            return new TelemetryLogContext
            {
                TraceId = activity.TraceId.ToString(),
                SpanId = activity.SpanId.ToString(),
            };
        }

        public void Enrich(IDictionary<string, object> properties)
        {
            if (properties == null)
            {
                return;
            }

            var context = this.GetCurrentContext();

            if (!string.IsNullOrWhiteSpace(context.TraceId) && !properties.ContainsKey("trace.id"))
            {
                properties["trace.id"] = context.TraceId;
            }

            if (!string.IsNullOrWhiteSpace(context.SpanId) && !properties.ContainsKey("span.id"))
            {
                properties["span.id"] = context.SpanId;
            }
        }

        public void Log(
            TelemetryLogLevel level,
            string message,
            Exception exception = null,
            IDictionary<string, object> properties = null)
        {
            var context = this.GetCurrentContext();
            var entry = new TelemetryLogEntry
            {
                Level = level,
                Message = message,
                Exception = exception,
                TraceId = context.TraceId,
                SpanId = context.SpanId,
            };

            if (properties != null)
            {
                foreach (var property in properties)
                {
                    entry.Properties[property.Key] = property.Value;
                }
            }

            this.Enrich(entry.Properties);

            foreach (var sink in this.sinks)
            {
                sink.Write(entry);
            }
        }
    }
}
