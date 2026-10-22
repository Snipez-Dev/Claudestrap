param(
    [Parameter(Mandatory=$true)][string]$Version,
    [Parameter(Mandatory=$true)][string]$PublishDirectory,
    [string]$OutputDirectory,
    [string]$Channel = 'local',
    [string]$Dependencies = 'R:\Entwicklung\Dependencies'
)
$ErrorActionPreference='Stop'
$env:DOTNET_ROOT=Join-Path $Dependencies 'dotnet-10'
$env:NUGET_PACKAGES=Join-Path $Dependencies 'nuget-packages'
$tool=Join-Path $Dependencies 'velopack\vpk.exe'
if (-not (Test-Path -LiteralPath $tool)) { throw 'Install vpk 1.2.161 in Dependencies\velopack first.' }
if (-not $OutputDirectory) { $OutputDirectory=Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\releases' }
& $tool pack --packId Claudestrap.Local --packTitle 'Claudestrap Local' --packVersion $Version `
    --packDir $PublishDirectory --mainExe Claudestrap.exe --channel $Channel --runtime win-x64 `
    --outputDir $OutputDirectory --delta BestSpeed
if ($LASTEXITCODE -ne 0) { throw "Package build failed: $LASTEXITCODE" }
