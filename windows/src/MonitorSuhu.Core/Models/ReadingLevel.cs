namespace MonitorSuhu.Core.Models;

public enum ReadingLevel
{
    Ok = 0,
    Warn = 1,
    Critical = 2
}

public static class ReadingLevelUtil
{
    public static ReadingLevel Of(double value, Thresholds t)
    {
        if (value >= t.Critical) return ReadingLevel.Critical;
        if (value >= t.Warn) return ReadingLevel.Warn;
        return ReadingLevel.Ok;
    }

    public static ReadingLevel Of(SensorReading reading, AppSettings settings) =>
        Of(reading.Value, settings.ThresholdsFor(reading.Kind));

    public static ReadingLevel Worst(IEnumerable<ReadingLevel> levels)
    {
        var worst = ReadingLevel.Ok;
        foreach (var level in levels)
        {
            if (level > worst) worst = level;
        }
        return worst;
    }

    public static ReadingLevel Worst(IEnumerable<SensorReading> readings, AppSettings settings) =>
        Worst(readings.Select(r => Of(r, settings)));
}
