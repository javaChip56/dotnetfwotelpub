using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using FwoTelemetry.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace FwoTelemetry.OpenTelemetry
{
    public sealed class OpenTelemetryAdapter : ITelemetryAdapter
    {
        private readonly ActivitySource activitySource;
        private readonly Meter meter;
        private readonly TracerProvider tracerProvider;
        private readonly MeterProvider meterProvider;
        private readonly ConcurrentDictionary<string, Counter<long>> counters;
        private readonly ConcurrentDictionary<string, Histogram<double>> histograms;
        private readonly OpenTelemetryPropagator propagator;
        private readonly OpenTelemetryLogHook logging;
        private readonly TelemetrySanitizer sanitizer;
        private readonly TelemetryMetricCatalog metricCatalog;

        public OpenTelemetryAdapter(TelemetryOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            if (string.IsNullOrWhiteSpace(options.ServiceName))
            {
                throw new ArgumentException("ServiceName is required.", "options");
            }

            options = NormalizeOptions(options);

            var tracerName = string.IsNullOrWhiteSpace(options.TracerName)
                ? options.ServiceName
                : options.TracerName;

            var meterName = string.IsNullOrWhiteSpace(options.MeterName)
                ? tracerName
                : options.MeterName;

            var resourceBuilder = BuildResourceBuilder(options);

            this.activitySource = new ActivitySource(tracerName);
            this.meter = new Meter(meterName);
            this.counters = new ConcurrentDictionary<string, Counter<long>>();
            this.histograms = new ConcurrentDictionary<string, Histogram<double>>();
            this.sanitizer = new TelemetrySanitizer(options.Redaction);
            this.metricCatalog = new TelemetryMetricCatalog(options);
            this.propagator = new OpenTelemetryPropagator(this.sanitizer);
            this.logging = new OpenTelemetryLogHook(this.sanitizer);

            var tracerProviderBuilder = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(resourceBuilder)
                .SetSampler(new TraceIdRatioBasedSampler(options.SamplingRatio))
                .AddSource(tracerName);

            var meterProviderBuilder = Sdk.CreateMeterProviderBuilder()
                .SetResourceBuilder(resourceBuilder)
                .AddMeter(meterName);

            if (options.EnableAspNetInstrumentation)
            {
                tracerProviderBuilder.AddAspNetInstrumentation();
            }

            if (options.EnableHttpClientInstrumentation)
            {
                tracerProviderBuilder.AddHttpClientInstrumentation();
            }

            if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint))
            {
                tracerProviderBuilder.AddOtlpExporter(exporterOptions =>
                {
                    ConfigureTraceExporter(exporterOptions, options);
                });

                meterProviderBuilder.AddOtlpExporter(exporterOptions =>
                {
                    ConfigureMetricExporter(exporterOptions, options);
                });
            }

            if (options.EnableConsoleExporter)
            {
                tracerProviderBuilder.AddConsoleExporter();
                meterProviderBuilder.AddConsoleExporter();
            }

            this.tracerProvider = tracerProviderBuilder.Build();
            this.meterProvider = meterProviderBuilder.Build();
        }

        public ITelemetryPropagator Propagator
        {
            get { return this.propagator; }
        }

        public ITelemetryLogHook Logging
        {
            get { return this.logging; }
        }

        public bool ForceFlush(int timeoutMilliseconds)
        {
            var traceFlushed = this.tracerProvider.ForceFlush(timeoutMilliseconds);
            var metricFlushed = this.meterProvider.ForceFlush(timeoutMilliseconds);
            return traceFlushed && metricFlushed;
        }

        public ITelemetrySpan StartSpan(
            string name,
            TelemetrySpanKind kind = TelemetrySpanKind.Internal,
            IDictionary<string, object> attributes = null,
            TelemetryPropagationContext parentContext = null)
        {
            Activity activity;

            if (parentContext != null)
            {
                var activityContext = ResolveParentContext(parentContext);

                if (HasParentContext(activityContext))
                {
                    activity = this.activitySource.StartActivity(name, MapKind(kind), activityContext);
                }
                else
                {
                    activity = this.activitySource.StartActivity(name, MapKind(kind));
                }
            }
            else
            {
                activity = this.activitySource.StartActivity(name, MapKind(kind));
            }

            if (activity != null && attributes != null)
            {
                foreach (var entry in this.sanitizer.SanitizeSpanAttributes(attributes))
                {
                    activity.SetTag(entry.Key, entry.Value);
                }
            }

            return new OpenTelemetrySpan(activity, this.sanitizer);
        }

        public void IncrementCounter(
            string name,
            long value = 1,
            IDictionary<string, object> attributes = null,
            string unit = null,
            string description = null)
        {
            var definition = this.metricCatalog.Resolve(name, TelemetryMetricType.Counter, unit, description);
            var counter = this.counters.GetOrAdd(
                name,
                instrumentName => this.meter.CreateCounter<long>(instrumentName, definition.Unit, definition.Description));

            var sanitized = this.sanitizer.SanitizeMetricTags(attributes, definition.AllowedTagKeys);
            counter.Add(value, this.sanitizer.ToTagArray(sanitized));
        }

        public void RecordHistogram(
            string name,
            double value,
            IDictionary<string, object> attributes = null,
            string unit = null,
            string description = null)
        {
            var definition = this.metricCatalog.Resolve(name, TelemetryMetricType.Histogram, unit, description);
            var histogram = this.histograms.GetOrAdd(
                name,
                instrumentName => this.meter.CreateHistogram<double>(instrumentName, definition.Unit, definition.Description));

            var sanitized = this.sanitizer.SanitizeMetricTags(attributes, definition.AllowedTagKeys);
            histogram.Record(value, this.sanitizer.ToTagArray(sanitized));
        }

        public void Dispose()
        {
            this.ForceFlush(5000);
            this.logging.Dispose();
            this.tracerProvider.Dispose();
            this.meterProvider.Dispose();
            this.activitySource.Dispose();
            this.meter.Dispose();
        }

        private static ResourceBuilder BuildResourceBuilder(TelemetryOptions options)
        {
            var builder = ResourceBuilder.CreateDefault().AddService(
                serviceName: options.ServiceName,
                serviceVersion: options.ServiceVersion);

            if (!string.IsNullOrWhiteSpace(options.EnvironmentName))
            {
                builder.AddAttributes(new[]
                {
                    new KeyValuePair<string, object>("deployment.environment", options.EnvironmentName),
                });
            }

            if (options.ResourceAttributes != null)
            {
                foreach (var entry in options.ResourceAttributes)
                {
                    builder.AddAttributes(new[]
                    {
                        new KeyValuePair<string, object>(entry.Key, entry.Value),
                    });
                }
            }

            return builder;
        }

        private static void ConfigureTraceExporter(
            global::OpenTelemetry.Exporter.OtlpExporterOptions exporterOptions,
            TelemetryOptions options)
        {
            exporterOptions.Endpoint = BuildSignalEndpoint(options.OtlpEndpoint, "v1/traces");
            exporterOptions.Protocol = global::OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
            exporterOptions.TimeoutMilliseconds = options.ExportTimeoutMilliseconds;

            if (!string.IsNullOrWhiteSpace(options.OtlpHeaders))
            {
                exporterOptions.Headers = options.OtlpHeaders;
            }
        }

        private static void ConfigureMetricExporter(
            global::OpenTelemetry.Exporter.OtlpExporterOptions exporterOptions,
            TelemetryOptions options)
        {
            exporterOptions.Endpoint = BuildSignalEndpoint(options.OtlpEndpoint, "v1/metrics");
            exporterOptions.Protocol = global::OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
            exporterOptions.TimeoutMilliseconds = options.ExportTimeoutMilliseconds;

            if (!string.IsNullOrWhiteSpace(options.OtlpHeaders))
            {
                exporterOptions.Headers = options.OtlpHeaders;
            }
        }

        private static ActivityKind MapKind(TelemetrySpanKind kind)
        {
            switch (kind)
            {
                case TelemetrySpanKind.Server:
                    return ActivityKind.Server;
                case TelemetrySpanKind.Client:
                    return ActivityKind.Client;
                case TelemetrySpanKind.Producer:
                    return ActivityKind.Producer;
                case TelemetrySpanKind.Consumer:
                    return ActivityKind.Consumer;
                default:
                    return ActivityKind.Internal;
            }
        }

        private static ActivityContext ResolveParentContext(TelemetryPropagationContext parentContext)
        {
            ActivityContext nativeContext;

            if (parentContext.NativeContext is ActivityContext)
            {
                nativeContext = (ActivityContext)parentContext.NativeContext;
                return nativeContext;
            }

            if (parentContext.Headers != null && parentContext.Headers.Count > 0)
            {
                return OpenTelemetryPropagator.ExtractActivityContext(parentContext.Headers);
            }

            return default(ActivityContext);
        }

        private static bool HasParentContext(ActivityContext activityContext)
        {
            return activityContext.TraceId.ToString() != "00000000000000000000000000000000"
                && activityContext.SpanId.ToString() != "0000000000000000";
        }

        private static TelemetryOptions NormalizeOptions(TelemetryOptions options)
        {
            if (options.SamplingRatio < 0.0)
            {
                options.SamplingRatio = 0.0;
            }
            else if (options.SamplingRatio > 1.0)
            {
                options.SamplingRatio = 1.0;
            }

            if (options.ExportTimeoutMilliseconds <= 0)
            {
                options.ExportTimeoutMilliseconds = 10000;
            }

            return options;
        }

        private static Uri BuildSignalEndpoint(string endpoint, string signalPath)
        {
            var uri = new Uri(endpoint);

            if (uri.AbsolutePath.EndsWith(signalPath, StringComparison.OrdinalIgnoreCase))
            {
                return uri;
            }

            var builder = new UriBuilder(uri);
            var path = builder.Path ?? string.Empty;

            if (string.IsNullOrWhiteSpace(path) || path == "/")
            {
                builder.Path = signalPath;
            }
            else
            {
                builder.Path = path.TrimEnd('/') + "/" + signalPath;
            }

            return builder.Uri;
        }
    }
}
