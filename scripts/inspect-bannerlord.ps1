param(
    [string]$Type = "",

    [string]$Member = "",
    [string]$Assembly = "",
    [string]$ListTypes = "",
    [switch]$List
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$tmpRoot = Join-Path $repoRoot ".tmp"
$dotnetTemp = Join-Path $tmpRoot "dotnet-temp"
$toolIntermediate = Join-Path $tmpRoot "obj\BannerlordInspector-$PID"
$toolRoot = Join-Path $repoRoot "tools\BannerlordInspector"
$toolSource = Join-Path $toolRoot "Program.cs"
$toolOutput = Join-Path $tmpRoot "tools\BannerlordInspector-$PID"
$toolExe = Join-Path $toolOutput "BannerlordInspector.exe"
$csc = "C:\Program Files\dotnet\sdk\8.0.403\Roslyn\bincore\csc.dll"
$frameworkRefs = Join-Path $env:USERPROFILE ".nuget\packages\microsoft.netframework.referenceassemblies.net472\1.0.3\build\.NETFramework\v4.7.2"

New-Item -ItemType Directory -Force -Path $dotnetTemp | Out-Null
New-Item -ItemType Directory -Force -Path $toolIntermediate | Out-Null
New-Item -ItemType Directory -Force -Path $toolOutput | Out-Null

$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:MSBUILDDISABLENODEREUSE = "1"
$env:TEMP = $dotnetTemp
$env:TMP = $dotnetTemp

if (-not (Test-Path $csc)) {
    Write-Error "Could not find Roslyn compiler at $csc"
    exit 2
}

if (-not (Test-Path $frameworkRefs)) {
    Write-Error "Could not find cached .NET Framework 4.7.2 references at $frameworkRefs"
    exit 2
}

$referenceArgs = Get-ChildItem -Path $frameworkRefs -Filter "*.dll" |
    Where-Object { $_.Name -notin @("System.EnterpriseServices.Thunk.dll", "System.EnterpriseServices.Wrapper.dll") } |
    ForEach-Object {
    "/reference:$($_.FullName)"
}

$rsp = Join-Path $toolIntermediate "csc.rsp"
$compileArgs = @(
    "/nologo",
    "/target:exe",
    "/optimize+",
    "/langversion:latest",
    "/nullable:enable",
    "/nostdlib+",
    "/out:$toolExe"
) + $referenceArgs + @($toolSource)

Set-Content -LiteralPath $rsp -Value $compileArgs -Encoding ASCII
dotnet $csc "@$rsp"
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

if ([string]::IsNullOrWhiteSpace($Type) -and [string]::IsNullOrWhiteSpace($ListTypes)) {
    Write-Error "Pass either -Type <Full.Type.Name> or -ListTypes <filter>."
    exit 2
}

$args = @()
if (-not [string]::IsNullOrWhiteSpace($ListTypes)) {
    $args += @("--list-types", $ListTypes)
} else {
    $args += @("--type", $Type)
}
if (-not [string]::IsNullOrWhiteSpace($Member)) {
    $args += @("--member", $Member)
}
if (-not [string]::IsNullOrWhiteSpace($Assembly)) {
    $args += @("--assembly", $Assembly)
}
if ($List) {
    $args += "--list"
}

& $toolExe @args
exit $LASTEXITCODE
