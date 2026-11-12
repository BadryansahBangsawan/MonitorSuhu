namespace MonitorSuhu.Linux.Services;

public static class AutostartService
{
    public static string? Apply(bool enabled, string exePath)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "autostart");
            var file = Path.Combine(dir, "monitorsuhu.desktop");

            if (!enabled)
            {
                if (File.Exists(file))
                    File.Delete(file);
                return null;
            }

            var exec = string.IsNullOrWhiteSpace(exePath) ? Environment.ProcessPath : exePath;
            if (string.IsNullOrWhiteSpace(exec))
                return "Could not update Start with Linux: missing executable path.";

            Directory.CreateDirectory(dir);
            var quoted = "\"" + exec.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
            File.WriteAllText(file,
                "[Desktop Entry]\n" +
                "Type=Application\n" +
                "Name=Bara\n" +
                "Exec=" + quoted + "\n");
            return null;
        }
        catch (Exception ex)
        {
            return $"Could not update Start with Linux: {ex.Message}";
        }
    }
}
