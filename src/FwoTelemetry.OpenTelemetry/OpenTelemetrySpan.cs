using System;
using System.Collections.Generic;
using System.Diagnostics;
using FwoTelemetry.Abstractions;

namespace FwoTelemetry.OpenTelemetry
{
    internal sealed class OpenTelemetrySpan : ITelemetrySpan
    {
        private readonly Activity activity;
        private readonly TelemetrySanitizer sanitizer;

        public OpenTelemetrySpan(Activity activity, TelemetrySanitizer sanitizer)
        {
            this.activity = activity;
            this.sanitizer = sanitizer;
        }

        public string TraceId
        {
            get { return this.activity == null ? string.Empty : this.activity.TraceId.ToString(); }
        }

        public string SpanId
        {
            get { return this.activity == null ? string.Empty : this.activity.SpanId.ToString(); }
        }

        public void SetAttribute(string key, string value)
        {
            this.SetAttributes(new Dictionary<string, object> { { key, value } });
        }

        public void SetAttribute(string key, long value)
        {
            this.SetAttributes(new Dictionary<string, object> { { key, value } });
        }

        public void SetAttribute(string key, double value)
        {
            this.SetAttributes(new Dictionary<string, object> { { key, value } });
        }

        public void SetAttribute(string key, bool value)
        {
            this.SetAttributes(new Dictionary<string, object> { { key, value } });
        }

        public void AddEvent(string name, IDictionary<string, object> attributes = null)
        {
            if (this.activity == null)
            {
                return;
            }

            ActivityTagsCollection tags = null;
            var sanitized = this.sanitizer.SanitizeSpanAttributes(attributes);

            if (sanitized.Count > 0)
            {
                tags = new ActivityTagsCollection();

                foreach (var entry in sanitized)
                {
                    tags.Add(entry.Key, entry.Value);
                }
            }

            this.activity.AddEvent(new ActivityEvent(name, DateTimeOffset.UtcNow, tags));
        }

        public void RecordException(Exception exception)
        {
            if (this.activity == null || exception == null)
            {
                return;
            }

            this.activity.AddEvent(
                new ActivityEvent(
                    "exception",
                    DateTimeOffset.UtcNow,
                    new ActivityTagsCollection
                    {
                        { "exception.type", exception.GetType().FullName },
                        { "exception.message", this.sanitizer.SanitizeExceptionMessage(exception.Message) },
                    }));
        }

        public void SetStatus(TelemetryStatusCode statusCode, string description = null)
        {
            if (this.activity == null)
            {
                return;
            }

            switch (statusCode)
            {
                case TelemetryStatusCode.Ok:
                    this.activity.SetStatus(ActivityStatusCode.Ok, description);
                    break;
                case TelemetryStatusCode.Error:
                    this.activity.SetStatus(ActivityStatusCode.Error, description);
                    break;
                default:
                    this.activity.SetStatus(ActivityStatusCode.Unset, description);
                    break;
            }
        }

        public void Dispose()
        {
            if (this.activity != null)
            {
                this.activity.Dispose();
            }
        }

        private void SetAttributes(IDictionary<string, object> attributes)
        {
            if (this.activity == null)
            {
                return;
            }

            foreach (var entry in this.sanitizer.SanitizeSpanAttributes(attributes))
            {
                this.activity.SetTag(entry.Key, entry.Value);
            }
        }
    }
}
