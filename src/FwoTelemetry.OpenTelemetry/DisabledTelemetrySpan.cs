using System;
using System.Collections.Generic;
using FwoTelemetry.Abstractions;

namespace FwoTelemetry.OpenTelemetry
{
    internal sealed class DisabledTelemetrySpan : ITelemetrySpan
    {
        public static readonly DisabledTelemetrySpan Instance = new DisabledTelemetrySpan();

        private DisabledTelemetrySpan()
        {
        }

        public string TraceId
        {
            get { return string.Empty; }
        }

        public string SpanId
        {
            get { return string.Empty; }
        }

        public void SetAttribute(string key, string value)
        {
        }

        public void SetAttribute(string key, long value)
        {
        }

        public void SetAttribute(string key, double value)
        {
        }

        public void SetAttribute(string key, bool value)
        {
        }

        public void AddEvent(string name, IDictionary<string, object> attributes = null)
        {
        }

        public void RecordException(Exception exception)
        {
        }

        public void SetStatus(TelemetryStatusCode statusCode, string description = null)
        {
        }

        public void Dispose()
        {
        }
    }
}
