using System;
using System.Configuration;
using FwoTelemetry.Abstractions;

namespace FwoTelemetry.OpenTelemetry
{
    public static class TelemetryOptionsLoader
    {
        public static TelemetryOptions LoadFromAppSettings(string prefix = "FwoTelemetry:")
        {
            var options = new TelemetryOptions
            {
                ServiceName = GetSetting(prefix + "ServiceName"),
                ServiceVersion = GetSetting(prefix + "ServiceVersion"),
                EnvironmentName = GetSetting(prefix + "EnvironmentName"),
                TracerName = GetSetting(prefix + "TracerName"),
                MeterName = GetSetting(prefix + "MeterName"),
                OtlpEndpoint = GetSetting(prefix + "OtlpEndpoint"),
                OtlpHeaders = GetSetting(prefix + "OtlpHeaders"),
                EnableConsoleExporter = GetBoolean(prefix + "EnableConsoleExporter", false),
                EnableHttpClientInstrumentation = GetBoolean(prefix + "EnableHttpClientInstrumentation", true),
                EnableAspNetInstrumentation = GetBoolean(prefix + "EnableAspNetInstrumentation", true),
                StrictMetricSchema = GetBoolean(prefix + "StrictMetricSchema", false),
                SamplingRatio = GetDouble(prefix + "SamplingRatio", 1.0),
                ExportTimeoutMilliseconds = GetInt32(prefix + "ExportTimeoutMilliseconds", 10000),
            };

            var protocol = GetSetting(prefix + "OtlpProtocol");
            TelemetryExportProtocol parsedProtocol;

            if (TryParseProtocol(protocol, out parsedProtocol))
            {
                options.OtlpProtocol = parsedProtocol;
            }

            return options;
        }

        private static bool TryParseProtocol(string value, out TelemetryExportProtocol protocol)
        {
            protocol = TelemetryExportProtocol.HttpProtobuf;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (string.Equals(value, "grpc", StringComparison.OrdinalIgnoreCase))
            {
                protocol = TelemetryExportProtocol.Grpc;
                return true;
            }

            if (string.Equals(value, "httpprotobuf", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "http/protobuf", StringComparison.OrdinalIgnoreCase))
            {
                protocol = TelemetryExportProtocol.HttpProtobuf;
                return true;
            }

            return false;
        }

        private static string GetSetting(string key)
        {
            var environmentValue = Environment.GetEnvironmentVariable(ToEnvironmentKey(key));

            if (!string.IsNullOrWhiteSpace(environmentValue))
            {
                return environmentValue;
            }

            return ConfigurationManager.AppSettings[key];
        }

        private static bool GetBoolean(string key, bool defaultValue)
        {
            var value = GetSetting(key);
            bool parsed;
            return bool.TryParse(value, out parsed) ? parsed : defaultValue;
        }

        private static int GetInt32(string key, int defaultValue)
        {
            var value = GetSetting(key);
            int parsed;
            return int.TryParse(value, out parsed) ? parsed : defaultValue;
        }

        private static double GetDouble(string key, double defaultValue)
        {
            var value = GetSetting(key);
            double parsed;
            return double.TryParse(value, out parsed) ? parsed : defaultValue;
        }

        private static string ToEnvironmentKey(string key)
        {
            return key.Replace(':', '_').Replace('.', '_').ToUpperInvariant();
        }
    }
}
