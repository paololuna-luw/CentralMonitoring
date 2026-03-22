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

function Read-YesNo {
    param([string]$Prompt, [bool]$Default = $true)
    $suffix = if ($Default) { "[Y/n]" } else { "[y/N]" }
    while ($true) {
        $value = Read-Host "$Prompt $suffix"
        if ([string]::IsNullOrWhiteSpace($value)) { return $Default }
        switch ($value.Trim().ToLowerInvariant()) {
            "y" { return $true }
            "yes" { return $true }
            "n" { return $false }
            "no" { return $false }
            default { Write-Host "Responde y o n." -ForegroundColor Yellow }
        }
    }
}

function New-RandomSecret {
    param([int]$Length = 40)
    $chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_"
    $bytes = New-Object byte[] ($Length)
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $result = New-Object System.Text.StringBuilder
    foreach ($b in $bytes) { [void]$result.Append($chars[$b % $chars.Length]) }
    return $result.ToString()
}

function Write-JsonFile {
    param([string]$Path, [object]$Data)
    [System.IO.File]::WriteAllText($Path, (($Data | ConvertTo-Json -Depth 20) + [Environment]::NewLine), [System.Text.UTF8Encoding]::new($false))
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
$apiTarget = Read-RequiredValue "Ruta final API" "C:\CentralMonitoring\Api"
$workerTarget = Read-RequiredValue "Ruta final Worker" "C:\CentralMonitoring\Worker"
$uiTarget = Read-RequiredValue "Ruta final UI" "C:\CentralMonitoring\Ui"
$dbHost = Read-RequiredValue "PostgreSQL host" "127.0.0.1"
$dbPort = Read-RequiredValue "PostgreSQL port" "5432"
$dbName = Read-RequiredValue "PostgreSQL database" "central_monitoring"
$dbUser = Read-RequiredValue "PostgreSQL user" "central_app"
$dbPassword = Read-RequiredValue "PostgreSQL password"
$apiKey = Read-OptionalValue "ApiKey local de esta central (dejar vacio para generar una nueva)"
if ([string]::IsNullOrWhiteSpace($apiKey)) { $apiKey = New-RandomSecret 48 }

$cloudEnabled = Read-YesNo "Habilitar cloud en el Worker" $true
$cloudBaseUrl = ""
$cloudInstanceId = ""
$cloudInstanceName = ""
$cloudOrgName = ""
$cloudOrgSlug = ""
$cloudDescription = ""
$cloudSyncApiKey = ""
$cloudSyncInterval = 60
if ($cloudEnabled) {
    $cloudBaseUrl = Read-RequiredValue "Cloud BaseUrl" "https://centralmonitoring-cloudapi.example.com"
    $cloudInstanceId = Read-RequiredValue "Cloud InstanceId"
    $cloudInstanceName = Read-RequiredValue "Cloud InstanceName" "Central Cliente 01"
    $cloudOrgName = Read-RequiredValue "Cloud OrganizationName" "Cliente 01"
    $cloudOrgSlug = Read-RequiredValue "Cloud OrganizationSlug" "cliente-01"
    $cloudDescription = Read-OptionalValue "Cloud Description" $cloudInstanceName
    $cloudSyncApiKey = Read-OptionalValue "Cloud SyncApiKey (opcional)"
    $cloudSyncInterval = [int](Read-RequiredValue "Cloud SyncIntervalSeconds" "60")
}

New-Item -ItemType Directory -Force -Path $apiTarget, $workerTarget, $uiTarget | Out-Null
Copy-Item "$apiSource\*" $apiTarget -Recurse -Force
Copy-Item "$workerSource\*" $workerTarget -Recurse -Force
Copy-Item "$uiSource\*" $uiTarget -Recurse -Force

$connectionString = "Host=$dbHost;Port=$dbPort;Database=$dbName;Username=$dbUser;Password=$dbPassword"
$apiConfig = Get-Content (Join-Path $repoRoot "CentralMonitoring.Api/appsettings.Production.example.json") -Raw | ConvertFrom-Json
$workerConfig = Get-Content (Join-Path $repoRoot "CentralMonitoring.Worker/appsettings.Production.example.json") -Raw | ConvertFrom-Json
$apiConfig.ConnectionStrings.Default = $connectionString
$apiConfig.ApiKey = $apiKey
$workerConfig.ConnectionStrings.Default = $connectionString
$workerConfig.Cloud.Enabled = $cloudEnabled
$workerConfig.Cloud.BaseUrl = $cloudBaseUrl
$workerConfig.Cloud.InstanceId = $cloudInstanceId
$workerConfig.Cloud.InstanceName = $cloudInstanceName
$workerConfig.Cloud.OrganizationName = $cloudOrgName
$workerConfig.Cloud.OrganizationSlug = $cloudOrgSlug
$workerConfig.Cloud.Description = $cloudDescription
$workerConfig.Cloud.SyncApiKey = $cloudSyncApiKey
$workerConfig.Cloud.SyncIntervalSeconds = $cloudSyncInterval

Write-JsonFile -Path (Join-Path $apiTarget "appsettings.Production.json") -Data $apiConfig
Write-JsonFile -Path (Join-Path $workerTarget "appsettings.Production.json") -Data $workerConfig

Write-Host "Central instalada en rutas finales." -ForegroundColor Green
Write-Host "ApiKey: $apiKey" -ForegroundColor Yellow
