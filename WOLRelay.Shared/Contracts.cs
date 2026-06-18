namespace WOLRelay.Shared;

/// <summary>
/// Sent by an agent to the relay right after it connects, so the relay knows which
/// machine the connection belongs to. The MAC must match the one used to wake the
/// machine via the WOL endpoint, so wake and shutdown target the same identifier.
/// </summary>
public sealed class AgentRegistration
{
    public string MacAddress { get; set; } = "";
    public string Hostname { get; set; } = "";
}

/// <summary>
/// A registered, currently-connected agent as tracked by the relay registry.
/// </summary>
public sealed class AgentInfo
{
    public string MacAddress { get; set; } = "";
    public string Hostname { get; set; } = "";
    public string ConnectionId { get; set; } = "";
    public DateTimeOffset ConnectedAtUtc { get; set; }
    public DateTimeOffset LastSeenUtc { get; set; }
    public string Status { get; set; } = "Online";
}

public enum ShutdownMode
{
    Shutdown = 0,
    Restart = 1,
    Sleep = 2,
}

/// <summary>
/// JSON body posted to the relay's /net/shutdown endpoint to power off, restart, or sleep a machine.
/// </summary>
public sealed class ShutdownRequest
{
    public string MacAddress { get; set; } = "";
    public string Password { get; set; } = "";
    public string Mode { get; set; } = "shutdown";
    public int DelaySeconds { get; set; }
    public string Reason { get; set; } = "";
}

/// <summary>
/// Pushed from the relay down the agent's existing SignalR connection to power it off.
/// </summary>
public sealed class ShutdownCommand
{
    public ShutdownMode Mode { get; set; } = ShutdownMode.Shutdown;
    public int DelaySeconds { get; set; }
    public string Reason { get; set; } = "";
}
