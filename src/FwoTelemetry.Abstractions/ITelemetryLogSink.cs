namespace FwoTelemetry.Abstractions
{
    public interface ITelemetryLogSink
    {
        void Write(TelemetryLogEntry entry);
    }
}
