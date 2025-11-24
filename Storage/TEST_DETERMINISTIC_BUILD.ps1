# Test Deterministic Build with CI flag
# Builds package twice with CI=true and compares hashes

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "====================================" -ForegroundColor Cyan
Write-Host "Deterministic Build Test" -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Building package twice with CI flag..." -ForegroundColor Yellow
Write-Host ""

# Set CI environment variable
$env:CI = "true"

# Build 1
Write-Host "[1/2] First build..." -ForegroundColor Gray
dotnet clean -c Release --verbosity quiet | Out-Null
dotnet pack src\Databento.Client\Databento.Client.csproj -c Release --verbosity quiet | Out-Null
$pkg1 = "src\Databento.Client\bin\Release\Databento.Client.3.0.24-beta.nupkg"
$hash1 = (Get-FileHash $pkg1 -Algorithm SHA256).Hash
Write-Host "  Hash: $hash1" -ForegroundColor White

# Build 2
Write-Host ""
Write-Host "[2/2] Second build..." -ForegroundColor Gray
dotnet clean -c Release --verbosity quiet | Out-Null
dotnet pack src\Databento.Client\Databento.Client.csproj -c Release --verbosity quiet | Out-Null
$pkg2 = "src\Databento.Client\bin\Release\Databento.Client.3.0.24-beta.nupkg"
$hash2 = (Get-FileHash $pkg2 -Algorithm SHA256).Hash
Write-Host "  Hash: $hash2" -ForegroundColor White

Write-Host ""
Write-Host "====================================" -ForegroundColor Cyan

if ($hash1 -eq $hash2) {
    Write-Host "DETERMINISTIC BUILD: SUCCESS" -ForegroundColor Green
    Write-Host ""
    Write-Host "Both builds produced IDENTICAL packages!" -ForegroundColor Green
    Write-Host "This is critical for:" -ForegroundColor White
    Write-Host "  * Build reproducibility" -ForegroundColor Gray
    Write-Host "  * Security verification" -ForegroundColor Gray
    Write-Host "  * Supply chain trust" -ForegroundColor Gray
} else {
    Write-Host "DETERMINISTIC BUILD: FAILED" -ForegroundColor Red
    Write-Host ""
    Write-Host "Builds produced different packages" -ForegroundColor Red
    Write-Host "Hash 1: $hash1" -ForegroundColor Gray
    Write-Host "Hash 2: $hash2" -ForegroundColor Gray
}

Write-Host "====================================" -ForegroundColor Cyan
Write-Host ""

# Clear CI flag
Remove-Item Env:\CI -ErrorAction SilentlyContinue
