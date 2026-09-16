namespace MonitorSuhu.Core.Models;

public sealed class Thresholds
{
    public double Warn { get; set; }
    public double Critical { get; set; }
}

public sealed class OverlayPosition
{
    public string MonitorDeviceName { get; set; } = "";
    public double RelativeX { get; set; }
    public double RelativeY { get; set; }
    /// <summary>Right / bottom edges so a size change can pin the same corner.</summary>
    public double? RelativeMaxX { get; set; }
    public double? RelativeMaxY { get; set; }
}

public sealed class KeyChord
{
    public uint VirtualKey { get; set; }
    public bool Control { get; set; }
    public bool Shift { get; set; }
    public bool Alt { get; set; }
    public bool Win { get; set; }

    public uint Modifiers
    {
        get
        {
            uint m = 0;
            if (Alt) m |= 0x0001;
            if (Control) m |= 0x0002;
            if (Shift) m |= 0x0004;
            if (Win) m |= 0x0008;
            return m;
        }
    }

    public string Display
    {
        get
        {
            var parts = new List<string>();
            if (Control) parts.Add("Ctrl");
            if (Alt) parts.Add("Alt");
            if (Shift) parts.Add("Shift");
            if (Win) parts.Add("Win");
            parts.Add(VirtualKey is >= 0x41 and <= 0x5A
                ? ((char)VirtualKey).ToString()
                : $"0x{VirtualKey:X}");
            return string.Join("+", parts);
        }
    }
}

public sealed class AppSettings
{
    public const string NvidiaGreen = "#76B900";

    public double OverlayOpacity { get; set; } = 0.80;
    public double FontSize { get; set; } = 13;
    public string AccentHex { get; set; } = NvidiaGreen;
    public bool UseFahrenheit { get; set; }
    public int PollIntervalMs { get; set; } = 1000;
    public bool Locked { get; set; } = true;
    public bool OverlayVisible { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool ShowCpu { get; set; } = true;
    public bool ShowGpu { get; set; } = true;
    public bool ShowSsd { get; set; } = true;
    public bool ShowBoard { get; set; } = true;
    public bool ShowRam { get; set; } = true;
    public OverlayPosition? Position { get; set; }
    public Dictionary<string, Thresholds> Thresholds { get; set; } = new()
    {
        [nameof(SensorKind.Cpu)] = new Thresholds { Warn = 75, Critical = 90 },
        [nameof(SensorKind.Gpu)] = new Thresholds { Warn = 75, Critical = 90 },
        [nameof(SensorKind.Ssd)] = new Thresholds { Warn = 60, Critical = 70 },
        [nameof(SensorKind.Board)] = new Thresholds { Warn = 70, Critical = 85 },
        [nameof(SensorKind.Ram)] = new Thresholds { Warn = 70, Critical = 85 }
    };
    public KeyChord ToggleHotkey { get; set; } = new() { VirtualKey = 0x54, Control = true, Shift = true };
    public KeyChord EditHotkey { get; set; } = new() { VirtualKey = 0x45, Control = true, Shift = true };

    public bool IsKindVisible(SensorKind kind) => kind switch
    {
        SensorKind.Cpu => ShowCpu,
        SensorKind.Gpu => ShowGpu,
        SensorKind.Ssd => ShowSsd,
        SensorKind.Board => ShowBoard,
        SensorKind.Ram => ShowRam,
        _ => true
    };

    public Thresholds ThresholdsFor(SensorKind kind)
    {
        var key = kind.ToString();
        return Thresholds.TryGetValue(key, out var t) ? t : new Thresholds { Warn = 75, Critical = 90 };
    }

    public void SetThresholds(SensorKind kind, double warn, double critical)
    {
        Thresholds[kind.ToString()] = new Thresholds { Warn = warn, Critical = critical };
    }

    public string FormatTemperature(double celsius)
    {
        if (UseFahrenheit)
        {
            return $"{Math.Round(celsius * 9 / 5 + 32)}°F";
        }
        return $"{Math.Round(celsius)}°C";
    }
}
