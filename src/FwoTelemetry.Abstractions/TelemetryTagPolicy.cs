using System.Collections.Generic;

namespace FwoTelemetry.Abstractions
{
    public sealed class TelemetryTagPolicy
    {
        public TelemetryTagPolicy()
        {
            this.AllowedKeys = new HashSet<string>();
            this.SensitiveKeys = new HashSet<string>();
            this.MaxValueLength = 256;
        }

        public ISet<string> AllowedKeys { get; private set; }

        public ISet<string> SensitiveKeys { get; private set; }

        public bool DropUnknownKeys { get; set; }

        public int MaxValueLength { get; set; }
    }
}
