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
        }

        public TelemetryPropagationContext Extract(IDictionary<string, string> carrier)
        {
            if (carrier == null)
            {
                throw new ArgumentNullException("carrier");
            }

            var activityContext = ExtractActivityContext(carrier);
            var context = new TelemetryPropagationContext
            {
                TraceId = activityContext.TraceId.ToString(),
                SpanId = activityContext.SpanId.ToString(),
                TraceState = activityContext.TraceState,
                IsRemote = activityContext.IsRemote,
                NativeContext = activityContext,
            };

            foreach (var header in carrier)
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
    }
}
