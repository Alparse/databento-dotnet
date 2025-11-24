# Test Fresh Install - Databento.Client VC++ Runtime DLLs
# This script simulates a first-time user installing the package
# and verifies all VC++ runtime DLLs are present in output directory

Write-Host "=== Databento.Client Fresh Install Test ===" -ForegroundColor Cyan
Write-Host ""

# Test configuration
$TestDir = "$env:TEMP\databento-fresh-install-test-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
$RequiredDlls = @(
    "databento_native.dll",
    "msvcp140.dll",       # VC++ C++ Standard Library
    "vcruntime140.dll",   # VC++ Runtime Core
    "vcruntime140_1.dll"  # VC++ Runtime Extended
)

Write-Host "Test Directory: $TestDir" -ForegroundColor Gray
Write-Host ""

# Create isolated test directory
Write-Host "[1/7] Creating isolated test directory..." -ForegroundColor Yellow
New-Item -Path $TestDir -ItemType Directory -Force | Out-Null
Set-Location $TestDir

# Clear NuGet cache to ensure fresh download
Write-Host "[2/7] Clearing NuGet cache (ensures fresh download from NuGet.org)..." -ForegroundColor Yellow
dotnet nuget locals all --clear | Out-Null

# Create new console project
Write-Host "[3/7] Creating new .NET console project..." -ForegroundColor Yellow
dotnet new console --force --name FreshInstallTest | Out-Null

Set-Location FreshInstallTest

# Install package from NuGet.org (latest prerelease)
Write-Host "[4/7] Installing Databento.Client from NuGet.org..." -ForegroundColor Yellow
Write-Host "      Running: dotnet add package Databento.Client --prerelease" -ForegroundColor Gray

$installOutput = dotnet add package Databento.Client --prerelease 2>&1
$version = ($installOutput | Select-String "version '([^']+)'" | ForEach-Object { $_.Matches.Groups[1].Value })

if ($version) {
    Write-Host "      Installed version: $version" -ForegroundColor Green
} else {
    Write-Host "      ERROR: Could not determine installed version!" -ForegroundColor Red
    Write-Host $installOutput
    exit 1
}

# Build the project
Write-Host "[5/7] Building project..." -ForegroundColor Yellow
$buildOutput = dotnet build --verbosity quiet 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Host "      ERROR: Build failed!" -ForegroundColor Red
    Write-Host $buildOutput
    exit 1
}

Write-Host "      Build succeeded" -ForegroundColor Green

# Detect target framework
Write-Host "[6/7] Detecting output directory..." -ForegroundColor Yellow
$outputDirs = Get-ChildItem "bin\Debug" -Directory
if ($outputDirs.Count -eq 0) {
    Write-Host "      ERROR: No output directory found!" -ForegroundColor Red
    exit 1
}

$targetFramework = $outputDirs[0].Name
$outputDir = "bin\Debug\$targetFramework"
Write-Host "      Target Framework: $targetFramework" -ForegroundColor Gray
Write-Host "      Output Directory: $outputDir" -ForegroundColor Gray

# Check for required DLLs
Write-Host "[7/7] Verifying VC++ runtime DLLs in output directory..." -ForegroundColor Yellow
Write-Host ""

$allFound = $true
$missingDlls = @()
$foundDlls = @()

