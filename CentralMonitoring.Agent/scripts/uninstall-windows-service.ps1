param(
    [string]$ServiceName = "CentralMonitoring.Agent"
)

$ErrorActionPreference = "Stop"

if (-not (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue)) {
    Write-Host "El servicio $ServiceName no existe."
    exit 0
}

Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
sc.exe delete $ServiceName | Out-Null

Write-Host "Servicio $ServiceName desinstalado."
