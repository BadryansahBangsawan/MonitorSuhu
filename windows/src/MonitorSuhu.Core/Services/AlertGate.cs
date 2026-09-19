using MonitorSuhu.Core.Models;

namespace MonitorSuhu.Core.Services;

public sealed record AlertEvent(SensorKind Kind, string Message);

/// <summary>
/// Fires on transition into critical per kind; stay-critical does not re-fire;
/// drop below critical then re-cross does fire. Mute/disable skip sound without
/// clearing <c>_active</c>. HUD color is unchanged.
/// </summary>
public sealed class AlertGate
{
    public const long MuteMs = 15 * 60_000;

    private readonly HashSet<SensorKind> _active = [];
    private readonly Func<long> _nowMs;

    public AlertGate(Func<long>? nowMs = null)
    {
        _nowMs = nowMs ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    public IReadOnlyList<AlertEvent> Evaluate(IReadOnlyList<SensorReading> readings, AppSettings settings)
    {
        if (!settings.AlertsEnabled) return [];
        var now = _nowMs();
        if (settings.AlertMuteUntil is long until && until > now) return [];

        var currently = new HashSet<SensorKind>();
        List<AlertEvent> events = [];
        foreach (var reading in readings)
        {
            if (!settings.IsKindVisible(reading.Kind)) continue;
            if (reading.Value < settings.ThresholdsFor(reading.Kind).Critical) continue;
            currently.Add(reading.Kind);
            if (_active.Contains(reading.Kind)) continue;
            _active.Add(reading.Kind);
            events.Add(new AlertEvent(reading.Kind, $"{reading.Label} {settings.FormatValue(reading)}"));
        }
        _active.RemoveWhere(k => !currently.Contains(k));
        return events;
    }
}
