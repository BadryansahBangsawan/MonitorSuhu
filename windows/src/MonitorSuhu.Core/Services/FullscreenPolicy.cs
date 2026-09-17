using MonitorSuhu.Core.Models;

namespace MonitorSuhu.Core.Services;

public static class FullscreenPolicy
{
    public static bool ShouldHide(AppSettings settings, bool fullscreen, bool capturing) =>
        (settings.HideInFullscreen && fullscreen) || (settings.HideDuringCapture && capturing);

    public static bool RectCoversMonitor(
        int left, int top, int right, int bottom,
        int monitorLeft, int monitorTop, int monitorRight, int monitorBottom,
        int tolerance = 2) =>
        Math.Abs(left - monitorLeft) <= tolerance
        && Math.Abs(top - monitorTop) <= tolerance
        && Math.Abs(right - monitorRight) <= tolerance
        && Math.Abs(bottom - monitorBottom) <= tolerance;

    public static bool CoversScreen(double width, double height, double screenWidth, double screenHeight, double scale)
    {
        static bool Near(double a, double b) => Math.Abs(a - b) < 4;
        return (Near(width, screenWidth) && Near(height, screenHeight))
               || (Near(width, screenWidth * scale) && Near(height, screenHeight * scale));
    }

    public static bool IsCaptureOwner(string? name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return name.Contains("screencapture", StringComparison.OrdinalIgnoreCase);
    }
}
