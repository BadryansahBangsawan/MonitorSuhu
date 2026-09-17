using System.Diagnostics;
using MonitorSuhu.Core;

namespace MonitorSuhu.Linux.Services;

public static class UpdateInstaller
{
    public static string DestinationDir()
    {
        var exe = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exe))
        {
            var dir = Path.GetDirectoryName(exe);
            if (!string.IsNullOrEmpty(dir)) return dir;
        }
        return AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
    }

    public static void ApplyTarball(string tarPath)
    {
        var name = Path.GetFileName(tarPath);
        if (!ReleaseAssets.Matches(name, "linux") || !File.Exists(tarPath))
            throw new InvalidOperationException("Update file is not a MonitorSuhu Linux package.");

        var dest = DestinationDir();
        Directory.CreateDirectory(dest);
        var pid = Environment.ProcessId;
        var script = Path.Combine(Path.GetTempPath(), $"MonitorSuhu-replace-{pid}.sh");
        var quotedTar = ShQuote(tarPath);
        var quotedDest = ShQuote(dest);
        var quotedScript = ShQuote(script);
        var quotedBin = ShQuote(Path.Combine(dest, "MonitorSuhu"));
        var body = $"""
            #!/bin/bash
            set -euo pipefail
            trap '' HUP
            while /bin/kill -0 {pid} 2>/dev/null; do
              /bin/sleep 0.2
            done
            /usr/bin/tar -xzf {quotedTar} -C {quotedDest}
            /bin/chmod +x {quotedBin}
            nohup {quotedBin} >/dev/null 2>&1 &
            /bin/rm -f {quotedTar} {quotedScript}
            """;
        File.WriteAllText(script, body);
        if (OperatingSystem.IsLinux())
            File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var started = Process.Start(new ProcessStartInfo
        {
            FileName = "/usr/bin/nohup",
            ArgumentList = { "/bin/bash", script },
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });
        if (started is null)
            throw new InvalidOperationException("Could not start the installer.");
    }

    private static string ShQuote(string value) =>
        "'" + value.Replace("'", "'\\''") + "'";
}
