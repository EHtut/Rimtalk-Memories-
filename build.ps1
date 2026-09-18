<#
.SYNOPSIS
    Builds Arkh straight into 1.6/Assemblies.

.EXAMPLE
    .\build.ps1
    .\build.ps1 -Configuration Debug
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $projectDir 'Arkh.csproj'

Write-Host "Building Arkh ($Configuration)..." -ForegroundColor Cyan
& dotnet build $project -c $Configuration -v minimal

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build FAILED (exit $LASTEXITCODE)" -ForegroundColor Red
    exit $LASTEXITCODE
}

$dll = Join-Path $projectDir '1.6\Assemblies\Arkh.dll'
if (Test-Path $dll) {
    $size = [math]::Round((Get-Item $dll).Length / 1KB, 1)
    Write-Host "Build OK -> 1.6\Assemblies\Arkh.dll ($size KB)" -ForegroundColor Green
} else {
    Write-Host "Build reported success but $dll is missing." -ForegroundColor Yellow
    exit 1
}
