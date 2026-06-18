<#
.SYNOPSIS
    Runs the WOLRelay Agent in the foreground (for testing / non-service use).

.EXAMPLE
    .\run.ps1 -RelayUrl "http://localhost:5001" -Key "your-secure-key" -DryRun
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $RelayUrl,
    [Parameter(Mandatory = $true)] [string] $Key,
    [switch] $DryRun,
    [string] $ExePath
)

$ErrorActionPreference = "Stop"

$agentArgs = @("--RelayUrl=$RelayUrl", "--Key=$Key")
if ($DryRun) { $agentArgs += "--DryRun=true" }

if ($ExePath) {
    & $ExePath @agentArgs
}
else {
    # Run from source against the project.
    $projectDir = Split-Path -Parent $PSScriptRoot
    dotnet run --project $projectDir -- @agentArgs
}
