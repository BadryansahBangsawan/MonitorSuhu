using Microsoft.Win32.TaskScheduler;

namespace MonitorSuhu.App.Services;

public static class AutostartService
{
    private const string TaskName = "MonitorSuhu";

    public static string? Apply(bool enabled, string exePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(exePath))
                return "Could not update Start with Windows: missing executable path.";

            using var service = new TaskService();
            service.RootFolder.DeleteTask(TaskName, exceptionOnNotExists: false);
            if (!enabled) return null;

            var definition = service.NewTask();
            definition.RegistrationInfo.Description = "Start MonitorSuhu overlay at logon";
            definition.Principal.RunLevel = TaskRunLevel.Highest;
            definition.Principal.LogonType = TaskLogonType.InteractiveToken;
            definition.Triggers.Add(new LogonTrigger());
            definition.Actions.Add(new ExecAction(exePath));
            definition.Settings.StopIfGoingOnBatteries = false;
            definition.Settings.DisallowStartIfOnBatteries = false;
            definition.Settings.ExecutionTimeLimit = TimeSpan.Zero;
            service.RootFolder.RegisterTaskDefinition(TaskName, definition);
            return null;
        }
        catch (Exception ex)
        {
            return $"Could not update Start with Windows: {ex.Message}";
        }
    }
}
