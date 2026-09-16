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

    private sealed class FixtureTree : IDisposable
    {
        public string Root { get; }
        public string HwmonRoot { get; }
        public string ThermalRoot { get; }

        private FixtureTree(string root, string hwmonRoot, string thermalRoot)
        {
            Root = root;
            HwmonRoot = hwmonRoot;
            ThermalRoot = thermalRoot;
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
