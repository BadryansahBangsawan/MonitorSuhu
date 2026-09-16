namespace MonitorSuhu.Core.Models;

public enum SensorKind
{
    Cpu,
    Gpu,
    Ssd,
    Board,
    Ram,
    Fan
}

public sealed record SensorReading(string Id, SensorKind Kind, string Label, double Celsius);

public sealed class HardwareSnapshot
{
    public IReadOnlyList<SensorReading> Readings { get; init; } = [];
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
