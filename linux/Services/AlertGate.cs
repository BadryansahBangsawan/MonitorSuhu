using MonitorSuhu.Core.Models;

namespace MonitorSuhu.Linux.Services;

public sealed class AlertGate
{
    private readonly HashSet<SensorKind> _inCritical = [];

    public void Evaluate(IReadOnlyList<SensorReading> readings, AppSettings settings)
    {
        HashSet<SensorKind> now = [];
        foreach (var reading in readings)
        {
            if (!settings.IsKindVisible(reading.Kind)) continue;
            if (reading.Value >= settings.ThresholdsFor(reading.Kind).Critical)
                now.Add(reading.Kind);
        }

        var entered = now.Count > 0 && !now.IsSubsetOf(_inCritical);
        _inCritical.Clear();
        _inCritical.UnionWith(now);
        if (entered)
            Console.Beep();
    }
}
