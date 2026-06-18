using Microsoft.AspNetCore.SignalR.Client;
using WOLRelay.Shared;

namespace WOLRelay.Agent;

public sealed class Worker : BackgroundService
{
    private readonly AgentOptions _options;
    private readonly ShutdownExecutor _shutdownExecutor;
    private readonly ILogger<Worker> _logger;
    private HubConnection? _connection;

    public Worker(AgentOptions options, ShutdownExecutor shutdownExecutor, ILogger<Worker> logger)
    {
        _options = options;
        _shutdownExecutor = shutdownExecutor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.RelayUrl))
        {
            _logger.LogError("RelayUrl is not configured. Set it in appsettings.json or pass --RelayUrl=... on the command line.");
            return;
        }

        var hubUrl = $"{_options.RelayUrl.TrimEnd('/')}/hubs/agent?key={Uri.EscapeDataString(_options.Key)}";

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _connection.On<ShutdownCommand>("Shutdown", command =>
        {
            _logger.LogInformation("Received {Mode} command (delay {Delay}s): {Reason}",
                command.Mode, command.DelaySeconds, command.Reason);
            _shutdownExecutor.Execute(command);
        });

        // Re-register after an automatic reconnect — the relay drops registration on disconnect.
        _connection.Reconnected += async _ =>
        {
            _logger.LogInformation("Reconnected to relay. Re-registering.");
            await RegisterAsync();
        };

        await ConnectWithRetryAsync(stoppingToken);

        // Heartbeat loop keeps last-seen fresh and keeps the connection warm.
        var heartbeat = TimeSpan.FromSeconds(Math.Max(5, _options.HeartbeatSeconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(heartbeat, stoppingToken);
                if (_connection.State == HubConnectionState.Connected)
                    await _connection.InvokeAsync("Heartbeat", stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Heartbeat failed.");
            }
        }
    }

    private async Task ConnectWithRetryAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _connection!.StartAsync(stoppingToken);
                _logger.LogInformation("Connected to relay at {Url}.", _options.RelayUrl);
                await RegisterAsync();
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not connect to relay; retrying in 5s.");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private async Task RegisterAsync()
    {
        var registration = MachineInfo.Current();
        _logger.LogInformation("Registering as {Host} ({Mac}).", registration.Hostname, registration.MacAddress);
        await _connection!.InvokeAsync("Register", registration);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
            await _connection.DisposeAsync();

        await base.StopAsync(cancellationToken);
    }
}
