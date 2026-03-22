param(
    [string]$OutputRoot = "dist"
)

$ErrorActionPreference = "Stop"

function Read-RequiredValue {
    param([string]$Prompt, [string]$Default = "")
    while ($true) {
        $label = if ([string]::IsNullOrWhiteSpace($Default)) { $Prompt } else { "$Prompt [$Default]" }
        $value = Read-Host $label
        if ([string]::IsNullOrWhiteSpace($value)) { $value = $Default }
        if (-not [string]::IsNullOrWhiteSpace($value)) { return $value.Trim() }
        Write-Host "Valor obligatorio." -ForegroundColor Yellow
    }
}

function Read-OptionalValue {
    param([string]$Prompt, [string]$Default = "")
    $label = if ([string]::IsNullOrWhiteSpace($Default)) { $Prompt } else { "$Prompt [$Default]" }
    $value = Read-Host $label
    if ([string]::IsNullOrWhiteSpace($value)) { return $Default }
    return $value.Trim()
}

function Write-JsonFile {
    param([string]$Path, [object]$Data)
    [System.IO.File]::WriteAllText($Path, (($Data | ConvertTo-Json -Depth 20) + [Environment]::NewLine), [System.Text.UTF8Encoding]::new($false))
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$agentSource = Join-Path $repoRoot (Join-Path $OutputRoot "CentralMonitoring.Agent")
if (-not (Test-Path $agentSource)) { throw "No existe publish listo. Ejecuta primero scripts/publish-agent-win.ps1." }

$agentTarget = Read-RequiredValue "Ruta final Agent" "C:\CentralMonitoring\Agent"
$apiBaseUrl = Read-RequiredValue "ApiBaseUrl de la central" "http://localhost:5188"
$apiKey = Read-RequiredValue "ApiKey emitida por la central"
$hostId = Read-RequiredValue "HostId emitido por la central"
$flushInterval = [int](Read-RequiredValue "FlushIntervalSeconds" "15")
$requestTimeout = [int](Read-RequiredValue "RequestTimeoutSeconds" "10")
$bufferFilePath = Read-RequiredValue "BufferFilePath" "buffer/agent-buffer.json"
$maxBufferedBatches = [int](Read-RequiredValue "MaxBufferedBatches" "500")
$topProcessesCount = [int](Read-RequiredValue "TopProcessesCount" "5")
$criticalServicesRaw = Read-OptionalValue "Servicios Windows criticos separados por coma (opcional)"
$criticalWindowsServices = @()
if (-not [string]::IsNullOrWhiteSpace($criticalServicesRaw)) {
    $criticalWindowsServices = @($criticalServicesRaw.Split(",") | ForEach-Object { $_.Trim() } | Where-Object { $_ })
}

New-Item -ItemType Directory -Force -Path $agentTarget | Out-Null
Copy-Item "$agentSource\*" $agentTarget -Recurse -Force

$cfg = Get-Content (Join-Path $repoRoot "CentralMonitoring.Agent/appsettings.Production.example.json") -Raw | ConvertFrom-Json
$cfg.Agent.ApiBaseUrl = $apiBaseUrl
$cfg.Agent.ApiKey = $apiKey
$cfg.Agent.HostId = $hostId
$cfg.Agent.FlushIntervalSeconds = $flushInterval
$cfg.Agent.RequestTimeoutSeconds = $requestTimeout
$cfg.Agent.BufferFilePath = $bufferFilePath
$cfg.Agent.MaxBufferedBatches = $maxBufferedBatches
$cfg.Agent.TopProcessesCount = $topProcessesCount
$cfg.Agent.CriticalWindowsServices = @($criticalWindowsServices)
$cfg.Agent.CriticalLinuxSystemdUnits = @()
Write-JsonFile -Path (Join-Path $agentTarget "appsettings.Production.json") -Data $cfg

Write-Host "Agent instalado en ruta final." -ForegroundColor Green
