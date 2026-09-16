using System.Diagnostics;
using MonitorSuhu.Core;

namespace MonitorSuhu.App.Services;

public static class UpdateInstaller
{
    public static void LaunchWindows(string setupPath)
    {
        var name = System.IO.Path.GetFileName(setupPath);
        if (!ReleaseAssets.Matches(name, "windows") || !System.IO.File.Exists(setupPath))
            throw new InvalidOperationException("Update file is not a MonitorSuhu installer.");

        var quoted = setupPath.Replace("\"", "\\\"");
        var started = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c ping 127.0.0.1 -n 2 >nul & start \"\" \"{quoted}\" /VERYSILENT /NORESTART /CLOSEAPPLICATIONS /SUPPRESSMSGBOXES",
            UseShellExecute = false,
            CreateNoWindow = true
        });
        if (started is null)
            throw new InvalidOperationException("Could not start the installer.");
    }
}
