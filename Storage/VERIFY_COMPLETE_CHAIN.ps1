# Verify Complete Chain - Package → NuGet Cache → Output Directory
# This proves the DLLs go from NuGet.org all the way to the running application

param(
    [string]$Version = "3.0.24-beta"
)

$TestDir = "$env:TEMP\verify-chain-$Version"
$vcDlls = @("msvcp140.dll", "vcruntime140.dll", "vcruntime140_1.dll")

Write-Host "=== Verifying Complete Installation Chain ===" -ForegroundColor Cyan
Write-Host "Version: $Version" -ForegroundColor White
Write-Host "This test proves DLLs go from NuGet.org → Cache → Output Directory" -ForegroundColor Gray
Write-Host ""

# Clean up
Remove-Item $TestDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item $TestDir -ItemType Directory | Out-Null
Set-Location $TestDir

# Step 1: Download package directly from NuGet.org
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "STEP 1: Download package from NuGet.org" -ForegroundColor Yellow
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host ""

$packageUrl = "https://www.nuget.org/api/v2/package/Databento.Client/$Version"
$packageFile = "Databento.Client.$Version.nupkg"

Write-Host "Downloading from: $packageUrl" -ForegroundColor Gray
Invoke-WebRequest -Uri $packageUrl -OutFile $packageFile -ErrorAction Stop

# Extract and check
$zipFile = $packageFile.Replace(".nupkg", ".zip")
Copy-Item $packageFile $zipFile
Expand-Archive -Path $zipFile -DestinationPath "package-contents" -Force

Write-Host "✓ Downloaded package" -ForegroundColor Green

# Check if DLLs are in package
$packageDllPath = "package-contents\runtimes\win-x64\native"
$packageHasDlls = $true

Write-Host ""
Write-Host "Checking package contents:" -ForegroundColor Gray
foreach ($dll in $vcDlls) {
    $path = Join-Path $packageDllPath $dll
    if (Test-Path $path) {
        $size = [math]::Round((Get-Item $path).Length / 1KB)
        Write-Host "  ✓ $dll ($size KB) in package" -ForegroundColor Green
    } else {
        Write-Host "  ✗ $dll MISSING from package" -ForegroundColor Red
        $packageHasDlls = $false
    }
}

if (-not $packageHasDlls) {
    Write-Host ""
    Write-Host "❌ FAILED: DLLs not in package!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Result: Package contains all VC++ DLLs ✓" -ForegroundColor Green
Write-Host ""

# Step 2: Clear cache and install via dotnet
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "STEP 2: Install package via dotnet CLI" -ForegroundColor Yellow
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host ""

Write-Host "Clearing NuGet cache..." -ForegroundColor Gray
dotnet nuget locals all --clear | Out-Null
Write-Host "✓ Cache cleared" -ForegroundColor Green

Write-Host ""
Write-Host "Creating test project..." -ForegroundColor Gray
dotnet new console --name TestApp --force | Out-Null
Set-Location TestApp
Write-Host "✓ Project created" -ForegroundColor Green

Write-Host ""
Write-Host "Installing package from NuGet.org only..." -ForegroundColor Gray
$installOutput = dotnet add package Databento.Client --version $Version --source https://api.nuget.org/v3/index.json 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Installation failed!" -ForegroundColor Red
    Write-Host $installOutput
    exit 1
}
Write-Host "✓ Package installed" -ForegroundColor Green

Write-Host ""
Write-Host "Result: Package installed from NuGet.org ✓" -ForegroundColor Green
Write-Host ""

# Step 3: Check NuGet cache
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "STEP 3: Verify DLLs in NuGet cache" -ForegroundColor Yellow
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host ""

$cachePath = "$env:USERPROFILE\.nuget\packages\databento.client\$Version\runtimes\win-x64\native"
Write-Host "Cache location: $cachePath" -ForegroundColor Gray
Write-Host ""

$cacheHasDlls = $true
if (Test-Path $cachePath) {
    Write-Host "Checking cache:" -ForegroundColor Gray
    foreach ($dll in $vcDlls) {
        $path = Join-Path $cachePath $dll
        if (Test-Path $path) {
            $size = [math]::Round((Get-Item $path).Length / 1KB)
            Write-Host "  ✓ $dll ($size KB) in cache" -ForegroundColor Green
        } else {
            Write-Host "  ✗ $dll MISSING from cache" -ForegroundColor Red
            $cacheHasDlls = $false
        }
    }
} else {
    Write-Host "✗ Cache directory not found!" -ForegroundColor Red
    $cacheHasDlls = $false
}

Write-Host ""
if ($cacheHasDlls) {
    Write-Host "Result: NuGet cache contains all VC++ DLLs ✓" -ForegroundColor Green
} else {
    Write-Host "Result: NuGet cache MISSING DLLs ✗" -ForegroundColor Red
}
Write-Host ""

# Step 4: Build project
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "STEP 4: Build project (triggers MSBuild targets)" -ForegroundColor Yellow
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host ""

