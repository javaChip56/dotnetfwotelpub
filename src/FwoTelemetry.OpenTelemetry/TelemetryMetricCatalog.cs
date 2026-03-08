using System;
using System.Collections.Concurrent;
using FwoTelemetry.Abstractions;

namespace FwoTelemetry.OpenTelemetry
{
    internal sealed class TelemetryMetricCatalog
    {
        private readonly bool strict;
        private readonly ConcurrentDictionary<string, TelemetryMetricDefinition> definitions;

        public TelemetryMetricCatalog(TelemetryOptions options)
        {
            this.strict = options.StrictMetricSchema;
            this.definitions = new ConcurrentDictionary<string, TelemetryMetricDefinition>(StringComparer.OrdinalIgnoreCase);

            foreach (var definition in options.MetricDefinitions)
            {
                if (definition != null && !string.IsNullOrWhiteSpace(definition.Name))
                {
                    this.definitions[definition.Name] = definition;
                }
            }
        }

        public TelemetryMetricDefinition Resolve(string name, TelemetryMetricType metricType, string unit, string description)
        {
            TelemetryMetricDefinition definition;

            if (this.definitions.TryGetValue(name, out definition))
            {
                if (definition.MetricType != metricType)
                {
                    throw new InvalidOperationException("Metric '" + name + "' is registered with a different type.");
                }

                return definition;
            }

            if (this.strict)
            {
                throw new InvalidOperationException("Metric '" + name + "' is not registered in the telemetry schema.");
            }

            return new TelemetryMetricDefinition
            {
                Name = name,
                MetricType = metricType,
                Unit = unit,
                Description = description,
            };
        }
    }
}
