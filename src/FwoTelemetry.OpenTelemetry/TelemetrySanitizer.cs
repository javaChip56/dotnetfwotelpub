using System;
using System.Collections.Generic;
using FwoTelemetry.Abstractions;

namespace FwoTelemetry.OpenTelemetry
{
    internal sealed class TelemetrySanitizer
    {
        private readonly TelemetryRedactionOptions options;

        public TelemetrySanitizer(TelemetryRedactionOptions options)
        {
            this.options = options ?? new TelemetryRedactionOptions();
        }

        public IDictionary<string, object> SanitizeSpanAttributes(IDictionary<string, object> attributes)
        {
            return this.SanitizeObjectMap(attributes, this.options.SpanAttributes, null);
        }

        public IDictionary<string, object> SanitizeMetricTags(IDictionary<string, object> attributes, ISet<string> allowedKeys)
        {
            return this.SanitizeObjectMap(attributes, this.options.MetricTags, allowedKeys);
        }

        public IDictionary<string, object> SanitizeLogProperties(IDictionary<string, object> properties)
        {
            return this.SanitizeObjectMap(properties, this.options.LogProperties, null);
        }

        public IDictionary<string, string> SanitizeHeaders(IDictionary<string, string> headers)
        {
            var sanitized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (headers == null)
            {
                return sanitized;
            }

            foreach (var entry in headers)
            {
                string key;
                object value;

                if (this.TrySanitizeEntry(entry.Key, entry.Value, this.options.Headers, null, out key, out value))
                {
                    sanitized[key] = value == null ? string.Empty : value.ToString();
                }
            }

            return sanitized;
        }

        public string SanitizeExceptionMessage(string message)
        {
            string key;
            object value;

            if (!this.TrySanitizeEntry("exception.message", message, this.options.LogProperties, null, out key, out value))
            {
                return this.options.RedactedValue;
            }

            return value == null ? string.Empty : value.ToString();
        }

        public KeyValuePair<string, object>[] ToTagArray(IDictionary<string, object> attributes)
        {
            if (attributes == null || attributes.Count == 0)
            {
                return Array.Empty<KeyValuePair<string, object>>();
            }

            var tags = new KeyValuePair<string, object>[attributes.Count];
            var index = 0;

            foreach (var entry in attributes)
            {
                tags[index++] = new KeyValuePair<string, object>(entry.Key, entry.Value);
            }

            return tags;
        }

        private IDictionary<string, object> SanitizeObjectMap(
            IDictionary<string, object> source,
            TelemetryTagPolicy policy,
            ISet<string> explicitAllowedKeys)
        {
            var sanitized = new Dictionary<string, object>();

            if (source == null)
            {
                return sanitized;
            }

            foreach (var entry in source)
            {
                string key;
                object value;

                if (this.TrySanitizeEntry(entry.Key, entry.Value, policy, explicitAllowedKeys, out key, out value))
                {
                    sanitized[key] = value;
                }
            }

            return sanitized;
        }

        private bool TrySanitizeEntry(
            string rawKey,
            object rawValue,
            TelemetryTagPolicy policy,
            ISet<string> explicitAllowedKeys,
            out string key,
            out object value)
        {
            key = rawKey;
            value = rawValue;

            if (string.IsNullOrWhiteSpace(rawKey) || rawValue == null)
            {
                return false;
            }

            if (explicitAllowedKeys != null && explicitAllowedKeys.Count > 0 && !explicitAllowedKeys.Contains(rawKey))
            {
                return false;
            }

            if (policy.DropUnknownKeys && policy.AllowedKeys.Count > 0 && !policy.AllowedKeys.Contains(rawKey))
            {
                return false;
            }

            if (policy.SensitiveKeys.Contains(rawKey))
            {
                value = this.options.RedactedValue;
                return true;
            }

            if (rawValue is string)
            {
                var text = (string)rawValue;

                if (policy.MaxValueLength > 0 && text.Length > policy.MaxValueLength)
                {
                    value = text.Substring(0, policy.MaxValueLength);
                }
            }

            return true;
        }
    }
}
