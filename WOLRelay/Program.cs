using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using WOLRelay.Services;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

builder.Services.AddTransient<WakeOnLanService>();

var appPassword = builder.Configuration.GetValue<string>("VerySecureKey");

var app = builder.Build();

var netApi = app.MapGroup("/net");

netApi.MapGet("wake", (WakeOnLanService service, [FromQuery] string macAddress, [FromQuery] string password = "") =>
{
    if (appPassword != password) return "Invalid request";
    service.Send(macAddress);
    return "Ok";
});

app.Run();

[JsonSerializable(typeof(string))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}
