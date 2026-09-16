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
    public bool CompactHud { get; set; }
    public bool ShowSparkline { get; set; }
    public bool ShowFan { get; set; } = true;
    public Dictionary<string, string> SensorBindings { get; set; } = new();
    public OverlayPosition? Position { get; set; }
    public Dictionary<string, Thresholds> Thresholds { get; set; } = new()
    {
        [nameof(SensorKind.Cpu)] = new Thresholds { Warn = 75, Critical = 90 },
        [nameof(SensorKind.Gpu)] = new Thresholds { Warn = 75, Critical = 90 },
        [nameof(SensorKind.Ssd)] = new Thresholds { Warn = 60, Critical = 70 },
        [nameof(SensorKind.Board)] = new Thresholds { Warn = 70, Critical = 85 },
        [nameof(SensorKind.Ram)] = new Thresholds { Warn = 70, Critical = 85 },
        [nameof(SensorKind.Fan)] = new Thresholds { Warn = 4000, Critical = 5500 }
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
        SensorKind.Fan => ShowFan,
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
        Sanitize();
    }

    public void Sanitize()
    {
        OverlayOpacity = Clamp(OverlayOpacity, 0.4, 0.95);
        FontSize = Clamp(FontSize, 11, 18);
        PollIntervalMs = (int)Clamp(PollIntervalMs, 400, 3000);
        var hex = NormalizeHex(AccentHex);
        AccentHex = hex.Length == 6 ? "#" + hex : NvidiaGreen;

        var next = new Dictionary<string, Thresholds>
        {
            [nameof(SensorKind.Cpu)] = new Thresholds { Warn = 75, Critical = 90 },
            [nameof(SensorKind.Gpu)] = new Thresholds { Warn = 75, Critical = 90 },
            [nameof(SensorKind.Ssd)] = new Thresholds { Warn = 60, Critical = 70 },
            [nameof(SensorKind.Board)] = new Thresholds { Warn = 70, Critical = 85 },
            [nameof(SensorKind.Ram)] = new Thresholds { Warn = 70, Critical = 85 },
            [nameof(SensorKind.Fan)] = new Thresholds { Warn = 4000, Critical = 5500 }
        };
        foreach (var (key, value) in Thresholds ?? [])
        {
            var t = new Thresholds { Warn = value.Warn, Critical = value.Critical };
            if (key == nameof(SensorKind.Fan))
            {
                t.Warn = Clamp(t.Warn, 500, 8000);
                t.Critical = Clamp(t.Critical, 600, 10000);
                if (t.Warn >= t.Critical) t.Critical = Math.Min(10000, t.Warn + 5);
            }
            else
            {
                t.Warn = Clamp(t.Warn, 1, 120);
                t.Critical = Clamp(t.Critical, 2, 130);
                if (t.Warn >= t.Critical) t.Critical = Math.Min(130, t.Warn + 5);
            }
            next[key] = t;
        }
        Thresholds = next;
        SensorBindings ??= new();
    }

    public static string NormalizeHex(string hex)
    {
        var s = (hex ?? "").Trim().ToUpperInvariant();
        if (s.StartsWith('#')) s = s[1..];
        return s;
    }

    private static double Clamp(double value, double lo, double hi) =>
        Math.Min(hi, Math.Max(lo, value));

    public string FormatTemperature(double celsius)
    {
        if (UseFahrenheit)
        {
            return $"{Math.Round(celsius * 9 / 5 + 32)}°F";
        }
        return $"{Math.Round(celsius)}°C";
    }

    public string FormatValue(SensorReading r) =>
        r.Kind == SensorKind.Fan
            ? $"{Math.Round(r.Value)} RPM"
            : FormatTemperature(r.Value);

    public string MenuBarTitle(double? cpuCelsius)
    {
        if (cpuCelsius is not double c) return "Suhu";
        var n = UseFahrenheit ? Math.Round(c * 9 / 5 + 32) : Math.Round(c);
        return $"Suhu {n}°";
    }
}
