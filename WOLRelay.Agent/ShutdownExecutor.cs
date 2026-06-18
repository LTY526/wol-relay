using System.Diagnostics;
using WOLRelay.Shared;

namespace WOLRelay.Agent;

public sealed class ShutdownExecutor
{
    private readonly AgentOptions _options;
    private readonly ILogger<ShutdownExecutor> _logger;

    public ShutdownExecutor(AgentOptions options, ILogger<ShutdownExecutor> logger)
    {
        _options = options;
        _logger = logger;
    }

    public void Execute(ShutdownCommand command)
    {
        if (command.Mode == ShutdownMode.Restart && !_options.AllowRestart)
        {
            _logger.LogWarning("Restart command received but AllowRestart is disabled — ignoring.");
            return;
        }

        if (command.Mode == ShutdownMode.Sleep && !_options.AllowSleep)
        {
            _logger.LogWarning("Sleep command received but AllowSleep is disabled — ignoring.");
            return;
        }

        if (command.Mode == ShutdownMode.Sleep)
        {
            ExecuteSleep();
            return;
        }

        var delay = Math.Max(0, command.DelaySeconds);
        var flag = command.Mode == ShutdownMode.Restart ? "/r" : "/s";
        var comment = string.IsNullOrWhiteSpace(command.Reason) ? "WOLRelay remote command" : command.Reason;

        // /t <delay> sets the timeout; /c provides a comment shown to the user.
        var arguments = $"{flag} /t {delay} /c \"{comment}\"";

        Run("shutdown.exe", arguments);
    }

    private void ExecuteSleep()
    {
        // Windows has no shutdown.exe sleep flag. SetSuspendState puts the machine to sleep:
        // args are bHibernate, bForceCritical, bWakeupEventsDisabled. 0,1,0 => sleep, forced, wake enabled.
        // NOTE: if hibernation is enabled system-wide, Windows may hibernate instead of sleeping.
        Run("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0");
    }

    private void Run(string fileName, string arguments)
    {
        if (_options.DryRun)
        {
            _logger.LogInformation("[DryRun] Would run: {FileName} {Arguments}", fileName, arguments);
            return;
        }

        _logger.LogInformation("Executing: {FileName} {Arguments}", fileName, arguments);

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try
        {
            using var process = Process.Start(psi);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch {FileName}", fileName);
        }
    }
}
