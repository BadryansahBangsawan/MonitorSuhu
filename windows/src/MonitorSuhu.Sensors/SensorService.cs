using LibreHardwareMonitor.Hardware;
using MonitorSuhu.Core.Models;

namespace MonitorSuhu.Sensors;

public sealed class SensorService : IDisposable
{
    private readonly Computer? _computer;
    private readonly System.Timers.Timer _timer;
    private readonly object _gate = new();
    public HardwareSnapshot Snapshot { get; private set; } = new();
    public string? LastError { get; }
    public event Action<HardwareSnapshot>? Updated;

    public SensorService()
    {
        _timer = new System.Timers.Timer(1000);
        _timer.AutoReset = true;
        _timer.Elapsed += (_, _) => Tick();

        if (!OperatingSystem.IsWindows())
        {
            LastError = "Hardware sensors require Windows.";
            return;
        }

        try
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMotherboardEnabled = true,
                IsMemoryEnabled = true,
                IsStorageEnabled = true
            };
            _computer.Open();
        }
        catch (Exception ex)
        {
            _computer = null;
            LastError = string.IsNullOrWhiteSpace(ex.Message)
                ? "Could not open hardware sensors. Run as Administrator and allow the LibreHardwareMonitor driver."
                : $"Could not open hardware sensors: {ex.Message}";
        }
    }

    public void Start(int intervalMs)
    {
        _timer.Interval = Math.Clamp(intervalMs, 400, 5000);
        _timer.Start();
        Tick();
    }

    public void Stop() => _timer.Stop();

    private void Tick()
    {
        HardwareSnapshot snapshot;
        lock (_gate)
        {
            if (_computer is null)
            {
                snapshot = new HardwareSnapshot { Readings = [], Timestamp = DateTime.Now };
            }
            else
            {
                snapshot = new HardwareSnapshot
                {
                    Readings = SensorMapper.Map(_computer),
                    Timestamp = DateTime.Now
                };
            }
            Snapshot = snapshot;
        }
        Updated?.Invoke(snapshot);
    }

    public void Dispose()
    {
        _timer.Dispose();
        try { _computer?.Close(); } catch { /* driver already gone */ }
    }
}
