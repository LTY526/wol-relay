# WOL Relay

A lightweight HTTP service for sending Wake-on-LAN (WOL) magic packets to wake up computers on your network remotely.

## Features

- Simple HTTP API for triggering Wake-on-LAN packets
- **Remote shutdown / restart** of participating PCs via a lightweight agent
- **Live presence** — list which PCs are currently connected/online
- Password protection for security
- Native AOT compilation for minimal memory footprint and fast startup
- Docker support for easy deployment
- Support for various MAC address formats (with or without colons/hyphens)

## Components

| Project          | Role                                                                            |
| ---------------- | ------------------------------------------------------------------------------- |
| `WOLRelay`       | Main service. HTTP API + SignalR hub. Sends WOL packets and pushes shutdowns.   |
| `WOLRelay.Agent` | Windows Service installed on each PC. Connects out to the relay and obeys it.   |
| `WOLRelay.Shared`| Message contracts shared by the relay and agent.                                |

Wake-on-LAN can *start* a PC but cannot *stop* one — shutdown must run on the target
itself. Each participating PC runs `WOLRelay.Agent`, which opens an outbound,
persistent SignalR connection to the relay (no inbound firewall rule needed, survives
DHCP IP changes). To power a PC off, the relay pushes a command down that existing
connection.

## Prerequisites

- .NET 10.0 SDK (for building from source)
- Docker (for containerized deployment)

## Configuration

The service uses a password for authentication. Set the password in `appsettings.json`:

```json
{
  "VerySecureKey": "your-secure-password-here"
}
```

Or use environment variables:
```bash
export VerySecureKey="your-secure-password-here"
```

### Persistence

The relay remembers every agent it has seen (online and offline) in a JSON file. The
path is configured with `AgentStorePath`:

