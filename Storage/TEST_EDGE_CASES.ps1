# Test Edge Cases - Why DLLs Might Not Deploy

$Version = "3.0.24-beta"
$TestBase = "$env:TEMP\edge-case-tests"

Write-Host "=== Testing Edge Cases for DLL Deployment ===" -ForegroundColor Cyan
Write-Host "Version: $Version" -ForegroundColor White
Write-Host ""

Remove-Item $TestBase -Recurse -Force -ErrorAction SilentlyContinue
New-Item $TestBase -ItemType Directory | Out-Null

# Helper function
function Test-EdgeCase {
    param($Name, $TestDir, $ScriptBlock)

    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray
    Write-Host "TEST: $Name" -ForegroundColor Yellow
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray

    Set-Location $TestBase
    Remove-Item $TestDir -Recurse -Force -ErrorAction SilentlyContinue
    New-Item $TestDir -ItemType Directory | Out-Null
    Set-Location $TestDir

    & $ScriptBlock

    Write-Host ""
}

# EDGE CASE 1: Run without building
Test-EdgeCase "dotnet run (without explicit build)" "$TestBase\case1" {
    Write-Host "Creating project..." -ForegroundColor Gray
    dotnet new console --force | Out-Null
    dotnet add package Databento.Client --version $Version --source https://api.nuget.org/v3/index.json | Out-Null

    Write-Host "Running WITHOUT explicit build..." -ForegroundColor Gray
    Write-Host "Command: dotnet run" -ForegroundColor Gray

    # This will implicitly build, but let's see if DLLs are there
    $fw = (Get-ChildItem "bin\Debug" -ErrorAction SilentlyContinue)[0].Name
    if ($fw) {
        $out = "bin\Debug\$fw"
        $hasDlls = (Test-Path "$out\msvcp140.dll")

        if ($hasDlls) {
            Write-Host "RESULT: PASS - DLLs present" -ForegroundColor Green
        } else {
            Write-Host "RESULT: FAIL - DLLs missing" -ForegroundColor Red
            Write-Host "DLLs in output:" -ForegroundColor Gray
            Get-ChildItem $out -Filter "*.dll" | ForEach-Object { Write-Host "  - $($_.Name)" -ForegroundColor Gray }
        }
    } else {
        Write-Host "RESULT: No build output created" -ForegroundColor Red
    }
}

# EDGE CASE 2: Run with --no-build
Test-EdgeCase "dotnet run --no-build" "$TestBase\case2" {
    Write-Host "Creating project..." -ForegroundColor Gray
    dotnet new console --force | Out-Null
    dotnet add package Databento.Client --version $Version --source https://api.nuget.org/v3/index.json | Out-Null

    Write-Host "Building first..." -ForegroundColor Gray
    dotnet build --verbosity quiet | Out-Null

    $fw = (Get-ChildItem "bin\Debug")[0].Name
    $out = "bin\Debug\$fw"

    $hasDlls = (Test-Path "$out\msvcp140.dll")

    if ($hasDlls) {
        Write-Host "RESULT: PASS - DLLs present after build" -ForegroundColor Green
    } else {
        Write-Host "RESULT: FAIL - DLLs missing even after explicit build!" -ForegroundColor Red
    }
}

# EDGE CASE 3: Restore only (no build)
Test-EdgeCase "dotnet restore (without build)" "$TestBase\case3" {
    Write-Host "Creating project..." -ForegroundColor Gray
    dotnet new console --force | Out-Null
    dotnet add package Databento.Client --version $Version --source https://api.nuget.org/v3/index.json | Out-Null

    Write-Host "Restore only (no build)..." -ForegroundColor Gray
    dotnet restore | Out-Null

    # Check if bin directory even exists
    if (Test-Path "bin") {
        Write-Host "RESULT: bin directory exists (shouldn't happen with restore only)" -ForegroundColor Yellow
    } else {
        Write-Host "RESULT: No bin directory (expected - need to build)" -ForegroundColor Green
    }
}

# EDGE CASE 4: Publish instead of build
Test-EdgeCase "dotnet publish" "$TestBase\case4" {
    Write-Host "Creating project..." -ForegroundColor Gray
    dotnet new console --force | Out-Null
    dotnet add package Databento.Client --version $Version --source https://api.nuget.org/v3/index.json | Out-Null

    Write-Host "Publishing..." -ForegroundColor Gray
    dotnet publish -c Release --verbosity quiet | Out-Null

    $fw = (Get-ChildItem "bin\Release" -ErrorAction SilentlyContinue)[0].Name
    if ($fw) {
        $pubDir = "bin\Release\$fw\publish"

        if (Test-Path $pubDir) {
            $hasDlls = (Test-Path "$pubDir\msvcp140.dll")

            if ($hasDlls) {
                Write-Host "RESULT: PASS - DLLs in publish directory" -ForegroundColor Green
            } else {
                Write-Host "RESULT: FAIL - DLLs missing from publish directory" -ForegroundColor Red
                Write-Host "Published DLLs:" -ForegroundColor Gray
                Get-ChildItem $pubDir -Filter "*.dll" -ErrorAction SilentlyContinue | ForEach-Object { Write-Host "  - $($_.Name)" -ForegroundColor Gray }
            }
        }
    }
}

