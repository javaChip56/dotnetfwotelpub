namespace FwoTelemetry.Abstractions
{
    public sealed class TelemetryBatchExportProcessorOptions
    {
        public TelemetryBatchExportProcessorOptions()
        {
            this.MaxQueueSize = 2048;
            this.MaxExportBatchSize = 512;
            this.ScheduledDelayMilliseconds = 5000;
            this.ExporterTimeoutMilliseconds = 30000;
        }

        public int MaxQueueSize { get; set; }

        public int MaxExportBatchSize { get; set; }

        public int ScheduledDelayMilliseconds { get; set; }

        public int ExporterTimeoutMilliseconds { get; set; }
    }
}
