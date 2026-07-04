param(
    [string]$Configuration = "Release",
    [switch]$Deploy,
    [switch]$Restore
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$tmpRoot = Join-Path $repoRoot ".tmp"
$dotnetTemp = Join-Path $tmpRoot "dotnet-temp"
$projectPath = Join-Path (Join-Path $repoRoot "FormationManager") "FormationManager.csproj"

New-Item -ItemType Directory -Force -Path $dotnetTemp | Out-Null

$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:MSBUILDDISABLENODEREUSE = "1"
$env:TEMP = $dotnetTemp
$env:TMP = $dotnetTemp

$buildArgs = @(
    "build",
    $projectPath,
    "-c", $Configuration,
    "-m:1",
    "-nr:false",
    "-p:UseSharedCompilation=false"
)

if (-not $Deploy) {
    $buildArgs += "-p:SkipBannerlordModuleCopy=true"
}

if (-not $Restore) {
    $buildArgs += "--no-restore"
}

try {
    dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
finally {
    dotnet build-server shutdown | Out-Null
}
