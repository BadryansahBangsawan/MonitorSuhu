using Xunit;
using MonitorSuhu.Core.Models;
using MonitorSuhu.Linux.Sensors;

namespace MonitorSuhu.Linux.Tests;

public sealed class HwmonMapperTests
{
    [Fact]
    public void Map_EmptyBindings_PicksCpuSsdGpuFan_NoInventedBoardOrRam()
    {
        using var tree = FixtureTree.Create();
        var result = HwmonMapper.Map(tree.HwmonRoot, new Dictionary<string, string>(), tree.ThermalRoot);

        Assert.Null(result.Error);

        var cpu = Assert.Single(result.Readings, r => r.Kind == SensorKind.Cpu);
        Assert.Equal("cpu", cpu.Id);
        Assert.Equal("CPU", cpu.Label);
        Assert.Equal(45, cpu.Value);

        var ssd = Assert.Single(result.Readings, r => r.Kind == SensorKind.Ssd);
        Assert.Equal("SSD", ssd.Label);
        Assert.Equal(36, ssd.Value);

        var gpu = Assert.Single(result.Readings, r => r.Kind == SensorKind.Gpu);
        Assert.Equal("GPU", gpu.Label);
        Assert.Equal(62, gpu.Value);

        var fan = Assert.Single(result.Readings, r => r.Kind == SensorKind.Fan);
        Assert.Equal("fan", fan.Id);
        Assert.Equal("FAN", fan.Label);
        Assert.Equal(1200, fan.Value);

        Assert.DoesNotContain(result.Readings, r => r.Kind == SensorKind.Board);
        Assert.DoesNotContain(result.Readings, r => r.Kind == SensorKind.Ram);

        Assert.Equal(4, result.Catalog.Count);
        Assert.Contains(result.Catalog, e =>
            e.Id == "hwmon0/temp1_input" && e.Name == "k10temp / Tctl" && e.Value == 45 && !e.IsFan);
        Assert.Contains(result.Catalog, e =>
            e.Id == "hwmon1/temp1_input" && e.Name == "nvme / Composite" && e.Value == 36 && !e.IsFan);
        Assert.Contains(result.Catalog, e =>
            e.Id == "hwmon2/temp1_input" && e.Name == "amdgpu / edge" && e.Value == 62 && !e.IsFan);
        Assert.Contains(result.Catalog, e =>
            e.Id == "hwmon3/fan1_input" && e.Name == "nct6775 / fan1_input" && e.Value == 1200 && e.IsFan);
    }

    [Fact]
    public void Map_CpuBinding_UsesBoundCatalogId()
    {
        using var tree = FixtureTree.Create();
        var bindings = new Dictionary<string, string> { ["Cpu"] = "hwmon1/temp1_input" };
        var result = HwmonMapper.Map(tree.HwmonRoot, bindings, tree.ThermalRoot);

        var cpu = Assert.Single(result.Readings, r => r.Kind == SensorKind.Cpu);
        Assert.Equal("CPU", cpu.Label);
        Assert.Equal(36, cpu.Value);
    }

    [Fact]
    public void Map_EmptyRoot_EmptyReadings()
    {
        using var tree = FixtureTree.Empty();
        var result = HwmonMapper.Map(tree.HwmonRoot, new Dictionary<string, string>(), tree.ThermalRoot);

        Assert.Empty(result.Readings);
        Assert.Empty(result.Catalog);
        Assert.Equal("No hwmon sensors.", result.Error);
    }

