param(
    [ValidateSet('Test','Publish')][string]$Mode = 'Publish',
    [string]$Dependencies = 'R:\Entwicklung\Dependencies',
    [string]$OutputDirectory,
    [switch]$ExtractAll
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$env:DOTNET_ROOT = Join-Path $Dependencies 'dotnet-10'
$env:NUGET_PACKAGES = Join-Path $Dependencies 'nuget-packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$dotnet = Join-Path $env:DOTNET_ROOT 'dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { throw "Install the SDK selected by global.json under $env:DOTNET_ROOT" }
Push-Location $projectRoot
try {
    if ($Mode -eq 'Test') {
        & $dotnet test Claudestrap.Tests/Claudestrap.Tests.csproj -c Release --nologo
    } else {
        if (-not $OutputDirectory) { $OutputDirectory=Join-Path $projectRoot 'artifacts\publish-local' }
        $extract=[bool]$ExtractAll
        & $dotnet publish Bloxstrap/Claudestrap.csproj -c Release -r win-x64 --self-contained true `
            -p:PublishSingleFile=true -p:IncludeSourceRevisionInInformationalVersion=false `
            "-p:IncludeAllContentForSelfExtract=$extract" -o $OutputDirectory --nologo
    }
    if ($LASTEXITCODE -ne 0) { throw "Build command failed with exit code $LASTEXITCODE" }
} finally { Pop-Location }
