namespace WOLRelay.Agent;

public sealed class AgentOptions
{
    /// <summary>Base URL of the relay, e.g. http://relay-host:8080 (no /hubs/agent suffix).</summary>
    public string RelayUrl { get; set; } = "";

    /// <summary>Shared key — must match the relay's VerySecureKey.</summary>
    public string Key { get; set; } = "";

    /// <summary>How often to send a heartbeat to refresh last-seen on the relay.</summary>
    public int HeartbeatSeconds { get; set; } = 30;

    /// <summary>When true, log the shutdown command instead of executing it.</summary>
    public bool DryRun { get; set; } = false;

    /// <summary>When false, a Restart command is logged and ignored.</summary>
    public bool AllowRestart { get; set; } = true;
}
