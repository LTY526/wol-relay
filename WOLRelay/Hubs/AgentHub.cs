using Microsoft.AspNetCore.SignalR;
using WOLRelay.Services;
using WOLRelay.Shared;

namespace WOLRelay.Hubs;

/// <summary>
/// Hub that participating PCs connect to. Plain <see cref="Hub"/> (not Hub&lt;T&gt;) so
/// the relay stays Native AOT compatible. Agents authenticate with the shared key as a
/// query string parameter on the connection, then call <see cref="Register"/>.
/// </summary>
public class AgentHub : Hub
{
    private readonly AgentRegistry _registry;
    private readonly string? _appPassword;

    public AgentHub(AgentRegistry registry, IConfiguration configuration)
    {
        _registry = registry;
        _appPassword = configuration.GetValue<string>("VerySecureKey");
    }

    public override Task OnConnectedAsync()
    {
        var key = Context.GetHttpContext()?.Request.Query["key"].ToString();

        if (_appPassword != key)
        {
            Context.Abort();
        }

        return base.OnConnectedAsync();
    }

    public void Register(AgentRegistration registration)
    {
        _registry.Upsert(Context.ConnectionId, registration, DateTimeOffset.UtcNow);
    }

    public void Heartbeat()
    {
        _registry.Touch(Context.ConnectionId, DateTimeOffset.UtcNow);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _registry.MarkOffline(Context.ConnectionId, DateTimeOffset.UtcNow);
        return base.OnDisconnectedAsync(exception);
    }
}
