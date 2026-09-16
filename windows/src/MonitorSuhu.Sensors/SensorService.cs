using LibreHardwareMonitor.Hardware;
using MonitorSuhu.Core.Models;

namespace MonitorSuhu.Sensors;

public sealed class SensorService : IDisposable
{
    private const int HistoryCap = 30;

    private readonly Computer? _computer;
    private readonly System.Timers.Timer _timer;
    private readonly object _gate = new();
    private readonly Func<IReadOnlyDictionary<string, string>>? _bindings;
    private readonly Dictionary<SensorKind, List<double>> _rings = new();

    public HardwareSnapshot Snapshot { get; private set; } = new();
    public IReadOnlyList<CatalogEntry> LastCatalog { get; private set; } = [];
    public string? LastError { get; }
    public event Action<HardwareSnapshot>? Updated;

    public SensorService(Func<IReadOnlyDictionary<string, string>>? bindings = null)
    {
        _bindings = bindings;
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
                IsStorageEnabled = true,
                IsControllerEnabled = true
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

    public IReadOnlyList<double> History(SensorKind kind)
    {
        lock (_gate)
        {
            return _rings.TryGetValue(kind, out var ring) ? ring.ToArray() : [];
        }
    }

    private void Tick()
    {
        HardwareSnapshot snapshot;
        lock (_gate)
        {
            if (_computer is null)
            {
                LastCatalog = [];
                snapshot = new HardwareSnapshot { Readings = [], Timestamp = DateTime.Now };
            }
            else
            {
                var bindings = _bindings?.Invoke() ?? new Dictionary<string, string>();
                LastCatalog = SensorMapper.MapCatalog(_computer);
                snapshot = new HardwareSnapshot
                {
                    Readings = SensorMapper.Map(_computer, bindings),
                    Timestamp = DateTime.Now
                };
            }

            foreach (var reading in snapshot.Readings)
            {
                if (!_rings.TryGetValue(reading.Kind, out var ring))
                {
                    ring = new List<double>(HistoryCap);
                    _rings[reading.Kind] = ring;
                }
                ring.Add(reading.Celsius);
                if (ring.Count > HistoryCap)
                {
                    ring.RemoveAt(0);
                }
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