foreach ($dll in $RequiredDlls) {
    $dllPath = Join-Path $outputDir $dll

    if (Test-Path $dllPath) {
        $fileInfo = Get-Item $dllPath
        $sizeKB = [math]::Round($fileInfo.Length / 1KB, 0)
        $foundDlls += [PSCustomObject]@{
            Name = $dll
            SizeKB = $sizeKB
            Path = $dllPath
        }

        # Special highlighting for VC++ DLLs
        if ($dll -match "msvcp|vcruntime") {
            Write-Host "   OK $dll ($sizeKB KB) [VC++ RUNTIME]" -ForegroundColor Green
        } else {
            Write-Host "   OK $dll ($sizeKB KB)" -ForegroundColor Green
        }
    } else {
        $allFound = $false
        $missingDlls += $dll

        if ($dll -match "msvcp|vcruntime") {
            Write-Host "   MISSING $dll [VC++ RUNTIME]" -ForegroundColor Red -BackgroundColor Yellow
        } else {
            Write-Host "   MISSING $dll" -ForegroundColor Red
        }
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan

if ($allFound) {
    Write-Host "✅ TEST PASSED!" -ForegroundColor Green
    Write-Host ""
    Write-Host "All required VC++ runtime DLLs are present in output directory." -ForegroundColor Green
    Write-Host "Package version $version is working correctly." -ForegroundColor Green
    Write-Host ""
    Write-Host "This means:" -ForegroundColor White
    Write-Host "  • Users WITHOUT system VC++ runtime should be able to run the application" -ForegroundColor White
    Write-Host "  • DLLs are bundled correctly in the NuGet package" -ForegroundColor White
    Write-Host "  • Build targets are executing properly" -ForegroundColor White
    Write-Host ""
} else {
    Write-Host "❌ TEST FAILED!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Missing DLLs:" -ForegroundColor Red
    foreach ($dll in $missingDlls) {
        Write-Host "  • $dll" -ForegroundColor Red
    }
    Write-Host ""
    Write-Host "This means:" -ForegroundColor White
    Write-Host "  • Users WITHOUT system VC++ runtime WILL experience DllNotFoundException" -ForegroundColor Red
    Write-Host "  • Either DLLs are not in the package OR build targets aren't executing" -ForegroundColor Red
    Write-Host ""
}

# Additional diagnostics
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Additional Diagnostics:" -ForegroundColor Yellow
Write-Host ""

# Check if DLLs exist in NuGet cache
Write-Host "Checking NuGet package cache..." -ForegroundColor Gray
$nugetCachePath = "$env:USERPROFILE\.nuget\packages\databento.client\$version\runtimes\win-x64\native"
if (Test-Path $nugetCachePath) {
    Write-Host "  Cache path exists: $nugetCachePath" -ForegroundColor Green

    $cachedVcDlls = Get-ChildItem $nugetCachePath -Filter "*vcruntime*.dll" -File
    $cachedVcDlls += Get-ChildItem $nugetCachePath -Filter "msvcp140.dll" -File

    if ($cachedVcDlls.Count -eq 3) {
        Write-Host "  ✓ All 3 VC++ DLLs found in NuGet cache" -ForegroundColor Green
        foreach ($dll in $cachedVcDlls) {
            Write-Host "    - $($dll.Name)" -ForegroundColor Gray
        }
    } else {
        Write-Host "  ✗ VC++ DLLs missing from NuGet cache! (Found: $($cachedVcDlls.Count)/3)" -ForegroundColor Red
    }
} else {
    Write-Host "  ✗ NuGet cache path not found: $nugetCachePath" -ForegroundColor Red
}

Write-Host ""

# Check runtimes subdirectory
Write-Host "Checking runtimes subdirectory in output..." -ForegroundColor Gray
$runtimesPath = Join-Path $outputDir "runtimes\win-x64\native"
if (Test-Path $runtimesPath) {
    Write-Host "  ✓ Runtimes subdirectory exists: $runtimesPath" -ForegroundColor Green

    $runtimeVcDlls = Get-ChildItem $runtimesPath -Filter "*vcruntime*.dll" -File -ErrorAction SilentlyContinue
    $runtimeVcDlls += Get-ChildItem $runtimesPath -Filter "msvcp140.dll" -File -ErrorAction SilentlyContinue

    if ($runtimeVcDlls.Count -gt 0) {
        Write-Host "  Note: DLLs also in runtimes subdirectory (copied by NuGet)" -ForegroundColor Gray
        Write-Host "  These are NOT used at runtime - root directory DLLs are used" -ForegroundColor Gray
    }
} else {
    Write-Host "  Runtimes subdirectory not found (this is OK if DLLs are in root)" -ForegroundColor Gray
}

Write-Host ""

# Show test location
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Test Location:" -ForegroundColor Yellow
Write-Host "  $TestDir" -ForegroundColor White
Write-Host ""
Write-Host "To inspect manually:" -ForegroundColor Gray
Write-Host "  cd `"$TestDir\FreshInstallTest`"" -ForegroundColor Gray
Write-Host "  ls $outputDir\*.dll" -ForegroundColor Gray
Write-Host ""

# Cleanup prompt
Write-Host "Keep test directory? [y/N]: " -ForegroundColor Yellow -NoNewline
$keep = Read-Host

if ($keep -ne 'y' -and $keep -ne 'Y') {
    Write-Host "Cleaning up test directory..." -ForegroundColor Gray
    Set-Location $env:TEMP
    Remove-Item -Path $TestDir -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "Cleaned up." -ForegroundColor Green
} else {
    Write-Host "Test directory preserved for inspection." -ForegroundColor Green
}

Write-Host ""
Write-Host "Test complete!" -ForegroundColor Cyan

# Return exit code based on test result
if ($allFound) {
    exit 0
} else {
    exit 1
}
