using System.Media;
using MonitorSuhu.Core.Models;
using MonitorSuhu.Core.Services;

namespace MonitorSuhu.App.Services;

public sealed class AlertPresenter
{
    private readonly AlertGate _gate = new();
    private readonly Action<string> _notify;

    public AlertPresenter(Action<string> notify)
    {
        _notify = notify;
    }

    public void Evaluate(IReadOnlyList<SensorReading> readings, AppSettings settings)
    {
        foreach (var evt in _gate.Evaluate(readings, settings))
        {
            try { SystemSounds.Exclamation.Play(); } catch { /* speaker optional */ }
            _notify(evt.Message);
        }
    }
}
