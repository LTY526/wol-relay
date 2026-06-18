<#
.SYNOPSIS
    Stops and removes the WOLRelay Agent Windows Service.

.DESCRIPTION
    Deletes the service. Pass -RemoveFiles to also delete the install directory created
    by install-service.ps1.

.EXAMPLE
    .\uninstall-service.ps1
    .\uninstall-service.ps1 -RemoveFiles
#>
[CmdletBinding()]
param(
    [string] $ServiceName = "WOLRelayAgent",
    [string] $InstallDir = "$env:ProgramFiles\WOLRelayAgent",
    [switch] $RemoveFiles
)

$ErrorActionPreference = "Stop"

if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    Write-Host "Stopping service '$ServiceName'..."
    sc.exe stop $ServiceName | Out-Null
    Start-Sleep -Seconds 2

    Write-Host "Deleting service '$ServiceName'..."
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 1
}
else {
    Write-Host "Service '$ServiceName' is not installed."
}

if ($RemoveFiles -and (Test-Path $InstallDir)) {
    Write-Host "Removing install directory '$InstallDir'..."
    Remove-Item -Path $InstallDir -Recurse -Force
}

Write-Host "Done."
