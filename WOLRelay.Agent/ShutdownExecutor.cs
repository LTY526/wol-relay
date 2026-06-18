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

        var delay = Math.Max(0, command.DelaySeconds);
        var flag = command.Mode == ShutdownMode.Restart ? "/r" : "/s";
        var comment = string.IsNullOrWhiteSpace(command.Reason) ? "WOLRelay remote command" : command.Reason;

        // /t <delay> sets the timeout; /c provides a comment shown to the user.
        var arguments = $"{flag} /t {delay} /c \"{comment}\"";

        if (_options.DryRun)
        {
            _logger.LogInformation("[DryRun] Would run: shutdown.exe {Arguments}", arguments);
            return;
        }

        _logger.LogInformation("Executing: shutdown.exe {Arguments}", arguments);

        var psi = new ProcessStartInfo
        {
            FileName = "shutdown.exe",
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
            _logger.LogError(ex, "Failed to launch shutdown.exe");
        }
    }
}
