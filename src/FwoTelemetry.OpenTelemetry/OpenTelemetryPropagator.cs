using System;
using System.Collections.Generic;
using System.Diagnostics;
using FwoTelemetry.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace FwoTelemetry.OpenTelemetry
{
    public sealed class OpenTelemetryPropagator : ITelemetryPropagator
    {
        private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;
        private readonly TelemetrySanitizer sanitizer;

        internal OpenTelemetryPropagator(TelemetrySanitizer sanitizer)
        {
            this.sanitizer = sanitizer;
        }

        public void Inject(IDictionary<string, string> carrier)
        {
            if (carrier == null)
            {
                throw new ArgumentNullException("carrier");
            }

            var currentActivity = Activity.Current;

            if (currentActivity == null)
            {
                return;
            }

            var propagationContext = new PropagationContext(currentActivity.Context, Baggage.Current);
            Propagator.Inject(propagationContext, carrier, SetCarrierValue);
            ReplaceWithSanitizedHeaders(carrier, this.sanitizer.SanitizeHeaders(carrier));
        }

        public TelemetryPropagationContext Extract(IDictionary<string, string> carrier)
        {
            if (carrier == null)
            {
                throw new ArgumentNullException("carrier");
            }

            var sanitized = this.sanitizer.SanitizeHeaders(carrier);
            var activityContext = ExtractActivityContext(sanitized);
            var context = new TelemetryPropagationContext
            {
                TraceId = activityContext.TraceId.ToString(),
                SpanId = activityContext.SpanId.ToString(),
                TraceState = activityContext.TraceState,
                IsRemote = activityContext.IsRemote,
                NativeContext = activityContext,
            };

            foreach (var header in sanitized)
            {
                context.Headers[header.Key] = header.Value;
            }

            return context;
        }

        internal static ActivityContext ExtractActivityContext(IDictionary<string, string> carrier)
        {
            var propagationContext = Propagator.Extract(default(PropagationContext), carrier, GetCarrierValues);
            return propagationContext.ActivityContext;
        }

        private static IEnumerable<string> GetCarrierValues(IDictionary<string, string> carrier, string key)
        {
            string value;

            if (carrier != null && carrier.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value))
            {
                return new[] { value };
            }

            return Array.Empty<string>();
        }

        private static void SetCarrierValue(IDictionary<string, string> carrier, string key, string value)
        {
            carrier[key] = value;
        }

        private static void ReplaceWithSanitizedHeaders(
            IDictionary<string, string> carrier,
            IDictionary<string, string> sanitized)
        {
            var keys = new List<string>(carrier.Keys);

            foreach (var key in keys)
            {
                carrier.Remove(key);
            }

            foreach (var entry in sanitized)
            {
                carrier[entry.Key] = entry.Value;
            }
        }
    }
}
