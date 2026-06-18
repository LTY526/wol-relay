using System.Text.Json;
using WOLRelay.Shared;

namespace WOLRelay.Services;

/// <summary>
/// Registry of agents the relay has ever seen, keyed by normalized MAC address (the
/// same identifier used by the wake endpoint). Connected agents are "Online"; on
/// disconnect they are kept and flipped to "Offline" so previously-seen PCs remain
/// listable. The set is persisted to a JSON file so it survives relay restarts.
/// </summary>
public class AgentRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, AgentInfo> _agents = new();
    private readonly string _storePath;

    public AgentRegistry(IConfiguration configuration)
    {
        _storePath = configuration.GetValue<string>("AgentStorePath") ?? "agents.json";
        Load();
    }

    public static string NormalizeMac(string mac) =>
        mac.Replace(":", "").Replace("-", "").ToUpperInvariant();

    public void Upsert(string connectionId, AgentRegistration registration, DateTimeOffset nowUtc)
    {
        var key = NormalizeMac(registration.MacAddress);

        lock (_gate)
        {
            _agents[key] = new AgentInfo
            {
                MacAddress = key,
                Hostname = registration.Hostname,
                ConnectionId = connectionId,
                ConnectedAtUtc = nowUtc,
                LastSeenUtc = nowUtc,
                Status = "Online",
            };
            Save();
        }
    }

    public void Touch(string connectionId, DateTimeOffset nowUtc)
    {
        // In-memory only — heartbeats are frequent and a fresh last-seen is persisted
        // on disconnect, so there's no need to write the file on every beat.
        lock (_gate)
        {
            foreach (var agent in _agents.Values)
            {
                if (agent.ConnectionId == connectionId)
                {
                    agent.LastSeenUtc = nowUtc;
                    return;
                }
            }
        }
    }

    public void MarkOffline(string connectionId, DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            foreach (var agent in _agents.Values)
            {
                if (agent.ConnectionId == connectionId)
                {
                    agent.Status = "Offline";
                    agent.ConnectionId = "";
                    agent.LastSeenUtc = nowUtc;
                    Save();
                    return;
                }
            }
        }
    }

    public bool TryGetConnectionId(string macAddress, out string connectionId)
    {
        lock (_gate)
        {
            if (_agents.TryGetValue(NormalizeMac(macAddress), out var agent)
                && agent.Status == "Online"
                && agent.ConnectionId.Length > 0)
            {
                connectionId = agent.ConnectionId;
                return true;
            }
        }

        connectionId = "";
        return false;
    }

    public bool Remove(string macAddress)
    {
        lock (_gate)
        {
            if (_agents.Remove(NormalizeMac(macAddress)))
            {
                Save();
                return true;
            }

            return false;
        }
    }

    public AgentInfo[] Snapshot()
    {
        lock (_gate)
        {
            return _agents.Values.ToArray();
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_storePath))
                return;

            var json = File.ReadAllText(_storePath);
            var loaded = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.AgentInfoArray);

            if (loaded is null)
                return;

            foreach (var agent in loaded)
            {
                // Nothing is connected at startup — everything loaded is offline.
                agent.Status = "Offline";
                agent.ConnectionId = "";
                _agents[NormalizeMac(agent.MacAddress)] = agent;
            }
        }
        catch
        {
            // A missing or corrupt store must not stop the relay from starting.
        }
    }

    private void Save()
    {
        // Caller holds _gate.
        try
        {
            var directory = Path.GetDirectoryName(_storePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(_agents.Values.ToArray(), AppJsonSerializerContext.Default.AgentInfoArray);

            // Write to a temp file then move, so a crash mid-write can't corrupt the store.
            var tmp = _storePath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, _storePath, overwrite: true);
        }
        catch
        {
            // Persistence is best-effort; never let it take down a hub callback.
        }
    }
}
