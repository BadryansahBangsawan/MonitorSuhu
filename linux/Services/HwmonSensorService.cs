using MonitorSuhu.Core.Models;
using MonitorSuhu.Linux.Sensors;

namespace MonitorSuhu.Linux.Services;

public sealed class HwmonSensorService : IDisposable
{
    private const int HistoryCap = 30;

    private readonly System.Timers.Timer _timer;
    private readonly object _gate = new();
    private readonly Func<IReadOnlyDictionary<string, string>>? _bindings;
    private readonly string _hwmonRoot;
    private readonly string _thermalRoot;
    private readonly string _procStatPath;
    private readonly Dictionary<SensorKind, List<double>> _rings = new();
    private readonly bool _isLinux;
    private (ulong Idle, ulong Total)? _cpuStat;

    public HardwareSnapshot Snapshot { get; private set; } = new();
    public IReadOnlyList<HwmonCatalogEntry> LastCatalog { get; private set; } = [];
    public string? LastError { get; private set; }
    public event Action<HardwareSnapshot>? Updated;

    public HwmonSensorService(
        Func<IReadOnlyDictionary<string, string>>? bindings = null,
        string hwmonRoot = "/sys/class/hwmon",
        string thermalRoot = "/sys/class/thermal",
        string procStatPath = "/proc/stat")
    {
        _bindings = bindings;
        _hwmonRoot = hwmonRoot;
        _thermalRoot = thermalRoot;
        _procStatPath = procStatPath;
        _timer = new System.Timers.Timer(1000);
        _timer.AutoReset = true;
        _timer.Elapsed += (_, _) => Tick();

        _isLinux = OperatingSystem.IsLinux();
        if (!_isLinux)
        {
            LastError = "Hardware sensors require Linux.";
        }
    }

    public void Start(int intervalMs)
    {
        _timer.Interval = Math.Clamp(intervalMs, 400, 3000);
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
            if (!_isLinux)
            {
                LastCatalog = [];
                snapshot = new HardwareSnapshot { Readings = [], Timestamp = DateTime.Now };
            }
            else
            {
                var bindings = _bindings?.Invoke() ?? new Dictionary<string, string>();
                try
                {
                    var mapped = HwmonMapper.Map(
                        _hwmonRoot,
                        bindings,
                        _thermalRoot,
                        _procStatPath,
                        _cpuStat);
                    LastCatalog = mapped.Catalog ?? [];
                    LastError = mapped.Error;
                    _cpuStat = mapped.CpuStat;
                    snapshot = new HardwareSnapshot
                    {
                        Readings = mapped.Readings ?? [],
                        Timestamp = DateTime.Now
                    };
                }
                catch (Exception ex)
                {
                    LastCatalog = [];
                    LastError = string.IsNullOrWhiteSpace(ex.Message)
                        ? "No hwmon sensors."
                        : ex.Message;
                    snapshot = new HardwareSnapshot { Readings = [], Timestamp = DateTime.Now };
                }
            }

            foreach (var reading in snapshot.Readings)
            {
                if (!_rings.TryGetValue(reading.Kind, out var ring))
                {
                    ring = new List<double>(HistoryCap);
                    _rings[reading.Kind] = ring;
                }

                ring.Add(reading.Value);
                if (ring.Count > HistoryCap)
                {
                    ring.RemoveAt(0);
                }
            }

            Snapshot = snapshot;
        }

        Updated?.Invoke(snapshot);
    }

    public void Dispose() => _timer.Dispose();
}
