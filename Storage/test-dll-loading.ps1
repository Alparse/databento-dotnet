# Advanced Test - Verify Which DLLs Are Actually Loaded at Runtime
# This script creates a test application, runs it, and checks which VC++ DLLs are loaded

Write-Host "=== Databento.Client DLL Loading Test ===" -ForegroundColor Cyan
Write-Host "This test verifies which VC++ runtime DLLs are actually loaded at runtime" -ForegroundColor Gray
Write-Host ""

# Test configuration
$TestDir = "$env:TEMP\databento-dll-loading-test-$(Get-Date -Format 'yyyyMMdd-HHmmss')"

Write-Host "Creating test project..." -ForegroundColor Yellow
New-Item -Path $TestDir -ItemType Directory -Force | Out-Null
Set-Location $TestDir

# Create test project
dotnet new console --force --name DllLoadingTest | Out-Null
Set-Location DllLoadingTest

# Install package
Write-Host "Installing Databento.Client..." -ForegroundColor Yellow
dotnet add package Databento.Client --prerelease | Out-Null

# Create test program that loads the native library
$testProgram = @'
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

class Program
{
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    static extern uint GetModuleFileName(IntPtr hModule, System.Text.StringBuilder lpFilename, int nSize);

    static void Main()
    {
        Console.WriteLine("=== DLL Loading Diagnostic ===");
        Console.WriteLine();

        // Try to trigger loading of databento_native.dll
        Console.WriteLine("Attempting to load Databento native library...");

        try
        {
            // This will attempt to load databento_native.dll and its dependencies
            var builder = new Databento.Client.HistoricalClientBuilder();
            Console.WriteLine("✓ HistoricalClientBuilder created successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to create builder: {ex.GetType().Name}");
            Console.WriteLine($"  Message: {ex.Message}");
        }

        Console.WriteLine();
        Console.WriteLine("Checking loaded VC++ runtime DLLs:");
        Console.WriteLine();

        // Check which VC++ DLLs are loaded and from where
        string[] vcDlls = { "msvcp140.dll", "vcruntime140.dll", "vcruntime140_1.dll" };

        foreach (var dllName in vcDlls)
        {
            IntPtr handle = GetModuleHandle(dllName);

            if (handle != IntPtr.Zero)
            {
                var path = new System.Text.StringBuilder(260);
                uint result = GetModuleFileName(handle, path, path.Capacity);

                if (result > 0)
                {
                    string fullPath = path.ToString();
                    string directory = System.IO.Path.GetDirectoryName(fullPath);
                    string currentDir = System.IO.Directory.GetCurrentDirectory();

                    Console.WriteLine($"✓ {dllName}");
                    Console.WriteLine($"  Path: {fullPath}");

                    // Check if it's our bundled DLL (in current directory)
                    if (directory.Equals(currentDir, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"  Source: BUNDLED (from output directory)");
                        Console.ResetColor();
                    }
                    else if (directory.Contains("System32", StringComparison.OrdinalIgnoreCase) ||
                             directory.Contains("SysWOW64", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"  Source: SYSTEM (from Windows directory)");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"  Source: OTHER ({directory})");
                        Console.ResetColor();
                    }
                }
                else
                {
                    Console.WriteLine($"✓ {dllName} - Loaded but path unknown");
                }
            }
            else
            {
                Console.WriteLine($"✗ {dllName} - Not loaded");
            }

            Console.WriteLine();
        }

        // Show current directory for reference
        Console.WriteLine($"Current Directory: {System.IO.Directory.GetCurrentDirectory()}");
        Console.WriteLine($"Output Directory DLLs:");

        foreach (var dllName in vcDlls)
        {
            string localPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), dllName);
            if (System.IO.File.Exists(localPath))
            {
                var fileInfo = new System.IO.FileInfo(localPath);
                Console.WriteLine($"  ✓ {dllName} ({fileInfo.Length / 1024} KB)");
            }
            else
            {
                Console.WriteLine($"  ✗ {dllName} - NOT FOUND IN OUTPUT");
            }
        }
    }
}
'@

Write-Host "Creating test program..." -ForegroundColor Yellow
Set-Content -Path "Program.cs" -Value $testProgram

# Build
Write-Host "Building..." -ForegroundColor Yellow
$buildOutput = dotnet build --verbosity quiet 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    Write-Host $buildOutput
    exit 1
}

# Detect output directory
$targetFramework = (Get-ChildItem "bin\Debug" -Directory)[0].Name
$outputDir = "bin\Debug\$targetFramework"

Write-Host "Running test program..." -ForegroundColor Yellow
Write-Host "Output directory: $outputDir" -ForegroundColor Gray
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan

# Run the test
Set-Location $outputDir
$runOutput = dotnet DllLoadingTest.dll 2>&1
Write-Host $runOutput

Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Analysis
Write-Host "Analysis:" -ForegroundColor Yellow
Write-Host ""

$usingBundled = $runOutput | Select-String "BUNDLED"
$usingSystem = $runOutput | Select-String "SYSTEM"

if ($usingBundled) {
    Write-Host "✅ Application is loading BUNDLED VC++ runtime DLLs" -ForegroundColor Green
    Write-Host "   This means the fix is working - users without system VC++ can run the app" -ForegroundColor Green
} elseif ($usingSystem) {
    Write-Host "⚠️  Application is loading SYSTEM VC++ runtime DLLs" -ForegroundColor Yellow
    Write-Host "   This could mean:" -ForegroundColor Yellow
    Write-Host "   1. Bundled DLLs exist but Windows prefers system DLLs (unusual)" -ForegroundColor Yellow
    Write-Host "   2. Bundled DLLs don't exist in output directory (BAD)" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "   Check if bundled DLLs exist in output directory above." -ForegroundColor Yellow
} else {
    Write-Host "⚠️  Could not determine DLL source" -ForegroundColor Yellow
    Write-Host "   Check the output above for details" -ForegroundColor Yellow
}

Write-Host ""

# Cleanup prompt
Write-Host "Test directory: $TestDir" -ForegroundColor Gray
Write-Host "Keep test directory? [y/N]: " -ForegroundColor Yellow -NoNewline
$keep = Read-Host

if ($keep -ne 'y' -and $keep -ne 'Y') {
    Set-Location $env:TEMP
    Remove-Item -Path $TestDir -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "Cleaned up." -ForegroundColor Green
}

Write-Host ""
Write-Host "Test complete!" -ForegroundColor Cyan
