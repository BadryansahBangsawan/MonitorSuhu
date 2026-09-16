using LibreHardwareMonitor.Hardware;
using MonitorSuhu.Core.Models;

namespace MonitorSuhu.Sensors;

public static class SensorMapper
{
    public static IReadOnlyList<CatalogEntry> MapCatalog(IComputer computer)
    {
        var list = new List<CatalogEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var hardware in computer.Hardware)
        {
            hardware.Update();
            foreach (var sub in hardware.SubHardware)
            {
                sub.Update();
            }

            AddCatalogRows(list, seen, hardware);
            foreach (var sub in hardware.SubHardware)
            {
                AddCatalogRows(list, seen, sub);
            }
        }

        return list;
    }

    public static IReadOnlyList<SensorReading> Map(
        IComputer computer,
        IReadOnlyDictionary<string, string> bindings) =>
        Map(MapCatalog(computer), computer, bindings);

    public static IReadOnlyList<SensorReading> Map(
        IReadOnlyList<CatalogEntry> catalog,
        IComputer computer,
        IReadOnlyDictionary<string, string> bindings)
    {
        var catalogById = new Dictionary<string, CatalogEntry>(StringComparer.Ordinal);
        foreach (var entry in catalog)
        {
            catalogById.TryAdd(entry.Id, entry);
        }

        var readings = new List<SensorReading>();
        var bound = new HashSet<SensorKind>();

        TryBind(readings, bound, catalogById, bindings, SensorKind.Cpu, "cpu", "CPU", wantFan: false);
        TryBind(readings, bound, catalogById, bindings, SensorKind.Gpu, "gpu-0", "GPU", wantFan: false);
        TryBind(readings, bound, catalogById, bindings, SensorKind.Ssd, "ssd-0", "SSD", wantFan: false);
        TryBind(readings, bound, catalogById, bindings, SensorKind.Board, "board", "BOARD", wantFan: false);
        TryBind(readings, bound, catalogById, bindings, SensorKind.Ram, "ram", "RAM", wantFan: false);

        var gpuIndex = 0;
        var ssdIndex = 0;

        foreach (var hardware in computer.Hardware)
        {
            switch (hardware.HardwareType)
            {
                case HardwareType.Cpu:
                    if (!bound.Contains(SensorKind.Cpu) && PickCpu(hardware) is { } cpu)
                    {
                        readings.Add(new SensorReading("cpu", SensorKind.Cpu, "CPU", cpu));
                    }
                    break;

                case HardwareType.GpuNvidia:
                case HardwareType.GpuAmd:
                case HardwareType.GpuIntel:
                    if (bound.Contains(SensorKind.Gpu))
                    {
                        break;
                    }
                    if (PickNamed(hardware, "GPU Core", "GPU Hot Spot", "Temperature") is { } gpu)
                    {
                        var label = gpuIndex == 0 ? "GPU" : $"GPU {gpuIndex}";
                        readings.Add(new SensorReading($"gpu-{gpuIndex}", SensorKind.Gpu, label, gpu));
                        gpuIndex++;
                    }
                    break;

                case HardwareType.Storage:
                    if (bound.Contains(SensorKind.Ssd))
                    {
                        break;
                    }
                    if (PickNamed(hardware, "Temperature", "Composite") is { } ssd)
                    {
                        var shortName = Shorten(hardware.Name);
                        var label = ssdIndex == 0 ? "SSD" : $"SSD {ssdIndex}";
                        if (!string.IsNullOrWhiteSpace(shortName))
                        {
                            label = ssdIndex == 0 ? "SSD" : shortName;
                        }
                        readings.Add(new SensorReading($"ssd-{ssdIndex}", SensorKind.Ssd, label, ssd));
                        ssdIndex++;
                    }
                    break;

                case HardwareType.Motherboard:
                    if (!bound.Contains(SensorKind.Board) && PickBoard(hardware) is { } board)
                    {
                        readings.Add(new SensorReading("board", SensorKind.Board, "BOARD", board));
                    }
                    break;

                case HardwareType.Memory:
                    if (!bound.Contains(SensorKind.Ram) && PickNamed(hardware, "Temperature") is { } ram)
                    {
                        readings.Add(new SensorReading("ram", SensorKind.Ram, "RAM", ram));
                    }
                    break;
            }
        }

        double? rpm = null;
        if (bindings.TryGetValue(nameof(SensorKind.Fan), out var fanId)
            && !string.IsNullOrWhiteSpace(fanId)
            && catalogById.TryGetValue(fanId, out var fanHit)
            && fanHit.IsFan
            && fanHit.Value > 0)
        {
            rpm = fanHit.Value;
        }
        else
        {
            var max = 0d;
            foreach (var entry in catalog)
            {
                if (entry.IsFan && entry.Value > max)
                {
                    max = entry.Value;
                }
            }
            if (max > 0)
            {
                rpm = max;
            }
        }

        if (rpm is > 0)
        {
            readings.Add(new SensorReading("fan", SensorKind.Fan, "FAN", rpm.Value));
        }

        return readings;
    }

    private static void AddCatalogRows(List<CatalogEntry> list, HashSet<string> seen, IHardware hardware)
    {
        foreach (var sensor in hardware.Sensors)
        {
            var isFan = sensor.SensorType == SensorType.Fan;
            if (!isFan && sensor.SensorType != SensorType.Temperature)
            {
                continue;
            }
            if (sensor.Value is not float raw || !float.IsFinite(raw) || raw <= 0)
            {
                continue;
            }

            var id = sensor.Identifier.ToString();
            if (string.IsNullOrEmpty(id) || !seen.Add(id))
            {
                continue;
            }

            list.Add(new CatalogEntry(id, $"{hardware.Name} / {sensor.Name}", raw, isFan));
        }
    }

    private static void TryBind(
        List<SensorReading> readings,
        HashSet<SensorKind> bound,
        IReadOnlyDictionary<string, CatalogEntry> catalogById,
        IReadOnlyDictionary<string, string> bindings,
        SensorKind kind,
        string id,
        string label,
        bool wantFan)
    {
        if (!bindings.TryGetValue(kind.ToString(), out var boundId) || string.IsNullOrWhiteSpace(boundId))
        {
            return;
        }
        if (!catalogById.TryGetValue(boundId, out var entry) || entry.IsFan != wantFan || entry.Value <= 0)
        {
            return;
        }

        readings.Add(new SensorReading(id, kind, label, entry.Value));
        bound.Add(kind);
    }

    private static double? PickCpu(IHardware cpu)
    {
        var sensors = AllTemps(cpu).ToList();
        return First(sensors, "Package")
            ?? First(sensors, "Tctl")
            ?? First(sensors, "Tdie")
            ?? First(sensors, "CPU")
            ?? Max(sensors);
    }

    private static double? PickBoard(IHardware board)
    {
        var sensors = AllTemps(board)
            .Select(s => (s.Name, (double)(s.Value ?? 0)))
            .ToList();
        return PickBoard(sensors);
    }

    /// <summary>
    /// Prefer board/ambient/chipset temps. A Super-I/O sensor named "CPU"
    /// is last resort so BOARD does not clone the CPU package reading.
    /// </summary>
    public static double? PickBoard(IReadOnlyList<(string Name, double Value)> sensors)
    {
        var valid = sensors.Where(s => s.Value is > 0 and < 120).ToList();
        if (valid.Count == 0) return null;

        var preferred = valid.Where(s => !LooksLikeCpu(s.Name)).ToList();
        var cpuLike = valid.Where(s => LooksLikeCpu(s.Name)).ToList();

        return FirstNamed(preferred, "System")
            ?? FirstNamed(preferred, "Motherboard")
            ?? FirstNamed(preferred, "Ambient")
            ?? FirstNamed(preferred, "Super I/O")
            ?? FirstNamed(preferred, "Chipset")
            ?? MaxValue(preferred)
            ?? FirstNamed(cpuLike, "CPU")
            ?? MaxValue(cpuLike);
    }

    private static bool LooksLikeCpu(string name)
    {
        var n = name.ToLowerInvariant();
        return n.Contains("cpu")
            || n.Contains("package")
            || n.Contains("tctl")
            || n.Contains("tdie")
            || n.Contains("core");
    }

    private static double? PickNamed(IHardware hardware, params string[] names)
    {
        var sensors = AllTemps(hardware).ToList();
        foreach (var name in names)
        {
            var hit = First(sensors, name);
            if (hit is not null) return hit;
        }
        return Max(sensors);
    }

    private static IEnumerable<ISensor> AllTemps(IHardware hardware)
    {
        foreach (var sensor in hardware.Sensors.Where(s => s.SensorType == SensorType.Temperature))
        {
            yield return sensor;
        }
        foreach (var sub in hardware.SubHardware)
        {
            foreach (var sensor in sub.Sensors.Where(s => s.SensorType == SensorType.Temperature))
            {
                yield return sensor;
            }
        }
    }

    private static double? First(IEnumerable<ISensor> sensors, string token)
    {
        var hit = sensors.FirstOrDefault(s =>
            s.Name.Contains(token, StringComparison.OrdinalIgnoreCase) && s.Value is > 0 and < 120);
        return hit?.Value;
    }

    private static double? Max(IEnumerable<ISensor> sensors)
    {
        var values = sensors.Select(s => s.Value).Where(v => v is > 0 and < 120).Cast<float>();
        return values.Any() ? values.Max() : null;
    }

    private static double? FirstNamed(IReadOnlyList<(string Name, double Value)> sensors, string token)
    {
        foreach (var sensor in sensors)
        {
            if (sensor.Name.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return sensor.Value;
            }
        }
        return null;
    }

    private static double? MaxValue(IReadOnlyList<(string Name, double Value)> sensors)
    {
        if (sensors.Count == 0) return null;
        return sensors.Max(s => s.Value);
    }

    private static string Shorten(string name)
    {
        var trimmed = name.Replace("NVMe", "").Replace("SSD", "").Trim();
        return trimmed.Length <= 12 ? trimmed : trimmed[..12];
    }
}
