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
$agentSource = Join-Path $repoRoot (Join-Path $OutputRoot "CentralMonitoring.Agent")
if (-not (Test-Path $agentSource)) { throw "No existe publish listo. Ejecuta primero scripts/publish-agent-win.ps1." }

$agentTarget = Read-RequiredValue "Ruta instalada Agent" "C:\CentralMonitoring\Agent"
$cfg = if (Test-Path (Join-Path $agentTarget "appsettings.Production.json")) { Get-Content (Join-Path $agentTarget "appsettings.Production.json") -Raw } else { $null }

Remove-Item (Join-Path $agentTarget "*") -Recurse -Force -ErrorAction SilentlyContinue
Copy-Item "$agentSource\*" $agentTarget -Recurse -Force
if ($null -ne $cfg) { [System.IO.File]::WriteAllText((Join-Path $agentTarget "appsettings.Production.json"), $cfg, [System.Text.UTF8Encoding]::new($false)) }

Write-Host "Agent actualizado. Reinicia el servicio." -ForegroundColor Green