    [Fact]
    public void Map_PowerAndGpuBusy_AddsExtras()
    {
        using var tree = FixtureTree.CreateWithExtras();
        var previous = HwmonMapper.ReadCpuStat(tree.ProcStatPrevious);
        var result = HwmonMapper.Map(
            tree.HwmonRoot,
            new Dictionary<string, string>(),
            tree.ThermalRoot,
            tree.ProcStatCurrent,
            previous);

        var gpuLoad = Assert.Single(result.Readings, r => r.Kind == SensorKind.GpuLoad);
        Assert.Equal("GPU%", gpuLoad.Label);
        Assert.Equal(33, gpuLoad.Value);

        var power = Assert.Single(result.Readings, r => r.Kind == SensorKind.Power);
        Assert.Equal("PWR", power.Label);
        Assert.Equal(45, power.Value);

        var cpuLoad = Assert.Single(result.Readings, r => r.Kind == SensorKind.CpuLoad);
        Assert.Equal("CPU%", cpuLoad.Label);
        Assert.Equal(90, cpuLoad.Value);

        Assert.Contains(result.Catalog, e =>
            e.Id == "hwmon2/gpu_busy_percent" && e.Hint == CatalogHint.Load && e.Value == 33);
        Assert.Contains(result.Catalog, e =>
            e.Id == "hwmon2/power1_input" && e.Hint == CatalogHint.Power && e.Value == 45);
    }

    [Fact]
    public void CpuLoadPercent_UsesIdleDelta()
    {
        var percent = HwmonMapper.CpuLoadPercent((Idle: 100, Total: 200), (Idle: 110, Total: 300));
        Assert.Equal(90, percent);
    }

    private sealed class FixtureTree : IDisposable
    {
        public string Root { get; }
        public string HwmonRoot { get; }
        public string ThermalRoot { get; }
        public string ProcStatPrevious { get; }
        public string ProcStatCurrent { get; }

        private FixtureTree(string root, string hwmonRoot, string thermalRoot, string procStatPrevious = "", string procStatCurrent = "")
        {
            Root = root;
            HwmonRoot = hwmonRoot;
            ThermalRoot = thermalRoot;
            ProcStatPrevious = procStatPrevious;
            ProcStatCurrent = procStatCurrent;
        }

        public static FixtureTree Create()
        {
            var root = NewRoot();
            var hwmon = Path.Combine(root, "hwmon");
            Write(Path.Combine(hwmon, "hwmon0", "name"), "k10temp");
            Write(Path.Combine(hwmon, "hwmon0", "temp1_input"), "45000");
            Write(Path.Combine(hwmon, "hwmon0", "temp1_label"), "Tctl");
            Write(Path.Combine(hwmon, "hwmon1", "name"), "nvme");
            Write(Path.Combine(hwmon, "hwmon1", "temp1_input"), "36000");
            Write(Path.Combine(hwmon, "hwmon1", "temp1_label"), "Composite");
            Write(Path.Combine(hwmon, "hwmon2", "name"), "amdgpu");
            Write(Path.Combine(hwmon, "hwmon2", "temp1_input"), "62000");
            Write(Path.Combine(hwmon, "hwmon2", "temp1_label"), "edge");
            Write(Path.Combine(hwmon, "hwmon3", "name"), "nct6775");
            Write(Path.Combine(hwmon, "hwmon3", "fan1_input"), "1200");
            var thermal = Path.Combine(root, "thermal");
            Directory.CreateDirectory(thermal);
            return new FixtureTree(root, hwmon, thermal);
        }

        public static FixtureTree CreateWithExtras()
        {
            var tree = Create();
            Write(Path.Combine(tree.HwmonRoot, "hwmon2", "power1_input"), "45000000");
            Write(Path.Combine(tree.HwmonRoot, "hwmon2", "device", "gpu_busy_percent"), "33");
            var previous = Path.Combine(tree.Root, "stat-prev");
            var current = Path.Combine(tree.Root, "stat-now");
            File.WriteAllText(previous, "cpu 50 0 50 100\n");
            File.WriteAllText(current, "cpu 140 0 50 110\n");
            return new FixtureTree(tree.Root, tree.HwmonRoot, tree.ThermalRoot, previous, current);
        }

        public static FixtureTree Empty()
        {
            var root = NewRoot();
            var hwmon = Path.Combine(root, "hwmon");
            var thermal = Path.Combine(root, "thermal");
            Directory.CreateDirectory(hwmon);
            Directory.CreateDirectory(thermal);
            return new FixtureTree(root, hwmon, thermal);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
            catch (IOException)
            {
            }
        }

        private static string NewRoot()
        {
            var root = Path.Combine(Path.GetTempPath(), "monitorsuhu-hwmon-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void Write(string path, string content)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(path, content);
        }
    }
}
