# Test Multiple Versions from NuGet.org

$versions = @("3.0.22-beta", "3.0.23-beta", "3.0.24-beta")
$results = @()

Write-Host "=== Testing Multiple Versions from NuGet.org ===" -ForegroundColor Cyan
Write-Host ""

foreach ($version in $versions) {
    Write-Host "----------------------------------------" -ForegroundColor Gray
    Write-Host "Testing version: $version" -ForegroundColor Yellow
    Write-Host "----------------------------------------" -ForegroundColor Gray

    $TestDir = "$env:TEMP\databento-test-$version"

    # Clean up
    Remove-Item $TestDir -Recurse -Force -ErrorAction SilentlyContinue
    New-Item $TestDir -ItemType Directory | Out-Null
    Set-Location $TestDir

    # Clear cache
    dotnet nuget locals all --clear | Out-Null

    # Create project
    dotnet new console --name Test --force | Out-Null
    Set-Location Test

    # Install specific version from NuGet.org only
    Write-Host "Installing $version from NuGet.org..." -ForegroundColor Gray
    $output = dotnet add package Databento.Client --version $version --source https://api.nuget.org/v3/index.json 2>&1

    if ($LASTEXITCODE -ne 0) {
        Write-Host "  FAILED to install!" -ForegroundColor Red
        continue
    }

    # Build
    dotnet build --verbosity quiet | Out-Null

    if ($LASTEXITCODE -ne 0) {
        Write-Host "  FAILED to build!" -ForegroundColor Red
        continue
    }

    # Check for DLLs
    $framework = (Get-ChildItem "bin\Debug")[0].Name
    $outDir = "bin\Debug\$framework"

    $vcDlls = @("msvcp140.dll", "vcruntime140.dll", "vcruntime140_1.dll")
    $foundCount = 0

    foreach ($dll in $vcDlls) {
        $path = Join-Path $outDir $dll
        if (Test-Path $path) {
            $foundCount++
        }
    }

    $hasDlls = ($foundCount -eq 3)

    # Store result
    $results += [PSCustomObject]@{
        Version = $version
        HasDlls = $hasDlls
        FoundCount = $foundCount
    }

    if ($hasDlls) {
        Write-Host "  [OK] All 3 VC++ DLLs present" -ForegroundColor Green
    } else {
        Write-Host "  [FAIL] Only $foundCount/3 VC++ DLLs present" -ForegroundColor Red
    }

    Write-Host ""
}

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "SUMMARY" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

foreach ($result in $results) {
    $status = if ($result.HasDlls) { "[OK]" } else { "[FAIL]" }
    $color = if ($result.HasDlls) { "Green" } else { "Red" }

    Write-Host "$status $($result.Version) - $($result.FoundCount)/3 VC++ DLLs" -ForegroundColor $color
}

Write-Host ""

# Determine cutoff version
$firstGood = $results | Where-Object { $_.HasDlls } | Select-Object -First 1
$lastBad = $results | Where-Object { -not $_.HasDlls } | Select-Object -Last 1

if ($firstGood) {
    Write-Host "Minimum version with fix: $($firstGood.Version)" -ForegroundColor Green
}

if ($lastBad) {
    Write-Host "Last version WITHOUT fix: $($lastBad.Version)" -ForegroundColor Red
}

Write-Host ""
Write-Host "RECOMMENDATION:" -ForegroundColor Yellow
if ($firstGood) {
    Write-Host "Users must use version $($firstGood.Version) or later" -ForegroundColor Yellow
} else {
    Write-Host "ERROR: No version has the VC++ DLLs!" -ForegroundColor Red
}