- **Local default:** `agents.json` in the working directory.
- **Docker default:** `/data/agents.json` (set via `ENV` in the image). Mount a host
  directory or named volume at `/data` so the list survives container restarts — see
  [Using Docker](#using-docker).

Override it anywhere with the `AgentStorePath` setting (config key or env var).

## API Usage

### Wake a Computer

Send a GET request to wake a computer by its MAC address:

```bash
GET /net/wake?macAddress=AA:BB:CC:DD:EE:FF&password=your-secure-password
```

**Parameters:**
- `macAddress` - The MAC address of the target computer (supports formats with/without colons or hyphens)
- `password` - The configured security password

**Example:**
```bash
curl "http://localhost:8080/net/wake?macAddress=AA:BB:CC:DD:EE:FF&password=your-secure-password"
```

**Response:**
- `"Ok"` - Magic packet sent successfully
- `"Invalid request"` - Incorrect password

### List PCs

Returns every agent the relay has ever seen. Currently-connected PCs have
`status: "Online"`; ones that have disconnected are kept with `status: "Offline"` and
their `lastSeenUtc`, so you can see — and wake — PCs that aren't connected right now.

```bash
GET /agents?password=your-secure-password
```

**Response:** JSON array of `{ macAddress, hostname, connectionId, connectedAtUtc, lastSeenUtc, status }`.

The list is persisted to disk (see [Persistence](#persistence)) so it survives relay
restarts. Once seen, a PC stays in the list until you remove it:

```bash
DELETE /agents?macAddress=AA:BB:CC:DD:EE:FF&password=your-secure-password
```

Returns `"Ok"`, `"Not found"`, or `"Invalid request"`.

### Shut Down / Restart a Computer

Pushes a power command to a connected agent. The target must have the agent running
and connected (check `/agents` first).

```bash
POST /net/shutdown?macAddress=AA:BB:CC:DD:EE:FF&password=your-secure-password&mode=shutdown&delaySeconds=0&reason=
```

**Parameters:**
- `macAddress` - Target MAC (same identifier used to wake it)
- `password` - The configured security password
- `mode` - `shutdown` (default) or `restart`
- `delaySeconds` - Countdown before the command runs (default `0`); cancel on the PC with `shutdown /a`
- `reason` - Optional comment shown to the user

**Response:**
- `"Ok"` - Command pushed to the agent
- `"Not connected"` - No agent with that MAC is currently connected
- `"Invalid request"` - Incorrect password

## The Agent (participating PCs)

Install `WOLRelay.Agent` on each Windows PC you want to control. It registers itself
with the relay (reporting hostname + MAC) and waits for shutdown commands.

### Publish

The agent is a standalone Windows executable (not a container). Publish it
**self-contained** so target PCs don't need the .NET runtime installed:

```bash
dotnet publish WOLRelay.Agent/WOLRelay.Agent.csproj -c Release -r win-x64 \
  --self-contained -p:PublishSingleFile=true -o C:\WOLRelayAgent
```

This produces a single `C:\WOLRelayAgent\WOLRelay.Agent.exe`. Copy that folder to each
PC. (Drop `--self-contained -p:PublishSingleFile=true` for a smaller, framework-dependent
build if the PCs already have the .NET 10 runtime.)

### Configuration

Settings come from `appsettings.json`, environment variables, then **command-line
arguments** (last wins). Per-machine setup is easiest via CLI args:

```bash
WOLRelay.Agent.exe --RelayUrl=http://relay-host:8080 --Key=your-secure-password
```

| Setting            | CLI                    | Default | Meaning                                            |
| ------------------ | ---------------------- | ------- | -------------------------------------------------- |
| `RelayUrl`         | `--RelayUrl` / `-r`    | —       | Relay base URL (no `/hubs/agent` suffix)           |
| `Key`              | `--Key` / `-k`         | —       | Must match the relay's `VerySecureKey`             |
| `HeartbeatSeconds` | `--HeartbeatSeconds`   | `30`    | Last-seen refresh interval                         |
| `DryRun`           | `--DryRun`             | `false` | Log the shutdown command instead of executing it   |
| `AllowRestart`     | `--AllowRestart`       | `true`  | Whether `mode=restart` is honored                  |

### Install as a Windows Service (starts on boot)

Run from `WOLRelay.Agent/scripts/` in an **elevated** PowerShell. The installer copies
the published files into a stable install directory (default
`C:\Program Files\WOLRelayAgent`), then registers the service to run from there — so the
folder you published to can be deleted afterwards. The service runs as LocalSystem
(which has shutdown privilege) and the relay URL/key are baked into the service
definition:

```powershell
.\install-service.ps1 -ExePath "C:\publish\WOLRelay.Agent.exe" `
    -RelayUrl "http://relay-host:8080" -Key "your-secure-password"

# Optional: -InstallDir "D:\Apps\WOLRelayAgent" to change the location.

# Remove later (add -RemoveFiles to also delete the install directory):
.\uninstall-service.ps1
```

To test in the foreground without installing a service, use `run.ps1` (supports
`-DryRun`), or run at logon via Task Scheduler / a Startup-folder shortcut instead of a
service.

## Running Locally

### Using .NET CLI

```bash
cd WOLRelay
dotnet run
```

The service will start on `http://localhost:5000` (or the port specified in `launchSettings.json`).

### Using Docker

Build the Docker image:
```bash
docker build -t wolrelay .
```

Run the container (mount a volume at `/data` to persist the known-PC list):
```bash
docker run -d \
  -p 8080:8080 \
  -e VerySecureKey="your-secure-password" \
  -v wolrelay-data:/data \
  --name wolrelay \
  wolrelay
```

The agent list is written to `/data/agents.json`. Use a named volume (as above) or a
host path (`-v /path/on/host:/data`) so it survives container restarts and is directly
accessible. To store it elsewhere, override `-e AgentStorePath=/somewhere/agents.json`.

## Building for Production

### Native AOT Build

For optimal performance and minimal memory usage:

```bash
cd WOLRelay
dotnet publish -c Release -o ./publish
```

The compiled binary will be in the `./publish` directory.

### Docker Build

The included Dockerfile uses multi-stage builds with AOT compilation for a minimal runtime image:

```bash
docker build -t wolrelay:latest .
```

## How It Works

The service sends Wake-on-LAN "magic packets" to wake up computers on your network. A magic packet consists of:
1. 6 bytes of `0xFF`
2. The target MAC address repeated 16 times

The packet is broadcast over UDP to port 9 (standard WOL port) on the broadcast address (255.255.255.255).

## Security Considerations

- **Always use a strong password** - The password protects the API from unauthorized use
- **Consider network isolation** - Run the service only on trusted networks
- **Use HTTPS in production** - Configure a reverse proxy with SSL/TLS for secure communication
- **Limit access** - Use firewall rules to restrict access to the service

## Requirements for Target Computers

For Wake-on-LAN to work, the target computer must:
1. Have Wake-on-LAN enabled in BIOS/UEFI settings
2. Have a network card that supports WOL
3. Be connected via Ethernet (Wi-Fi WOL support varies)
4. Be on the same network segment (or have proper routing configured)
