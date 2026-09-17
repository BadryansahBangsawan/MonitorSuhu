using System.Diagnostics;
using MonitorSuhu.Core.Models;
using MonitorSuhu.Core.Services;

namespace MonitorSuhu.Linux.Services;

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
            _notify(evt.Message);
            TryNotifySend(evt.Message);
        }
    }

    private static void TryNotifySend(string message)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "notify-send",
                ArgumentList = { "-a", "MonitorSuhu", "-u", "critical", "MonitorSuhu", message },
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });
        }
        catch (Exception)
        {
            // libnotify is optional; tray tooltip still changes.
        }
    }
}
