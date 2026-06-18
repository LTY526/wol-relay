using WOLRelay.Agent;

var switchMappings = new Dictionary<string, string>
{
    ["-r"] = "RelayUrl",
    ["--relay"] = "RelayUrl",
    ["-k"] = "Key",
    ["--key"] = "Key",
};

var builder = Host.CreateApplicationBuilder(args);

// Re-add the command-line source with switch mappings (and so it wins as the last layer).
builder.Configuration.AddCommandLine(args, switchMappings);

builder.Services.AddWindowsService(options => options.ServiceName = "WOLRelay Agent");

var options = new AgentOptions();
builder.Configuration.Bind(options);
builder.Services.AddSingleton(options);

builder.Services.AddSingleton<ShutdownExecutor>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