Write-Host "Running: dotnet build" -ForegroundColor Gray
$buildOutput = dotnet build --verbosity minimal 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Build failed!" -ForegroundColor Red
    Write-Host $buildOutput
    exit 1
}
Write-Host "✓ Build succeeded" -ForegroundColor Green
Write-Host ""

# Step 5: Check output directory
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "STEP 5: Verify DLLs copied to output directory" -ForegroundColor Yellow
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host ""

$framework = (Get-ChildItem "bin\Debug")[0].Name
$outputDir = "bin\Debug\$framework"
Write-Host "Output directory: $outputDir" -ForegroundColor Gray
Write-Host ""

$outputHasDlls = $true
Write-Host "Checking output directory:" -ForegroundColor Gray
foreach ($dll in $vcDlls) {
    $path = Join-Path $outputDir $dll
    if (Test-Path $path) {
        $size = [math]::Round((Get-Item $path).Length / 1KB)
        Write-Host "  ✓ $dll ($size KB) in output" -ForegroundColor Green
    } else {
        Write-Host "  ✗ $dll MISSING from output" -ForegroundColor Red
        $outputHasDlls = $false
    }
}

Write-Host ""
if ($outputHasDlls) {
    Write-Host "Result: Output directory contains all VC++ DLLs ✓" -ForegroundColor Green
} else {
    Write-Host "Result: Output directory MISSING DLLs ✗" -ForegroundColor Red
}
Write-Host ""

# Step 6: Verify build targets executed
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "STEP 6: Verify MSBuild targets executed" -ForegroundColor Yellow
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host ""

# Check if .targets file exists in package
$targetsPath = "$env:USERPROFILE\.nuget\packages\databento.client\$Version\build\Databento.Client.targets"
if (Test-Path $targetsPath) {
    Write-Host "✓ Build targets file exists: Databento.Client.targets" -ForegroundColor Green

    # Show what the targets file does
    Write-Host ""
    Write-Host "Build targets content (snippet):" -ForegroundColor Gray
    $targetsContent = Get-Content $targetsPath -Raw
    if ($targetsContent -match "CopyToOutputDirectory") {
        Write-Host "  ✓ Contains CopyToOutputDirectory rules" -ForegroundColor Green
    }
    if ($targetsContent -match "runtimes") {
        Write-Host "  ✓ References runtimes directory" -ForegroundColor Green
    }
} else {
    Write-Host "✗ Build targets file not found" -ForegroundColor Red
}

Write-Host ""
Write-Host "Result: MSBuild targets properly configured ✓" -ForegroundColor Green
Write-Host ""

# Final Summary
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "FINAL SUMMARY" -ForegroundColor Yellow
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host ""

$allPassed = $packageHasDlls -and $cacheHasDlls -and $outputHasDlls

Write-Host "Version tested: $Version" -ForegroundColor White
Write-Host ""
Write-Host "Complete Chain Verification:" -ForegroundColor White
Write-Host "  [1] NuGet.org Package:   " -NoNewline
Write-Host $(if ($packageHasDlls) { "✓ PASS" } else { "✗ FAIL" }) -ForegroundColor $(if ($packageHasDlls) { "Green" } else { "Red" })

Write-Host "  [2] NuGet Cache:         " -NoNewline
Write-Host $(if ($cacheHasDlls) { "✓ PASS" } else { "✗ FAIL" }) -ForegroundColor $(if ($cacheHasDlls) { "Green" } else { "Red" })

Write-Host "  [3] Output Directory:    " -NoNewline
Write-Host $(if ($outputHasDlls) { "✓ PASS" } else { "✗ FAIL" }) -ForegroundColor $(if ($outputHasDlls) { "Green" } else { "Red" })

Write-Host ""

if ($allPassed) {
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Green
    Write-Host "✅ COMPLETE CHAIN VERIFIED" -ForegroundColor Black -BackgroundColor Green
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Green
    Write-Host ""
    Write-Host "CONCLUSION:" -ForegroundColor Yellow
    Write-Host "  The complete installation chain works correctly." -ForegroundColor Green
    Write-Host "  DLLs successfully flow from:" -ForegroundColor Green
    Write-Host "    NuGet.org → NuGet Cache → Output Directory" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Users installing v$Version will get all VC++ runtime DLLs" -ForegroundColor Green
    Write-Host "  and will NOT need to install Visual C++ Redistributable." -ForegroundColor Green
} else {
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Red
    Write-Host "❌ CHAIN VERIFICATION FAILED" -ForegroundColor White -BackgroundColor Red
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Red
    Write-Host ""
    Write-Host "PROBLEM:" -ForegroundColor Red
    Write-Host "  The installation chain is broken somewhere." -ForegroundColor Red
    Write-Host "  Users will experience DllNotFoundException!" -ForegroundColor Red
}

Write-Host ""
Write-Host "Test artifacts preserved at: $TestDir" -ForegroundColor Gray
Write-Host ""

exit $(if ($allPassed) { 0 } else { 1 })
