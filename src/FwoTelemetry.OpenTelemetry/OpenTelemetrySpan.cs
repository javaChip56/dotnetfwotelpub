using System;
using System.Collections.Generic;
using System.Diagnostics;
using FwoTelemetry.Abstractions;
namespace FwoTelemetry.OpenTelemetry
{
    internal sealed class OpenTelemetrySpan : ITelemetrySpan
    {
        private readonly Activity activity;

        public OpenTelemetrySpan(Activity activity)
        {
            this.activity = activity;
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
            if (this.activity != null)
            {
                this.activity.SetTag(key, value);
            }
        }

        public void SetAttribute(string key, long value)
        {
            if (this.activity != null)
            {
                this.activity.SetTag(key, value);
            }
        }

        public void SetAttribute(string key, double value)
        {
            if (this.activity != null)
            {
                this.activity.SetTag(key, value);
            }
        }

        public void SetAttribute(string key, bool value)
        {
            if (this.activity != null)
            {
                this.activity.SetTag(key, value);
            }
        }

        public void AddEvent(string name, IDictionary<string, object> attributes = null)
        {
            if (this.activity == null)
            {
                return;
            }

            ActivityTagsCollection tags = null;

            if (attributes != null && attributes.Count > 0)
            {
                tags = new ActivityTagsCollection();

                foreach (var entry in attributes)
                {
                    tags.Add(entry.Key, entry.Value);
                }
            }

            this.activity.AddEvent(new ActivityEvent(name, DateTimeOffset.UtcNow, tags));
        }

        public void RecordException(Exception exception)
        {
            if (this.activity != null && exception != null)
            {
                this.activity.AddException(exception);
            }
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
    }
}
