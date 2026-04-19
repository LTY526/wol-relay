# WOL Relay

A lightweight HTTP service for sending Wake-on-LAN (WOL) magic packets to wake up computers on your network remotely.

## Features

- Simple HTTP API for triggering Wake-on-LAN packets
- Password protection for security
- Native AOT compilation for minimal memory footprint and fast startup
- Docker support for easy deployment
- Support for various MAC address formats (with or without colons/hyphens)

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

Run the container:
```bash
docker run -d \
  -p 8080:8080 \
  -e VerySecureKey="your-secure-password" \
  --name wolrelay \
  wolrelay
```

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
