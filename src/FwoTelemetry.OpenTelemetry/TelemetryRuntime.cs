using System;
using FwoTelemetry.Abstractions;

namespace FwoTelemetry.OpenTelemetry
{
    public static class TelemetryRuntime
    {
        private static readonly object SyncRoot = new object();
        private static ITelemetryAdapter current = DisabledTelemetryAdapter.Instance;
        private static bool initialized;

        public static ITelemetryAdapter Current
        {
            get { return current; }
        }

        public static ITelemetryAdapter Initialize(TelemetryOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            lock (SyncRoot)
            {
                if (initialized)
                {
                    return current;
                }

                current = new OpenTelemetryAdapter(options);
                initialized = true;
                return current;
            }
        }

        public static ITelemetryAdapter InitializeFromAppSettings(string prefix = "FwoTelemetry:")
        {
            return Initialize(TelemetryOptionsLoader.LoadFromAppSettings(prefix));
        }

        public static bool ForceFlush(int timeoutMilliseconds)
        {
            lock (SyncRoot)
            {
                return current.ForceFlush(timeoutMilliseconds);
            }
        }

        public static void Shutdown()
        {
            lock (SyncRoot)
            {
                current.Dispose();
                current = DisabledTelemetryAdapter.Instance;
                initialized = false;
            }
        }
    }
}