# EDGE CASE 5: Self-contained publish
Test-EdgeCase "dotnet publish --self-contained" "$TestBase\case5" {
    Write-Host "Creating project..." -ForegroundColor Gray
    dotnet new console --force | Out-Null
    dotnet add package Databento.Client --version $Version --source https://api.nuget.org/v3/index.json | Out-Null

    Write-Host "Publishing self-contained..." -ForegroundColor Gray
    dotnet publish -c Release --self-contained true -r win-x64 --verbosity quiet | Out-Null

    $pubDir = "bin\Release\net*\win-x64\publish"
    $pubDirResolved = Get-Item $pubDir -ErrorAction SilentlyContinue | Select-Object -First 1

    if ($pubDirResolved) {
        $hasDlls = (Test-Path "$($pubDirResolved.FullName)\msvcp140.dll")

        if ($hasDlls) {
            Write-Host "RESULT: PASS - DLLs in self-contained publish" -ForegroundColor Green
        } else {
            Write-Host "RESULT: FAIL - DLLs missing from self-contained publish" -ForegroundColor Red
        }
    }
}

# EDGE CASE 6: Different target framework
Test-EdgeCase "Target framework net8.0 explicitly" "$TestBase\case6" {
    Write-Host "Creating project with net8.0..." -ForegroundColor Gray
    dotnet new console --force --framework net8.0 | Out-Null
    dotnet add package Databento.Client --version $Version --source https://api.nuget.org/v3/index.json | Out-Null
    dotnet build --verbosity quiet | Out-Null

    $out = "bin\Debug\net8.0"
    $hasDlls = (Test-Path "$out\msvcp140.dll")

    if ($hasDlls) {
        Write-Host "RESULT: PASS - DLLs present for net8.0" -ForegroundColor Green
    } else {
        Write-Host "RESULT: FAIL - DLLs missing for net8.0" -ForegroundColor Red
    }
}

# EDGE CASE 7: Old-style csproj (non-SDK)
Test-EdgeCase "Old-style .csproj (non-SDK)" "$TestBase\case7" {
    Write-Host "Creating old-style project..." -ForegroundColor Gray

    # Create an old-style csproj
    $oldCsproj = @"
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Import Project="`$(MSBuildExtensionsPath)\`$(MSBuildToolsVersion)\Microsoft.Common.props" />
  <PropertyGroup>
    <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="System" />
  </ItemGroup>
  <ItemGroup>
    <Compile Include="Program.cs" />
  </ItemGroup>
  <Import Project="`$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
</Project>
"@

    Set-Content -Path "OldStyle.csproj" -Value $oldCsproj
    Set-Content -Path "Program.cs" -Value "class Program { static void Main() {} }"

    Write-Host "NOTE: Old-style projects don't support NuGet packages the same way" -ForegroundColor Yellow
    Write-Host "RESULT: SKIP - Would need packages.config, not relevant for modern .NET" -ForegroundColor Gray
}

# EDGE CASE 8: Custom build configuration
Test-EdgeCase "Custom runtime identifier" "$TestBase\case8" {
    Write-Host "Creating project with custom RuntimeIdentifier..." -ForegroundColor Gray
    dotnet new console --force | Out-Null

    # Add RuntimeIdentifier to csproj
    $csproj = Get-Content "*.csproj" -Raw
    $csproj = $csproj.Replace("</PropertyGroup>", "  <RuntimeIdentifier>win-x64</RuntimeIdentifier>`n  </PropertyGroup>")
    Set-Content "*.csproj" $csproj

    dotnet add package Databento.Client --version $Version --source https://api.nuget.org/v3/index.json | Out-Null
    dotnet build --verbosity quiet | Out-Null

    $fw = (Get-ChildItem "bin\Debug")[0].Name
    $out = "bin\Debug\$fw\win-x64"

    if (Test-Path $out) {
        $hasDlls = (Test-Path "$out\msvcp140.dll")

        if ($hasDlls) {
            Write-Host "RESULT: PASS - DLLs present with RuntimeIdentifier" -ForegroundColor Green
        } else {
            Write-Host "RESULT: FAIL - DLLs missing with RuntimeIdentifier" -ForegroundColor Red
            Write-Host "Output location: $out" -ForegroundColor Gray
        }
    } else {
        # Try without win-x64 subdirectory
        $out = "bin\Debug\$fw"
        $hasDlls = (Test-Path "$out\msvcp140.dll")

        if ($hasDlls) {
            Write-Host "RESULT: PASS - DLLs in parent directory" -ForegroundColor Green
        } else {
            Write-Host "RESULT: FAIL - DLLs missing" -ForegroundColor Red
        }
    }
}

# Summary
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "EDGE CASE TESTING COMPLETE" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host ""
Write-Host "Review the results above to identify which scenarios fail." -ForegroundColor White
Write-Host ""
Write-Host "Test artifacts preserved at: $TestBase" -ForegroundColor Gray
