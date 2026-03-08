namespace FwoTelemetry.Abstractions
{
    public enum TelemetrySpanKind
    {
        Internal = 0,
        Server = 1,
        Client = 2,
        Producer = 3,
        Consumer = 4
    }
}
