namespace MonitorSuhu.Linux.Services;

public static class AutostartService
{
    public static void Apply(bool enabled, string exePath)
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
                return;
            }

            var exec = string.IsNullOrWhiteSpace(exePath) ? Environment.ProcessPath : exePath;
            if (string.IsNullOrWhiteSpace(exec))
                return;

            Directory.CreateDirectory(dir);
            var quoted = "\"" + exec.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
            File.WriteAllText(file,
                "[Desktop Entry]\n" +
                "Type=Application\n" +
                "Name=MonitorSuhu\n" +
                "Exec=" + quoted + "\n");
        }
        catch
        {
            // Ignore IO errors besides a status string owned by the settings VM.
        }
    }
}
