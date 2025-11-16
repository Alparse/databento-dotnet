# Build script for entire solution (Windows)
param(
    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [Parameter()]
    [switch]$SkipNative
)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Building Databento.NET Solution" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Step 1: Build native library
if (!$SkipNative) {
    Write-Host "`n[1/2] Building native library..." -ForegroundColor Yellow
    & "$scriptDir\build-native.ps1" -Configuration $Configuration

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Native build failed"
        exit 1
    }
} else {
    Write-Host "`n[1/2] Skipping native build" -ForegroundColor Yellow
}

# Step 2: Build .NET solution
Write-Host "`n[2/2] Building .NET solution..." -ForegroundColor Yellow
Push-Location $rootDir
try {
    dotnet build Databento.NET.sln -c $Configuration

    if ($LASTEXITCODE -ne 0) {
        throw ".NET build failed"
    }

    Write-Host "`n========================================" -ForegroundColor Green
    Write-Host "Full solution build completed!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
}
finally {
    Pop-Location
}
