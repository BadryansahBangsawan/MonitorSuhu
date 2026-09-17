using System.Globalization;
using MonitorSuhu.Core.Models;

namespace MonitorSuhu.Linux.Sensors;

public sealed record HwmonCatalogEntry(string Id, string Name, double Value, CatalogHint Hint)
{
    public bool IsFan => Hint == CatalogHint.Fan;
}

public sealed class HwmonMapResult
{
    public IReadOnlyList<SensorReading> Readings { get; init; } = [];
    public IReadOnlyList<HwmonCatalogEntry> Catalog { get; init; } = [];
    public string? Error { get; init; }
    public (ulong Idle, ulong Total)? CpuStat { get; init; }
}

public static class HwmonMapper
{
    private static readonly string[] CpuInclude =
        ["k10temp", "coretemp", "zenpower", "cpu", "tctl", "tdie", "package", "soc", "pacc", "eacc"];
    private static readonly string[] CpuExclude = ["tdev"];
    private static readonly string[] GpuInclude =
        ["amdgpu", "nouveau", "nvidia", "radeon", "gpu", "i915", "xe"];
    private static readonly string[] GpuExclude = ["tdev"];
    private static readonly string[] SsdInclude = ["nvme", "drivetemp", "nand", "ssd", "storage"];
    private static readonly string[] SsdExclude = [];
    private static readonly string[] BoardInclude =
        ["acpitz", "pch", "wifi", "iwlwifi", "ambient", "thinkpad"];
    private static readonly string[] BoardExclude =
    [
        "k10temp", "coretemp", "zenpower", "cpu", "tctl", "tdie", "package", "soc", "pacc", "eacc",
        "nvme", "drivetemp", "nand", "ssd", "storage",
        "tdev"
    ];
    private static readonly string[] RamInclude = ["dimm", "spd", "memory", "ram"];
    private static readonly string[] RamExclude = [];

