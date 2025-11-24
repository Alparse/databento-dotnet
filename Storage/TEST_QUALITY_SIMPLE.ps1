# Simple Quality Indicators Test
# Verifies the essential quality indicator settings

$ErrorActionPreference = "Stop"
$Version = "3.0.24-beta"
$PackageName = "Databento.Client"

Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "Quality Indicators - Quick Test" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

$allPassed = $true

# Test 1: Packages exist
Write-Host "[1/6] Package files..." -ForegroundColor Yellow
$nupkg = "src\Databento.Client\bin\Release\$PackageName.$Version.nupkg"
$snupkg = "src\Databento.Client\bin\Release\$PackageName.$Version.snupkg"

if ((Test-Path $nupkg) -and (Test-Path $snupkg)) {
    Write-Host "  PASS - Both .nupkg and .snupkg exist" -ForegroundColor Green
} else {
    Write-Host "  FAIL - Package files missing" -ForegroundColor Red
    $allPassed = $false
}

# Test 2: Directory.Build.props settings
Write-Host "[2/6] Directory.Build.props..." -ForegroundColor Yellow
if (Test-Path "Directory.Build.props") {
    $props = Get-Content "Directory.Build.props" -Raw
    $checks = @(
        ($props -match "<Deterministic>true</Deterministic>"),
        ($props -match "<PublishRepositoryUrl>true</PublishRepositoryUrl>"),
        ($props -match "<SymbolPackageFormat>snupkg</SymbolPackageFormat>")
    )
    if ($checks -notcontains $false) {
        Write-Host "  PASS - All quality settings present" -ForegroundColor Green
    } else {
        Write-Host "  FAIL - Some settings missing" -ForegroundColor Red
        $allPassed = $false
    }
} else {
    Write-Host "  FAIL - Directory.Build.props not found" -ForegroundColor Red
    $allPassed = $false
}

# Test 3: Source Link packages
Write-Host "[3/6] Source Link packages..." -ForegroundColor Yellow
$client = Get-Content "src\Databento.Client\Databento.Client.csproj" -Raw
$interop = Get-Content "src\Databento.Interop\Databento.Interop.csproj" -Raw

if (($client -match "Microsoft.SourceLink.GitHub") -and ($interop -match "Microsoft.SourceLink.GitHub")) {
    Write-Host "  PASS - Source Link in both projects" -ForegroundColor Green
} else {
    Write-Host "  FAIL - Source Link missing" -ForegroundColor Red
    $allPassed = $false
}

# Test 4: Package contents
Write-Host "[4/6] Package contents..." -ForegroundColor Yellow
$tempDir = "$env:TEMP\pkg-inspect-$(Get-Random)"
New-Item -ItemType Directory -Path $tempDir | Out-Null

try {
    Copy-Item $nupkg "$tempDir\pkg.zip"
    Expand-Archive "$tempDir\pkg.zip" -DestinationPath "$tempDir\extracted" -Force

    $hasClient = Test-Path "$tempDir\extracted\lib\net8.0\Databento.Client.dll"
    $hasInterop = Test-Path "$tempDir\extracted\lib\net8.0\Databento.Interop.dll"
    $hasVcRuntime = Test-Path "$tempDir\extracted\runtimes\win-x64\native\vcruntime140.dll"

    if ($hasClient -and $hasInterop -and $hasVcRuntime) {
        Write-Host "  PASS - Key DLLs present in package" -ForegroundColor Green
    } else {
        Write-Host "  FAIL - Missing DLLs" -ForegroundColor Red
        if (-not $hasClient) { Write-Host "    - Missing Databento.Client.dll" -ForegroundColor Red }
        if (-not $hasInterop) { Write-Host "    - Missing Databento.Interop.dll" -ForegroundColor Red }
        if (-not $hasVcRuntime) { Write-Host "    - Missing VC++ runtime DLLs" -ForegroundColor Red }
        $allPassed = $false
    }
} finally {
    Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}

# Test 5: Build succeeds
Write-Host "[5/6] Clean build test..." -ForegroundColor Yellow
$buildOutput = dotnet build -c Release --verbosity quiet 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "  PASS - Build succeeded" -ForegroundColor Green
} else {
    Write-Host "  FAIL - Build failed" -ForegroundColor Red
    $allPassed = $false
}

# Test 6: Symbol package size
Write-Host "[6/6] Symbol package..." -ForegroundColor Yellow
$snupkgSize = (Get-Item $snupkg).Length / 1KB
if ($snupkgSize -gt 1) {
    Write-Host "  PASS - Symbol package created ($([int]$snupkgSize) KB)" -ForegroundColor Green
} else {
    Write-Host "  FAIL - Symbol package too small or empty" -ForegroundColor Red
    $allPassed = $false
}

Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
if ($allPassed) {
    Write-Host "ALL TESTS PASSED" -ForegroundColor Green
    Write-Host ""
    Write-Host "Quality indicators ready:" -ForegroundColor White
    Write-Host "  * Source Link: Enabled" -ForegroundColor Green
    Write-Host "  * Deterministic: Enabled" -ForegroundColor Green
    Write-Host "  * Symbols: Enabled (.snupkg)" -ForegroundColor Green
    Write-Host ""
    Write-Host "Ready to publish to NuGet.org!" -ForegroundColor Green
} else {
    Write-Host "SOME TESTS FAILED" -ForegroundColor Red
    Write-Host "Review errors above" -ForegroundColor Yellow
}
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""
