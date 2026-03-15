param(
    [string]$ServiceName = "CentralMonitoring.Agent",
    [string]$DisplayName = "Central Monitoring Agent",
    [string]$Description = "Agente de metricas para CentralMonitoring",
    [string]$PublishPath = ".\publish"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $PublishPath)) {
    Write-Host "Publicando agente en $PublishPath ..."
    dotnet publish ..\CentralMonitoring.Agent.csproj -c Release -o $PublishPath
}

$exePath = (Resolve-Path (Join-Path $PublishPath "CentralMonitoring.Agent.exe")).Path

if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    Write-Host "El servicio $ServiceName ya existe. Reiniciando configuracion..."
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

sc.exe create $ServiceName binPath= "`"$exePath`"" start= auto DisplayName= "`"$DisplayName`"" | Out-Null
sc.exe description $ServiceName "$Description" | Out-Null
sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/5000/restart/5000 | Out-Null

Start-Service -Name $ServiceName
Write-Host "Servicio $ServiceName instalado e iniciado."
