using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using WOLRelay.Hubs;
using WOLRelay.Services;
using WOLRelay.Shared;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

builder.Services.AddSignalR().AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

builder.Services.AddTransient<WakeOnLanService>();
builder.Services.AddSingleton<AgentRegistry>();

var appPassword = builder.Configuration.GetValue<string>("VerySecureKey");

var app = builder.Build();

var netApi = app.MapGroup("/net");

netApi.MapGet("wake", (WakeOnLanService service, [FromQuery] string macAddress, [FromQuery] string password = "") =>
{
    if (appPassword != password) return "Invalid request";
    service.Send(macAddress);
    return "Ok";
});

netApi.MapPost("shutdown", async (
    IHubContext<AgentHub> hub,
    AgentRegistry registry,
    [FromQuery] string macAddress,
    [FromQuery] string password = "",
    [FromQuery] string mode = "shutdown",
    [FromQuery] int delaySeconds = 0,
    [FromQuery] string reason = "") =>
{
    if (appPassword != password) return "Invalid request";

    if (!registry.TryGetConnectionId(macAddress, out var connectionId))
        return "Not connected";

    var command = new ShutdownCommand
    {
        Mode = string.Equals(mode, "restart", StringComparison.OrdinalIgnoreCase)
            ? ShutdownMode.Restart
            : ShutdownMode.Shutdown,
        DelaySeconds = delaySeconds,
        Reason = reason,
    };

    await hub.Clients.Client(connectionId).SendAsync("Shutdown", command);
    return "Ok";
});

app.MapGet("/agents", (AgentRegistry registry, [FromQuery] string password = "") =>
    appPassword != password
        ? Results.Text("Invalid request")
        : Results.Json(registry.Snapshot(), AppJsonSerializerContext.Default.AgentInfoArray));

app.MapDelete("/agents", (AgentRegistry registry, [FromQuery] string macAddress, [FromQuery] string password = "") =>
{
    if (appPassword != password) return "Invalid request";
    return registry.Remove(macAddress) ? "Ok" : "Not found";
});

app.MapHub<AgentHub>("/hubs/agent");

app.Run();

[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(AgentRegistration))]
[JsonSerializable(typeof(AgentInfo))]
[JsonSerializable(typeof(AgentInfo[]))]
[JsonSerializable(typeof(ShutdownCommand))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}
