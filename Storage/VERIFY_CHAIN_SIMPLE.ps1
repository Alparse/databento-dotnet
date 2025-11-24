# Simple Complete Chain Verification

param([string]$Version = "3.0.24-beta")

$TestDir = "$env:TEMP\verify-$Version-$(Get-Date -Format 'HHmmss')"
Write-Host "=== Complete Chain Verification: v$Version ===" -ForegroundColor Cyan
Write-Host ""

# Setup
Remove-Item $TestDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item $TestDir -ItemType Directory | Out-Null
Set-Location $TestDir

# STEP 1: Download package from NuGet.org
Write-Host "[1/5] Downloading package from NuGet.org..." -ForegroundColor Yellow
$url = "https://www.nuget.org/api/v2/package/Databento.Client/$Version"
$pkg = "package.nupkg"
Invoke-WebRequest -Uri $url -OutFile $pkg
Copy-Item $pkg "package.zip"
Expand-Archive "package.zip" -DestinationPath "pkg" -Force

$pkgPath = "pkg\runtimes\win-x64\native"
$pkgPass = (Test-Path "$pkgPath\msvcp140.dll") -and (Test-Path "$pkgPath\vcruntime140.dll") -and (Test-Path "$pkgPath\vcruntime140_1.dll")

if ($pkgPass) {
    Write-Host "   OK - Package contains all 3 VC++ DLLs" -ForegroundColor Green
} else {
    Write-Host "   FAIL - Package missing VC++ DLLs" -ForegroundColor Red
    exit 1
}

# STEP 2: Clear cache
Write-Host "[2/5] Clearing NuGet cache..." -ForegroundColor Yellow
dotnet nuget locals all --clear | Out-Null
Write-Host "   OK - Cache cleared" -ForegroundColor Green

# STEP 3: Install via dotnet
Write-Host "[3/5] Installing package..." -ForegroundColor Yellow
dotnet new console --name Test --force | Out-Null
Set-Location Test
dotnet add package Databento.Client --version $Version --source https://api.nuget.org/v3/index.json | Out-Null

$cachePath = "$env:USERPROFILE\.nuget\packages\databento.client\$Version\runtimes\win-x64\native"
$cachePass = (Test-Path "$cachePath\msvcp140.dll") -and (Test-Path "$cachePath\vcruntime140.dll") -and (Test-Path "$cachePath\vcruntime140_1.dll")

if ($cachePass) {
    Write-Host "   OK - DLLs in NuGet cache" -ForegroundColor Green
} else {
    Write-Host "   FAIL - DLLs missing from cache" -ForegroundColor Red
    exit 1
}

# STEP 4: Build
Write-Host "[4/5] Building project..." -ForegroundColor Yellow
dotnet build --verbosity quiet | Out-Null

# STEP 5: Check output
Write-Host "[5/5] Checking output directory..." -ForegroundColor Yellow
$fw = (Get-ChildItem "bin\Debug")[0].Name
$out = "bin\Debug\$fw"

$outPass = (Test-Path "$out\msvcp140.dll") -and (Test-Path "$out\vcruntime140.dll") -and (Test-Path "$out\vcruntime140_1.dll")

if ($outPass) {
    Write-Host "   OK - DLLs in output directory" -ForegroundColor Green
} else {
    Write-Host "   FAIL - DLLs missing from output" -ForegroundColor Red

    Write-Host ""
    Write-Host "   Listing output directory:" -ForegroundColor Gray
    Get-ChildItem $out -Filter "*.dll" | ForEach-Object { Write-Host "     - $($_.Name)" -ForegroundColor Gray }

    exit 1
}

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "RESULT: COMPLETE CHAIN VERIFIED" -ForegroundColor Black -BackgroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Version: $Version" -ForegroundColor White
Write-Host ""
Write-Host "Chain Status:" -ForegroundColor White
Write-Host "  [1] Package from NuGet.org:  PASS" -ForegroundColor Green
Write-Host "  [2] NuGet Cache:             PASS" -ForegroundColor Green
Write-Host "  [3] Output Directory:        PASS" -ForegroundColor Green
Write-Host ""
Write-Host "All VC++ runtime DLLs present:" -ForegroundColor White
Write-Host "  - msvcp140.dll" -ForegroundColor Gray
Write-Host "  - vcruntime140.dll" -ForegroundColor Gray
Write-Host "  - vcruntime140_1.dll" -ForegroundColor Gray
Write-Host ""
Write-Host "Users installing v$Version will NOT need to install C++ runtime!" -ForegroundColor Green
Write-Host ""
Write-Host "Test location: $TestDir" -ForegroundColor Gray
