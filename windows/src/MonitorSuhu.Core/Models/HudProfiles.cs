namespace MonitorSuhu.Core.Models;

/// <summary>
/// Named looks. Copies visibility, extras, compact, sparkline, opacity, font,
/// and thresholds. Position, lock, hotkeys, autostart, poll, and bindings stay.
/// </summary>
public static class HudProfiles
{
    public const string Desktop = "desktop";
    public const string Game = "game";
    public const string Silent = "silent";
    public const string Custom = "custom";

    public static string Title(string name) => name switch
    {
        Desktop => "Desktop",
        Game => "Game",
        Silent => "Silent",
        _ => "Custom"
    };

    public static void Apply(AppSettings settings, string name)
    {
        switch (name)
        {
            case Desktop:
                ResetLook(settings, compact: false, extras: false, opacity: 0.80, font: 13);
                ShowTemps(settings, cpu: true, gpu: true, ssd: true, board: true, ram: true, fan: true);
                settings.Thresholds = AppSettings.DefaultThresholds();
                break;
            case Game:
                ResetLook(settings, compact: true, extras: true, opacity: 0.70, font: 12);
                ShowTemps(settings, cpu: true, gpu: true, ssd: false, board: false, ram: false, fan: true);
                settings.Thresholds = AppSettings.DefaultThresholds();
                settings.Thresholds[nameof(SensorKind.Cpu)] = new Thresholds { Warn = 80, Critical = 95 };
                settings.Thresholds[nameof(SensorKind.Gpu)] = new Thresholds { Warn = 80, Critical = 95 };
                break;
            case Silent:
                ResetLook(settings, compact: false, extras: false, opacity: 0.80, font: 13);
                ShowTemps(settings, cpu: true, gpu: true, ssd: true, board: false, ram: false, fan: false);
                settings.Thresholds = AppSettings.DefaultThresholds();
                break;
            default:
                return;
        }

        settings.ActiveProfile = name;
        settings.Sanitize();
    }

    public static void MarkCustom(AppSettings settings)
    {
        if (settings.ActiveProfile != Custom)
            settings.ActiveProfile = Custom;
    }

    private static void ResetLook(AppSettings settings, bool compact, bool extras, double opacity, double font)
    {
        settings.CompactHud = compact;
        settings.ShowSparkline = false;
        settings.ShowCpuLoad = extras;
        settings.ShowGpuLoad = extras;
        settings.ShowPower = extras;
        settings.OverlayOpacity = opacity;
        settings.FontSize = font;
    }

    private static void ShowTemps(AppSettings settings, bool cpu, bool gpu, bool ssd, bool board, bool ram, bool fan)
    {
        settings.ShowCpu = cpu;
        settings.ShowGpu = gpu;
        settings.ShowSsd = ssd;
        settings.ShowBoard = board;
        settings.ShowRam = ram;
        settings.ShowFan = fan;
    }
}
