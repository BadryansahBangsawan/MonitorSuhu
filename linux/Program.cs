using System.Runtime.InteropServices;
using Avalonia;

namespace MonitorSuhu.Linux;

internal static class Program
{
    private const int O_RDWR = 2;
    private const int O_CREAT = 64;
    private const int S_IRUSR = 0x100;
    private const int S_IWUSR = 0x080;
    private const int LockEx = 2;
    private const int LockNb = 4;

    // Held open so flock lasts for the process lifetime.
    private static int _lockFd = -1;

    [STAThread]
    public static void Main(string[] args)
    {
        if (!TryAcquireInstanceLock())
            return;

        BuildAvaloniaApp()
            .UsePlatformDetect()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>().UsePlatformDetect();

    private static bool TryAcquireInstanceLock()
    {
        var runtime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        string lockPath;
        if (!string.IsNullOrEmpty(runtime))
        {
            lockPath = Path.Combine(runtime, "monitorsuhu.lock");
        }
        else
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".cache",
                "MonitorSuhu");
            try
            {
                Directory.CreateDirectory(dir);
            }
            catch (IOException)
            {
                return false;
            }

            lockPath = Path.Combine(dir, "monitorsuhu.lock");
        }

        var fd = Open(lockPath, O_RDWR | O_CREAT, S_IRUSR | S_IWUSR);
        if (fd < 0)
            return false;

        if (Flock(fd, LockEx | LockNb) != 0)
        {
            Close(fd);
            return false;
        }

        _lockFd = fd;
        return true;
    }

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int Open([MarshalAs(UnmanagedType.LPUTF8Str)] string pathname, int flags, int mode);

    [DllImport("libc", EntryPoint = "flock", SetLastError = true)]
    private static extern int Flock(int fd, int operation);

    [DllImport("libc", EntryPoint = "close", SetLastError = true)]
    private static extern int Close(int fd);
}
