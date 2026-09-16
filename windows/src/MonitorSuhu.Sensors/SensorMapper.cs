using LibreHardwareMonitor.Hardware;
using MonitorSuhu.Core.Models;

namespace MonitorSuhu.Sensors;

internal static class SensorMapper
{
    public static IReadOnlyList<SensorReading> Map(IComputer computer)
    {
        var readings = new List<SensorReading>();
        var gpuIndex = 0;
        var ssdIndex = 0;

        foreach (var hardware in computer.Hardware)
        {
            hardware.Update();
            foreach (var sub in hardware.SubHardware)
            {
                sub.Update();
            }

            switch (hardware.HardwareType)
            {
                case HardwareType.Cpu:
                    if (PickCpu(hardware) is { } cpu)
                    {
                        readings.Add(new SensorReading("cpu", SensorKind.Cpu, "CPU", cpu));
                    }
                    break;

                case HardwareType.GpuNvidia:
                case HardwareType.GpuAmd:
                case HardwareType.GpuIntel:
                    if (PickNamed(hardware, "GPU Core", "GPU Hot Spot", "Temperature") is { } gpu)
                    {
                        var label = gpuIndex == 0 ? "GPU" : $"GPU {gpuIndex}";
                        readings.Add(new SensorReading($"gpu-{gpuIndex}", SensorKind.Gpu, label, gpu));
                        gpuIndex++;
                    }
                    break;

                case HardwareType.Storage:
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
                    if (PickBoard(hardware) is { } board)
                    {
                        readings.Add(new SensorReading("board", SensorKind.Board, "BOARD", board));
                    }
                    break;

                case HardwareType.Memory:
                    if (PickNamed(hardware, "Temperature") is { } ram)
                    {
                        readings.Add(new SensorReading("ram", SensorKind.Ram, "RAM", ram));
                    }
                    break;
            }
        }

        return readings;
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
        var sensors = AllTemps(board).ToList();
        return First(sensors, "CPU")
            ?? First(sensors, "System")
            ?? First(sensors, "Motherboard")
            ?? Max(sensors);
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

    private static string Shorten(string name)
    {
        var trimmed = name.Replace("NVMe", "").Replace("SSD", "").Trim();
        return trimmed.Length <= 12 ? trimmed : trimmed[..12];
    }
}
