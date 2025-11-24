# Test ONLY from NuGet.org - Databento.Client VC++ DLLs

$TestDir = "$env:TEMP\databento-nuget-org-only-test"

Write-Host "=== Testing Databento.Client from NuGet.org ONLY ===" -ForegroundColor Cyan
Write-Host ""

Write-Host "Creating test directory..." -ForegroundColor Yellow
Remove-Item $TestDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item $TestDir -ItemType Directory | Out-Null
Set-Location $TestDir

Write-Host "Clearing NuGet cache..." -ForegroundColor Yellow
dotnet nuget locals all --clear | Out-Null

Write-Host "Creating console app..." -ForegroundColor Yellow
dotnet new console --name TestNuGetOrg --force | Out-Null
Set-Location TestNuGetOrg

Write-Host ""
Write-Host "Installing from NuGet.org ONLY (not local sources)..." -ForegroundColor Yellow
Write-Host "Command: dotnet add package Databento.Client --prerelease --source https://api.nuget.org/v3/index.json" -ForegroundColor Gray
Write-Host ""

$output = dotnet add package Databento.Client --prerelease --source https://api.nuget.org/v3/index.json 2>&1
$version = ($output | Select-String "version '([^']+)'" | % { $_.Matches.Groups[1].Value })

Write-Host "Installed version: $version" -ForegroundColor Cyan
Write-Host ""

Write-Host "Building..." -ForegroundColor Yellow
$buildResult = dotnet build --verbosity quiet 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Find output directory
$framework = (Get-ChildItem "bin\Debug")[0].Name
$outDir = "bin\Debug\$framework"

Write-Host "Checking VC++ DLLs in: $outDir" -ForegroundColor Yellow
Write-Host ""

# Check for VC++ DLLs
$vcDlls = @("msvcp140.dll", "vcruntime140.dll", "vcruntime140_1.dll")
$allFound = $true

foreach ($dll in $vcDlls) {
    $path = Join-Path $outDir $dll
    if (Test-Path $path) {
        $size = [math]::Round((Get-Item $path).Length / 1KB)
        Write-Host "  [OK] $dll ($size KB)" -ForegroundColor Green
    } else {
        Write-Host "  [MISSING] $dll" -ForegroundColor Red -BackgroundColor Yellow
        $allFound = $false
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan

if ($allFound) {
    Write-Host "RESULT: PASS" -ForegroundColor Black -BackgroundColor Green
    Write-Host "All VC++ DLLs are present in NuGet.org version $version" -ForegroundColor Green
} else {
    Write-Host "RESULT: FAIL" -ForegroundColor White -BackgroundColor Red
    Write-Host "VC++ DLLs are MISSING in NuGet.org version $version" -ForegroundColor Red
    Write-Host ""
    Write-Host "This means users installing from NuGet.org WILL experience DllNotFoundException!" -ForegroundColor Red
}

Write-Host ""
Write-Host "Test directory: $TestDir"
