# Download and Inspect NuGet Package from NuGet.org
# This script downloads the actual .nupkg file and shows what's inside

param(
    [string]$Version = "3.0.24-beta"
)

$PackageName = "Databento.Client"
$DownloadDir = "$env:TEMP\nuget-inspect"

Write-Host "=== Inspecting $PackageName $Version from NuGet.org ===" -ForegroundColor Cyan
Write-Host ""

# Clean and create download directory
Remove-Item $DownloadDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item $DownloadDir -ItemType Directory | Out-Null
Set-Location $DownloadDir

# Download the package directly from NuGet.org
$packageUrl = "https://www.nuget.org/api/v2/package/$PackageName/$Version"
$packageFile = "$PackageName.$Version.nupkg"

Write-Host "[1/4] Downloading package from NuGet.org..." -ForegroundColor Yellow
Write-Host "      URL: $packageUrl" -ForegroundColor Gray

try {
    Invoke-WebRequest -Uri $packageUrl -OutFile $packageFile -ErrorAction Stop
    Write-Host "      Downloaded: $packageFile" -ForegroundColor Green
    $fileSize = [math]::Round((Get-Item $packageFile).Length / 1MB, 2)
    Write-Host "      Size: $fileSize MB" -ForegroundColor Gray
} catch {
    Write-Host "      ERROR: Failed to download package!" -ForegroundColor Red
    Write-Host "      $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Extract the package (it's just a zip file)
Write-Host "[2/4] Extracting package contents..." -ForegroundColor Yellow
$extractDir = "extracted"

# Rename .nupkg to .zip (they're the same format)
$zipFile = $packageFile.Replace(".nupkg", ".zip")
Copy-Item $packageFile $zipFile
Expand-Archive -Path $zipFile -DestinationPath $extractDir -Force
Write-Host "      Extracted to: $extractDir" -ForegroundColor Green

Write-Host ""

# Check for VC++ runtime DLLs
Write-Host "[3/4] Looking for VC++ runtime DLLs..." -ForegroundColor Yellow
$runtimePath = "$extractDir\runtimes\win-x64\native"

if (Test-Path $runtimePath) {
    Write-Host "      Runtime directory exists: $runtimePath" -ForegroundColor Green
    Write-Host ""

    $vcDlls = @("msvcp140.dll", "vcruntime140.dll", "vcruntime140_1.dll")
    $foundDlls = @()
    $missingDlls = @()

    foreach ($dll in $vcDlls) {
        $dllPath = Join-Path $runtimePath $dll
        if (Test-Path $dllPath) {
            $size = [math]::Round((Get-Item $dllPath).Length / 1KB)
            $foundDlls += $dll
            Write-Host "      [OK] $dll ($size KB)" -ForegroundColor Green
        } else {
            $missingDlls += $dll
            Write-Host "      [MISSING] $dll" -ForegroundColor Red
        }
    }

    Write-Host ""

    if ($missingDlls.Count -eq 0) {
        Write-Host "      ✅ All 3 VC++ runtime DLLs are present!" -ForegroundColor Green
    } else {
        Write-Host "      ❌ $($missingDlls.Count) VC++ DLLs are missing!" -ForegroundColor Red
    }
} else {
    Write-Host "      ❌ Runtime directory not found: $runtimePath" -ForegroundColor Red
}

Write-Host ""

# List all DLLs in the package
Write-Host "[4/4] Complete DLL inventory in package..." -ForegroundColor Yellow
Write-Host ""

$allDlls = Get-ChildItem -Path $extractDir -Filter "*.dll" -Recurse | Sort-Object FullName

if ($allDlls.Count -gt 0) {
    Write-Host "      Found $($allDlls.Count) DLL files:" -ForegroundColor Gray
    Write-Host ""

    foreach ($dll in $allDlls) {
        $relativePath = $dll.FullName.Replace("$extractDir\", "")
        $sizeKB = [math]::Round($dll.Length / 1KB)

        # Highlight VC++ runtime DLLs
        if ($dll.Name -match "msvcp|vcruntime") {
            Write-Host "      $relativePath" -ForegroundColor Green -NoNewline
            Write-Host " ($sizeKB KB)" -ForegroundColor Gray -NoNewline
            Write-Host " ← VC++ RUNTIME" -ForegroundColor Cyan
        } else {
            Write-Host "      $relativePath ($sizeKB KB)" -ForegroundColor Gray
        }
    }
} else {
    Write-Host "      No DLL files found in package!" -ForegroundColor Red
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Summary
Write-Host "SUMMARY:" -ForegroundColor Yellow
Write-Host "  Package: $PackageName $Version" -ForegroundColor White
Write-Host "  Source: NuGet.org" -ForegroundColor White
Write-Host "  Downloaded: $DownloadDir\$packageFile" -ForegroundColor White
Write-Host ""

$vcDllsPresent = (Test-Path "$runtimePath\msvcp140.dll") -and
                 (Test-Path "$runtimePath\vcruntime140.dll") -and
                 (Test-Path "$runtimePath\vcruntime140_1.dll")

if ($vcDllsPresent) {
    Write-Host "  STATUS: ✅ PASS - VC++ runtime DLLs are bundled" -ForegroundColor Black -BackgroundColor Green
    Write-Host ""
    Write-Host "  This version WILL work for users without Visual C++ installed." -ForegroundColor Green
} else {
    Write-Host "  STATUS: ❌ FAIL - VC++ runtime DLLs are NOT bundled" -ForegroundColor White -BackgroundColor Red
    Write-Host ""
    Write-Host "  This version WILL NOT work for users without Visual C++ installed." -ForegroundColor Red
}

Write-Host ""
Write-Host "Package contents preserved at: $DownloadDir" -ForegroundColor Gray
Write-Host ""

# Ask to open explorer
Write-Host "Open package directory in Explorer? [y/N]: " -ForegroundColor Yellow -NoNewline
$open = Read-Host

if ($open -eq 'y' -or $open -eq 'Y') {
    explorer $DownloadDir
}
