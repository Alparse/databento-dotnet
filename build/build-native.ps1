# Build script for Databento.Native (Windows)
param(
    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [Parameter()]
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'

# Get script directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir
$nativeDir = Join-Path $rootDir "src\Databento.Native"
$buildDir = Join-Path $rootDir "build\native"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Building Databento.Native" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Clean if requested
if ($Clean -and (Test-Path $buildDir)) {
    Write-Host "Cleaning build directory..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force $buildDir
}

# Create build directory
if (!(Test-Path $buildDir)) {
    New-Item -ItemType Directory -Path $buildDir | Out-Null
}

# Check for CMake
$cmake = Get-Command cmake -ErrorAction SilentlyContinue
if (!$cmake) {
    Write-Error "CMake not found. Please install CMake and add it to PATH."
    exit 1
}

# Check for vcpkg toolchain
$vcpkgToolchain = "C:\vcpkg\scripts\buildsystems\vcpkg.cmake"
$cmakeArgs = @("-S", $nativeDir, "-B", ".", "-DCMAKE_BUILD_TYPE=$Configuration")
if (Test-Path $vcpkgToolchain) {
    Write-Host "Using vcpkg toolchain: $vcpkgToolchain" -ForegroundColor Yellow
    $cmakeArgs += "-DCMAKE_TOOLCHAIN_FILE=$vcpkgToolchain"
}

# Configure
Write-Host "`nConfiguring CMake..." -ForegroundColor Green
Push-Location $buildDir
try {
    & cmake $cmakeArgs

    if ($LASTEXITCODE -ne 0) {
        throw "CMake configuration failed"
    }

    # Build
    Write-Host "`nBuilding..." -ForegroundColor Green
    cmake --build . --config $Configuration

    if ($LASTEXITCODE -ne 0) {
        throw "Build failed"
    }

    Write-Host "`n========================================" -ForegroundColor Green
    Write-Host "Build completed successfully!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
}
finally {
    Pop-Location
}
