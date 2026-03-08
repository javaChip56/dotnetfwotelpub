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
        private readonly ITelemetryPropagator propagator;
        private readonly ITelemetryLogHook logging;

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
            this.propagator = new OpenTelemetryPropagator();
            this.logging = new OpenTelemetryLogHook();

            var tracerProviderBuilder = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(resourceBuilder)
                .AddSource(tracerName);

            var meterProviderBuilder = Sdk.CreateMeterProviderBuilder()
                .SetResourceBuilder(resourceBuilder)
                .AddMeter(meterName);

            if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint))
            {
                tracerProviderBuilder.AddOtlpExporter(exporterOptions =>
                {
                    ConfigureExporter(exporterOptions, options);
                });

                meterProviderBuilder.AddOtlpExporter(exporterOptions =>
                {
                    ConfigureExporter(exporterOptions, options);
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
                foreach (var entry in attributes)
                {
                    activity.SetTag(entry.Key, entry.Value);
                }
            }

            return new OpenTelemetrySpan(activity);
        }

        public void IncrementCounter(
            string name,
            long value = 1,
            IDictionary<string, object> attributes = null,
            string unit = null,
            string description = null)
        {
            var counter = this.counters.GetOrAdd(
                name,
                instrumentName => this.meter.CreateCounter<long>(instrumentName, unit, description));

            counter.Add(value, ToTagArray(attributes));
        }

        public void RecordHistogram(
            string name,
            double value,
            IDictionary<string, object> attributes = null,
            string unit = null,
            string description = null)
        {
            var histogram = this.histograms.GetOrAdd(
                name,
                instrumentName => this.meter.CreateHistogram<double>(instrumentName, unit, description));

            histogram.Record(value, ToTagArray(attributes));
        }

        public void Dispose()
        {
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

        private static void ConfigureExporter(
            global::OpenTelemetry.Exporter.OtlpExporterOptions exporterOptions,
            TelemetryOptions options)
        {
            exporterOptions.Endpoint = new Uri(options.OtlpEndpoint);
            exporterOptions.Protocol = global::OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;

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

        private static KeyValuePair<string, object>[] ToTagArray(IDictionary<string, object> attributes)
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
    }
}
