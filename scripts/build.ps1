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
$classifierBuildScript = Join-Path (Split-Path -Parent $repoRoot) "Bannerlord-Troop-Classifier\scripts\build.ps1"

New-Item -ItemType Directory -Force -Path $dotnetTemp | Out-Null

$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:MSBUILDDISABLENODEREUSE = "1"
$env:TEMP = $dotnetTemp
$env:TMP = $dotnetTemp

if ($Deploy) {
    if (-not (Test-Path $classifierBuildScript)) {
        throw "Troop Classifier is required but its sibling build script was not found: $classifierBuildScript"
    }

    $classifierArgs = @("-ExecutionPolicy", "Bypass", "-File", $classifierBuildScript, "-Configuration", $Configuration, "-Deploy")
    if ($Restore) {
        $classifierArgs += "-Restore"
    }

    & powershell @classifierArgs
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

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