    public static HwmonMapResult Map(
        string hwmonRoot,
        IReadOnlyDictionary<string, string> bindings,
        string thermalRoot = "/sys/class/thermal",
        string procStatPath = "/proc/stat",
        (ulong Idle, ulong Total)? previousCpu = null)
    {
        string? error = null;
        var catalog = new List<HwmonCatalogEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        try
        {
            if (Directory.Exists(hwmonRoot))
            {
                var dirs = Directory.GetDirectories(hwmonRoot);
                Array.Sort(dirs, StringComparer.Ordinal);
                foreach (var dir in dirs)
                {
                    try
                    {
                        AddChip(dir, catalog, seen);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        error ??= ex.Message;
                    }
                    catch (IOException ex)
                    {
                        error ??= ex.Message;
                    }
                }
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            error ??= ex.Message;
        }
        catch (IOException ex)
        {
            error ??= ex.Message;
        }

        var readings = new List<SensorReading>();
        if (catalog.Count > 0)
        {
            MapFromCatalog(catalog, bindings, readings);
        }
        else
        {
            try
            {
                ScanThermal(thermalRoot, readings);
            }
            catch (UnauthorizedAccessException ex)
            {
                error ??= ex.Message;
            }
            catch (IOException ex)
            {
                error ??= ex.Message;
            }
        }

        var cpuStat = ReadCpuStat(procStatPath);
        if (readings.Count > 0)
        {
            AddExtras(catalog, bindings, readings, previousCpu, cpuStat);
        }

        if (readings.Count == 0)
        {
            return new HwmonMapResult
            {
                Readings = [],
                Catalog = catalog,
                CpuStat = cpuStat,
                Error = error ?? "No hwmon sensors."
            };
        }

        return new HwmonMapResult
        {
            Readings = readings,
            Catalog = catalog,
            CpuStat = cpuStat,
            Error = null
        };
    }

    private static void MapFromCatalog(
        List<HwmonCatalogEntry> catalog,
        IReadOnlyDictionary<string, string> bindings,
        List<SensorReading> readings)
    {
        var byId = new Dictionary<string, HwmonCatalogEntry>(StringComparer.Ordinal);
        foreach (var entry in catalog)
        {
            byId.TryAdd(entry.Id, entry);
        }

        if (!TryBind(readings, byId, bindings, SensorKind.Cpu, "cpu", "CPU", CatalogHint.Temp))
        {
            var cpu = PickMax(catalog, CpuInclude, CpuExclude);
            if (cpu is not null)
            {
                readings.Add(new SensorReading("cpu", SensorKind.Cpu, "CPU", cpu.Value));
            }
        }

        if (!TryBind(readings, byId, bindings, SensorKind.Gpu, "gpu-0", "GPU", CatalogHint.Temp))
        {
            AddGrouped(readings, catalog, GpuInclude, GpuExclude, SensorKind.Gpu);
        }

        if (!TryBind(readings, byId, bindings, SensorKind.Ssd, "ssd-0", "SSD", CatalogHint.Temp))
        {
            AddGrouped(readings, catalog, SsdInclude, SsdExclude, SensorKind.Ssd);
        }

        if (!TryBind(readings, byId, bindings, SensorKind.Board, "board", "BOARD", CatalogHint.Temp))
        {
            var board = PickMax(catalog, BoardInclude, BoardExclude);
            if (board is not null)
            {
                readings.Add(new SensorReading("board", SensorKind.Board, "BOARD", board.Value));
            }
        }

        if (!TryBind(readings, byId, bindings, SensorKind.Ram, "ram", "RAM", CatalogHint.Temp))
        {
            var ram = PickMax(catalog, RamInclude, RamExclude);
            if (ram is not null)
            {
                readings.Add(new SensorReading("ram", SensorKind.Ram, "RAM", ram.Value));
            }
        }

        double? rpm = null;
        if (bindings.TryGetValue(nameof(SensorKind.Fan), out var fanId)
            && !string.IsNullOrWhiteSpace(fanId)
            && byId.TryGetValue(fanId, out var fanHit)
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
    }

    private static void AddExtras(
        List<HwmonCatalogEntry> catalog,
        IReadOnlyDictionary<string, string> bindings,
        List<SensorReading> readings,
        (ulong Idle, ulong Total)? previousCpu,
        (ulong Idle, ulong Total)? cpuStat)
    {
        var byId = new Dictionary<string, HwmonCatalogEntry>(StringComparer.Ordinal);
        foreach (var entry in catalog)
        {
            byId.TryAdd(entry.Id, entry);
        }

        if (!TryBind(readings, byId, bindings, SensorKind.CpuLoad, "cpu-load", "CPU%", CatalogHint.Load)
            && CpuLoadPercent(previousCpu, cpuStat) is { } cpuLoad)
        {
            readings.Add(new SensorReading("cpu-load", SensorKind.CpuLoad, "CPU%", cpuLoad));
        }

        if (!TryBind(readings, byId, bindings, SensorKind.GpuLoad, "gpu-load", "GPU%", CatalogHint.Load))
        {
            var gpu = catalog
                .Where(e => e.Hint == CatalogHint.Load)
                .OrderByDescending(e => e.Value)
                .FirstOrDefault();
            if (gpu is not null)
            {
                readings.Add(new SensorReading("gpu-load", SensorKind.GpuLoad, "GPU%", gpu.Value));
            }
        }

        if (!TryBind(readings, byId, bindings, SensorKind.Power, "power", "PWR", CatalogHint.Power))
        {
            var watts = 0d;
            var any = false;
            foreach (var entry in catalog)
            {
                if (entry.Hint != CatalogHint.Power || entry.Value <= 0) continue;
                watts += entry.Value;
                any = true;
            }
            if (any)
            {
                readings.Add(new SensorReading("power", SensorKind.Power, "PWR", watts));
            }
        }
    }

    public static double? CpuLoadPercent(
        (ulong Idle, ulong Total)? previous,
        (ulong Idle, ulong Total)? current)
    {
        if (previous is not { } prev || current is not { } now) return null;
        if (now.Total <= prev.Total) return null;
        var totalDelta = now.Total - prev.Total;
        var idleDelta = now.Idle >= prev.Idle ? now.Idle - prev.Idle : 0;
        var busy = 1.0 - (double)idleDelta / totalDelta;
        if (!double.IsFinite(busy)) return null;
        return Math.Clamp(busy * 100, 0, 100);
    }

    public static (ulong Idle, ulong Total)? ReadCpuStat(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var reader = new StreamReader(path);
            var line = reader.ReadLine();
            if (line is null || !line.StartsWith("cpu ", StringComparison.Ordinal)) return null;
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5) return null;
            ulong total = 0;
            for (var i = 1; i < parts.Length; i++)
            {
                if (!ulong.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                    return null;
                total += n;
            }
            if (!ulong.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var idle))
                return null;
            ulong idleAll = idle;
            if (parts.Length > 5
                && ulong.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var iowait))
            {
                idleAll += iowait;
            }
            return (idleAll, total);
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static void AddChip(string chipDir, List<HwmonCatalogEntry> catalog, HashSet<string> seen)
    {
        var dirName = Path.GetFileName(chipDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrEmpty(dirName))
        {
            return;
        }

        var chipName = dirName;
        var namePath = Path.Combine(chipDir, "name");
        try
        {
            if (File.Exists(namePath))
            {
                var text = File.ReadAllText(namePath).Trim();
                if (text.Length > 0)
                {
                    chipName = text;
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }
        catch (IOException)
        {
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(chipDir);
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }

        Array.Sort(files, StringComparer.Ordinal);
        foreach (var path in files)
        {
            var fileName = Path.GetFileName(path);
            if (IsTempInput(fileName))
            {
                if (!TryReadMilliCelsius(path, out var celsius))
                {
                    continue;
                }

                AddEntry(catalog, seen, dirName, fileName, chipName, ReadLabel(chipDir, fileName), celsius, CatalogHint.Temp);
            }
            else if (IsFanInput(fileName))
            {
                if (!TryReadRpm(path, out var rpm))
                {
                    continue;
                }

                AddEntry(catalog, seen, dirName, fileName, chipName, ReadLabel(chipDir, fileName), rpm, CatalogHint.Fan);
            }
            else if (IsPowerInput(fileName))
            {
                if (!TryReadWatts(path, out var watts))
                {
                    continue;
                }

                AddEntry(catalog, seen, dirName, fileName, chipName, ReadLabel(chipDir, fileName), watts, CatalogHint.Power);
            }
        }

        TryAddGpuBusy(chipDir, dirName, chipName, catalog, seen);
    }

    private static void AddEntry(
        List<HwmonCatalogEntry> catalog,
        HashSet<string> seen,
        string dirName,
        string fileName,
        string chipName,
        string label,
        double value,
        CatalogHint hint)
    {
        var id = $"{dirName}/{fileName}";
        if (!seen.Add(id))
        {
            return;
        }

        catalog.Add(new HwmonCatalogEntry(id, $"{chipName} / {label}", value, hint));
    }

    private static bool IsTempInput(string fileName) =>
        fileName.StartsWith("temp", StringComparison.Ordinal)
        && fileName.EndsWith("_input", StringComparison.Ordinal)
        && IsDigits(fileName, 4, fileName.Length - "_input".Length);

    private static bool IsFanInput(string fileName) =>
        fileName.StartsWith("fan", StringComparison.Ordinal)
        && fileName.EndsWith("_input", StringComparison.Ordinal)
        && IsDigits(fileName, 3, fileName.Length - "_input".Length);

    private static bool IsPowerInput(string fileName) =>
        fileName.StartsWith("power", StringComparison.Ordinal)
        && fileName.EndsWith("_input", StringComparison.Ordinal)
        && IsDigits(fileName, 5, fileName.Length - "_input".Length);

    private static bool IsDigits(string fileName, int start, int end)
    {
        if (end <= start)
        {
            return false;
        }

        for (var i = start; i < end; i++)
        {
            if (!char.IsAsciiDigit(fileName[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static string ReadLabel(string chipDir, string inputFileName)
    {
        if (!inputFileName.EndsWith("_input", StringComparison.Ordinal))
        {
            return inputFileName;
        }

        var labelPath = Path.Combine(chipDir, string.Concat(inputFileName.AsSpan(0, inputFileName.Length - 6), "_label"));
        try
        {
            if (File.Exists(labelPath))
            {
                var label = File.ReadAllText(labelPath).Trim();
                if (label.Length > 0)
                {
                    return label;
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (IOException)
        {
        }

        return inputFileName;
    }

    private static bool TryReadMilliCelsius(string path, out double celsius)
    {
        celsius = 0;
        if (!TryReadInteger(path, out var raw))
        {
            return false;
        }

        celsius = raw / 1000.0;
        return double.IsFinite(celsius) && celsius > 1 && celsius < 110;
    }

    private static bool TryReadRpm(string path, out double rpm)
    {
        rpm = 0;
        if (!TryReadInteger(path, out var raw))
        {
            return false;
        }

        rpm = raw;
        return double.IsFinite(rpm) && rpm > 0;
    }

    private static bool TryReadWatts(string path, out double watts)
    {
        watts = 0;
        if (!TryReadInteger(path, out var raw) || raw <= 0)
        {
            return false;
        }

        // hwmon power*_input is microwatts.
        watts = raw / 1_000_000.0;
        return double.IsFinite(watts) && watts > 0 && watts < 2000;
    }

    private static void TryAddGpuBusy(
        string chipDir,
        string dirName,
        string chipName,
        List<HwmonCatalogEntry> catalog,
        HashSet<string> seen)
    {
        if (!chipName.Contains("amdgpu", StringComparison.OrdinalIgnoreCase)
            && !chipName.Contains("gpu", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var candidate in new[]
                 {
                     Path.Combine(chipDir, "device", "gpu_busy_percent"),
                     Path.Combine(chipDir, "gpu_busy_percent")
                 })
        {
            if (!TryReadInteger(candidate, out var percent) || percent is < 0 or > 100)
            {
                continue;
            }

            AddEntry(catalog, seen, dirName, "gpu_busy_percent", chipName, "gpu_busy_percent", percent, CatalogHint.Load);
            return;
        }
    }

    private static bool TryReadInteger(string path, out long value)
    {
        value = 0;
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            var text = File.ReadAllText(path).Trim();
            return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static bool TryBind(
        List<SensorReading> readings,
        IReadOnlyDictionary<string, HwmonCatalogEntry> byId,
        IReadOnlyDictionary<string, string> bindings,
        SensorKind kind,
        string readingId,
        string label,
        CatalogHint want)
    {
        if (!bindings.TryGetValue(kind.ToString(), out var id) || string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        if (!byId.TryGetValue(id, out var hit) || hit.Hint != want)
        {
            return false;
        }

        readings.Add(new SensorReading(readingId, kind, label, hit.Value));
        return true;
    }

    private static HwmonCatalogEntry? PickMax(
        List<HwmonCatalogEntry> catalog,
        string[] include,
        string[] exclude)
    {
        HwmonCatalogEntry? best = null;
        foreach (var entry in catalog)
        {
            if (entry.Hint != CatalogHint.Temp || !Matches(entry, include, exclude))
            {
                continue;
            }

            if (best is null || entry.Value > best.Value)
            {
                best = entry;
            }
        }

        return best;
    }

    private static void AddGrouped(
        List<SensorReading> readings,
        List<HwmonCatalogEntry> catalog,
        string[] include,
        string[] exclude,
        SensorKind kind)
    {
        var bestByChip = new Dictionary<string, HwmonCatalogEntry>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var entry in catalog)
        {
            if (entry.Hint != CatalogHint.Temp || !Matches(entry, include, exclude))
            {
                continue;
            }

            var chip = ChipKey(entry.Id);
            if (bestByChip.TryGetValue(chip, out var existing))
            {
                if (entry.Value > existing.Value)
                {
                    bestByChip[chip] = entry;
                }
            }
            else
            {
                bestByChip[chip] = entry;
                order.Add(chip);
            }
        }

        for (var i = 0; i < order.Count; i++)
        {
            var entry = bestByChip[order[i]];
            var id = kind == SensorKind.Gpu ? $"gpu-{i}" : $"ssd-{i}";
            readings.Add(new SensorReading(id, kind, LabelFor(kind, i, entry), entry.Value));
        }
    }

    private static string ChipKey(string id)
    {
        var slash = id.IndexOf('/');
        return slash <= 0 ? id : id[..slash];
    }

    private static string LabelFor(SensorKind kind, int index, HwmonCatalogEntry entry)
    {
        if (kind == SensorKind.Gpu)
        {
            return index == 0 ? "GPU" : $"GPU {index}";
        }

        if (index == 0)
        {
            return "SSD";
        }

        var sep = entry.Name.IndexOf(" / ", StringComparison.Ordinal);
        var chipName = sep < 0 ? entry.Name : entry.Name[..sep];
        var shortName = Shorten(chipName);
        return string.IsNullOrWhiteSpace(shortName) ? $"SSD {index}" : shortName;
    }

    private static string Shorten(string name)
    {
        var trimmed = name.Replace("NVMe", "", StringComparison.Ordinal)
            .Replace("SSD", "", StringComparison.Ordinal)
            .Trim();
        return trimmed.Length <= 12 ? trimmed : trimmed[..12];
    }

    private static bool Matches(HwmonCatalogEntry entry, string[] include, string[] exclude)
    {
        var hay = (entry.Name + " " + entry.Id).ToLowerInvariant();
        foreach (var token in exclude)
        {
            if (hay.Contains(token, StringComparison.Ordinal))
            {
                return false;
            }
        }

        foreach (var token in include)
        {
            if (hay.Contains(token, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void ScanThermal(string thermalRoot, List<SensorReading> readings)
    {
        if (!Directory.Exists(thermalRoot))
        {
            return;
        }

        var dirs = Directory.GetDirectories(thermalRoot, "thermal_zone*");
        Array.Sort(dirs, StringComparer.Ordinal);

        double? cpu = null;
        double? board = null;
        foreach (var dir in dirs)
        {
            var typePath = Path.Combine(dir, "type");
            var tempPath = Path.Combine(dir, "temp");
            string type;
            try
            {
                if (!File.Exists(typePath) || !TryReadMilliCelsius(tempPath, out var celsius))
                {
                    continue;
                }

                type = File.Exists(typePath) ? File.ReadAllText(typePath).Trim() : "";
                if (type.Equals("x86_pkg_temp", StringComparison.OrdinalIgnoreCase))
                {
                    if (cpu is null || celsius > cpu)
                    {
                        cpu = celsius;
                    }
                }
                else if (board is null || celsius > board)
                {
                    board = celsius;
                }
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }
        }

        if (cpu is not null)
        {
            readings.Add(new SensorReading("cpu", SensorKind.Cpu, "CPU", cpu.Value));
        }

        if (board is not null)
        {
            readings.Add(new SensorReading("board", SensorKind.Board, "BOARD", board.Value));
        }
    }
}
