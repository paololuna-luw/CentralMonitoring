param(
    [ValidateSet("win-x64", "linux-x64")]
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$OutputRoot = "dist"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$agentOutput = Join-Path $repoRoot (Join-Path $OutputRoot "CentralMonitoring.Agent")

Write-Host "Publishing Agent to $agentOutput"
dotnet publish (Join-Path $repoRoot "CentralMonitoring.Agent/CentralMonitoring.Agent.csproj") `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -o $agentOutput

Write-Host "Agent publish completed."
