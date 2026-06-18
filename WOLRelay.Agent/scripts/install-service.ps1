<#
.SYNOPSIS
    Installs the WOLRelay Agent as a Windows Service that starts on boot.

.DESCRIPTION
    Copies the published agent into a stable install directory, then registers it as a
    LocalSystem service (which has the privilege required to shut the machine down).
    The relay URL and key are baked into the service's binary path as command-line
    arguments. Because the files are copied, the folder you published to can be deleted
    afterwards.

.EXAMPLE
    .\install-service.ps1 -ExePath "C:\publish\WOLRelay.Agent.exe" `
        -RelayUrl "http://relay-host:8080" -Key "your-secure-key"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $ExePath,
    [Parameter(Mandatory = $true)] [string] $RelayUrl,
    [Parameter(Mandatory = $true)] [string] $Key,
    [string] $ServiceName = "WOLRelayAgent",
    [string] $InstallDir = "$env:ProgramFiles\WOLRelayAgent",
    [switch] $DryRun
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $ExePath)) {
    throw "Executable not found: $ExePath. Publish the agent first (dotnet publish -c Release ...)."
}

$sourceExe = (Resolve-Path $ExePath).Path
$sourceDir = Split-Path -Parent $sourceExe
$exeName = Split-Path -Leaf $sourceExe

# Remove any existing service first so the install dir isn't locked while we copy.
if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    Write-Host "Service '$ServiceName' already exists. Removing it first..."
    sc.exe stop $ServiceName | Out-Null
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

# Copy the published output into the stable install directory.
Write-Host "Installing files to '$InstallDir'..."
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
Copy-Item -Path (Join-Path $sourceDir "*") -Destination $InstallDir -Recurse -Force

$targetExe = Join-Path $InstallDir $exeName
$dryRunArg = if ($DryRun) { " --DryRun=true" } else { "" }

# sc.exe requires the whole binPath (exe + args) as a single quoted token, with a
# space after binPath=. Inner quotes are escaped with a backslash.
$binPath = "\`"$targetExe\`" --RelayUrl=$RelayUrl --Key=$Key$dryRunArg"

Write-Host "Creating service '$ServiceName'..."
sc.exe create $ServiceName binPath= $binPath start= auto obj= LocalSystem | Out-Null
sc.exe description $ServiceName "WOLRelay remote power agent" | Out-Null

Write-Host "Starting service '$ServiceName'..."
sc.exe start $ServiceName | Out-Null

Write-Host "Done. Installed to '$targetExe'. Check status with: Get-Service $ServiceName"
