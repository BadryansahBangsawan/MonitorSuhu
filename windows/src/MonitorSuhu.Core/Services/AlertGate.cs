using MonitorSuhu.Core.Models;

namespace MonitorSuhu.Core.Services;

public sealed record AlertEvent(SensorKind Kind, string Message);

/// <summary>
/// Fires when a visible reading is at or above critical. Cooldown is 60 s per kind
/// even if the value stays critical. Mute skips sound/notification; HUD color is unchanged.
/// </summary>
public sealed class AlertGate
{
    public const long CooldownMs = 60_000;
    public const long MuteMs = 15 * 60_000;

    private readonly Dictionary<SensorKind, long> _lastFired = [];
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

        List<AlertEvent> events = [];
        foreach (var reading in readings)
        {
            if (!settings.IsKindVisible(reading.Kind)) continue;
            if (reading.Value < settings.ThresholdsFor(reading.Kind).Critical) continue;
            if (_lastFired.TryGetValue(reading.Kind, out var last) && now - last < CooldownMs) continue;
            _lastFired[reading.Kind] = now;
            events.Add(new AlertEvent(reading.Kind, $"{reading.Label} {settings.FormatValue(reading)}"));
        }

        return events;
    }
}
