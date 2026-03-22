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

$repoRoot = Split-Path -Parent $PSScriptRoot
$apiSource = Join-Path $repoRoot (Join-Path $OutputRoot "CentralMonitoring.Api")
$workerSource = Join-Path $repoRoot (Join-Path $OutputRoot "CentralMonitoring.Worker")
$uiDistRoot = Join-Path $repoRoot "ui/dist"
$uiSourceIndex = Get-ChildItem -Path $uiDistRoot -Recurse -Filter index.html -ErrorAction SilentlyContinue | Select-Object -First 1

if (-not (Test-Path $apiSource) -or -not (Test-Path $workerSource) -or $null -eq $uiSourceIndex) {
    throw "No existe publish listo. Ejecuta primero scripts/publish-central-win.ps1."
}

$uiSource = Split-Path -Parent $uiSourceIndex.FullName
$apiTarget = Read-RequiredValue "Ruta instalada API" "C:\CentralMonitoring\Api"
$workerTarget = Read-RequiredValue "Ruta instalada Worker" "C:\CentralMonitoring\Worker"
$uiTarget = Read-RequiredValue "Ruta instalada UI" "C:\CentralMonitoring\Ui"

$apiCfg = if (Test-Path (Join-Path $apiTarget "appsettings.Production.json")) { Get-Content (Join-Path $apiTarget "appsettings.Production.json") -Raw } else { $null }
$workerCfg = if (Test-Path (Join-Path $workerTarget "appsettings.Production.json")) { Get-Content (Join-Path $workerTarget "appsettings.Production.json") -Raw } else { $null }

Remove-Item (Join-Path $apiTarget "*") -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $workerTarget "*") -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $uiTarget "*") -Recurse -Force -ErrorAction SilentlyContinue

Copy-Item "$apiSource\*" $apiTarget -Recurse -Force
Copy-Item "$workerSource\*" $workerTarget -Recurse -Force
Copy-Item "$uiSource\*" $uiTarget -Recurse -Force

if ($null -ne $apiCfg) { [System.IO.File]::WriteAllText((Join-Path $apiTarget "appsettings.Production.json"), $apiCfg, [System.Text.UTF8Encoding]::new($false)) }
if ($null -ne $workerCfg) { [System.IO.File]::WriteAllText((Join-Path $workerTarget "appsettings.Production.json"), $workerCfg, [System.Text.UTF8Encoding]::new($false)) }

Write-Host "Central actualizada. Reinicia servicios y valida /health." -ForegroundColor Green
