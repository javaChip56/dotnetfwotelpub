using System;
using System.Collections.Generic;

namespace FwoTelemetry.Abstractions
{
    public interface ITelemetrySpan : IDisposable
    {
        string TraceId { get; }

        string SpanId { get; }

        void SetAttribute(string key, string value);

        void SetAttribute(string key, long value);

        void SetAttribute(string key, double value);

        void SetAttribute(string key, bool value);

        void AddEvent(string name, IDictionary<string, object> attributes = null);

        void RecordException(Exception exception);

        void SetStatus(TelemetryStatusCode statusCode, string description = null);
    }
}
