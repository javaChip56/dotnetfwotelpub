using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using FwoTelemetry.Abstractions;

namespace FwoTelemetry.OpenTelemetry
{
    public sealed class OpenTelemetryLogHook : ITelemetryLogHook, IDisposable
    {
        private readonly BlockingCollection<TelemetryLogEntry> queue;
        private readonly ConcurrentBag<ITelemetryLogSink> sinks;
        private readonly TelemetrySanitizer sanitizer;
        private readonly Thread worker;

        internal OpenTelemetryLogHook(TelemetrySanitizer sanitizer)
        {
            this.sanitizer = sanitizer;
            this.sinks = new ConcurrentBag<ITelemetryLogSink>();
            this.queue = new BlockingCollection<TelemetryLogEntry>(1024);
            this.worker = new Thread(this.ProcessQueue);
            this.worker.IsBackground = true;
            this.worker.Name = "FwoTelemetry.LogHook";
            this.worker.Start();
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

            var sanitized = this.sanitizer.SanitizeLogProperties(properties);
            var context = this.GetCurrentContext();

            if (!string.IsNullOrWhiteSpace(context.TraceId) && !sanitized.ContainsKey("trace.id"))
            {
                sanitized["trace.id"] = context.TraceId;
            }

            if (!string.IsNullOrWhiteSpace(context.SpanId) && !sanitized.ContainsKey("span.id"))
            {
                sanitized["span.id"] = context.SpanId;
            }

            properties.Clear();

            foreach (var item in sanitized)
            {
                properties[item.Key] = item.Value;
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
                foreach (var property in this.sanitizer.SanitizeLogProperties(properties))
                {
                    entry.Properties[property.Key] = property.Value;
                }
            }

            this.Enrich(entry.Properties);
            this.queue.TryAdd(entry);
        }

        public void Dispose()
        {
            this.queue.CompleteAdding();
            this.worker.Join(TimeSpan.FromSeconds(2));
        }

        private void ProcessQueue()
        {
            foreach (var entry in this.queue.GetConsumingEnumerable())
            {
                foreach (var sink in this.sinks)
                {
                    try
                    {
                        sink.Write(entry);
                    }
                    catch
                    {
                    }
                }
            }
        }
    }
}
