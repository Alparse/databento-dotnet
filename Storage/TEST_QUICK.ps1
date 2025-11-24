# Quick Test - Databento.Client VC++ DLLs Present

$TestDir = "$env:TEMP\databento-quick-test"

Write-Host "Creating test project..." -ForegroundColor Cyan
Remove-Item $TestDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item $TestDir -ItemType Directory | Out-Null
Set-Location $TestDir

Write-Host "Clearing NuGet cache..." -ForegroundColor Cyan
dotnet nuget locals all --clear | Out-Null

Write-Host "Creating console app..." -ForegroundColor Cyan
dotnet new console --name Test --force | Out-Null
Set-Location Test

Write-Host "Installing Databento.Client..." -ForegroundColor Cyan
$output = dotnet add package Databento.Client --prerelease 2>&1
$version = ($output | Select-String "version '([^']+)'" | % { $_.Matches.Groups[1].Value })
Write-Host "Version: $version" -ForegroundColor Yellow

Write-Host "Building..." -ForegroundColor Cyan
dotnet build --verbosity quiet | Out-Null

# Find output directory
$framework = (Get-ChildItem "bin\Debug")[0].Name
$outDir = "bin\Debug\$framework"

Write-Host "`nChecking DLLs in: $outDir" -ForegroundColor Cyan
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
        Write-Host "  [MISSING] $dll" -ForegroundColor Red
        $allFound = $false
    }
}

Write-Host ""
if ($allFound) {
    Write-Host "RESULT: PASS - All VC++ DLLs present" -ForegroundColor Green -BackgroundColor Black
} else {
    Write-Host "RESULT: FAIL - Some VC++ DLLs missing" -ForegroundColor Red -BackgroundColor Black
}

Write-Host "`nTest directory: $TestDir"
