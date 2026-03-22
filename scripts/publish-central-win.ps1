param(
    [ValidateSet("win-x64", "linux-x64")]
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$OutputRoot = "dist",
    [string]$UiApiBase = "",
    [string]$UiApiKey = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$apiOutput = Join-Path $repoRoot (Join-Path $OutputRoot "CentralMonitoring.Api")
$workerOutput = Join-Path $repoRoot (Join-Path $OutputRoot "CentralMonitoring.Worker")
$uiEnvPath = Join-Path $repoRoot "ui/src/app/env.ts"
$uiEnvBackup = Join-Path $env:TEMP ("centralmonitoring-ui-env-" + [guid]::NewGuid().ToString("N") + ".ts")

Copy-Item $uiEnvPath $uiEnvBackup -Force
try {
    if (-not [string]::IsNullOrWhiteSpace($UiApiBase) -and -not [string]::IsNullOrWhiteSpace($UiApiKey)) {
        $content = @"
export const env = {
  apiBase: '$UiApiBase',
  apiKey: '$UiApiKey'
};
"@
        [System.IO.File]::WriteAllText($uiEnvPath, $content, [System.Text.UTF8Encoding]::new($false))
    }

    Write-Host "Publishing API to $apiOutput"
    dotnet publish (Join-Path $repoRoot "CentralMonitoring.Api/CentralMonitoring.Api.csproj") `
        -c $Configuration `
        -r $Runtime `
        --self-contained true `
        -o $apiOutput

    Write-Host "Publishing Worker to $workerOutput"
    dotnet publish (Join-Path $repoRoot "CentralMonitoring.Worker/CentralMonitoring.Worker.csproj") `
        -c $Configuration `
        -r $Runtime `
        --self-contained true `
        -o $workerOutput

    Write-Host "Building UI"
    Push-Location (Join-Path $repoRoot "ui")
    try {
        if (-not (Test-Path "node_modules")) {
            npm install
        }

        npm run build
    }
    finally {
        Pop-Location
    }

    Write-Host "Central publish completed."
}
finally {
    if (Test-Path $uiEnvBackup) {
        Copy-Item $uiEnvBackup $uiEnvPath -Force
        Remove-Item $uiEnvBackup -Force
    }
}
