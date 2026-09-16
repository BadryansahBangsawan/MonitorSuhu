using Microsoft.Win32.TaskScheduler;

namespace MonitorSuhu.Core.Services;

public static class AutostartService
{
    private const string TaskName = "MonitorSuhu";

    public static void Apply(bool enabled, string exePath)
    {
        using var service = new TaskService();
        service.RootFolder.DeleteTask(TaskName, exceptionOnNotExists: false);
        if (!enabled) return;

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
    }
}
