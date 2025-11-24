#!/usr/bin/env pwsh
# Run all example projects and generate test report
# v3.0.27-beta validation

param(
    [int]$TimeoutSeconds = 30
)

$ErrorActionPreference = "Continue"
$timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
$reportFile = "TEST_REPORT_v3.0.27-beta_$timestamp.md"

# Find all example projects
$exampleProjects = Get-ChildItem -Path "examples" -Filter "*.csproj" -Recurse |
    Where-Object { $_.Directory.Name -notlike "obj" -and $_.Directory.Name -notlike "bin" } |
    Sort-Object FullName

$totalProjects = $exampleProjects.Count
$passedProjects = 0
$failedProjects = 0
$skippedProjects = 0
$results = @()

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "Running All Examples - v3.0.27-beta Validation" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "Total projects found: $totalProjects"
Write-Host "Timeout per project: $TimeoutSeconds seconds"
Write-Host ""

foreach ($project in $exampleProjects) {
    $projectName = $project.Directory.Name
    $projectPath = $project.Directory.FullName
    $relativeDir = $project.Directory.FullName.Replace($PWD.Path + "\", "")

    Write-Host "[$($results.Count + 1)/$totalProjects] Testing: $projectName" -ForegroundColor Yellow

    $result = [PSCustomObject]@{
        Number = $results.Count + 1
        Name = $projectName
        Path = $relativeDir
        Status = "Unknown"
        ExitCode = $null
        Duration = $null
        Output = ""
        Error = ""
        Notes = ""
    }

    # Skip certain projects
    $skipProjects = @("ApiTests.Internal", "TestsScratchpad.Internal", "DiagnosticTest", "DiagnosticTest2")
    if ($skipProjects -contains $projectName) {
        Write-Host "  SKIPPED (internal/diagnostic project)" -ForegroundColor Gray
        $result.Status = "Skipped"
        $result.Notes = "Internal/diagnostic project"
        $skippedProjects++
        $results += $result
        continue
    }

    try {
        $startTime = Get-Date

        # Run the project with timeout
        $process = Start-Process -FilePath "dotnet" `
            -ArgumentList "run --configuration Release" `
            -WorkingDirectory $projectPath `
            -NoNewWindow `
            -PassThru `
            -RedirectStandardOutput "$env:TEMP\example_stdout.txt" `
            -RedirectStandardError "$env:TEMP\example_stderr.txt"

        $completed = $process.WaitForExit($TimeoutSeconds * 1000)
        $endTime = Get-Date
        $duration = ($endTime - $startTime).TotalSeconds

        if (-not $completed) {
            $process.Kill()
            Write-Host "  TIMEOUT (exceeded $TimeoutSeconds seconds)" -ForegroundColor Magenta
            $result.Status = "Timeout"
            $result.Duration = $duration
            $result.Notes = "Exceeded $TimeoutSeconds second timeout"
            $failedProjects++
        } else {
            $exitCode = $process.ExitCode
            $stdout = Get-Content "$env:TEMP\example_stdout.txt" -Raw -ErrorAction SilentlyContinue
            $stderr = Get-Content "$env:TEMP\example_stderr.txt" -Raw -ErrorAction SilentlyContinue

            $result.ExitCode = $exitCode
            $result.Duration = [math]::Round($duration, 2)
            $result.Output = if ($stdout) { $stdout.Substring(0, [Math]::Min(500, $stdout.Length)) } else { "" }
            $result.Error = if ($stderr) { $stderr.Substring(0, [Math]::Min(500, $stderr.Length)) } else { "" }

            if ($exitCode -eq 0) {
                Write-Host "  PASS (exit code 0, ${duration}s)" -ForegroundColor Green
                $result.Status = "Pass"
                $passedProjects++
            } else {
                Write-Host "  FAIL (exit code $exitCode, ${duration}s)" -ForegroundColor Red
                $result.Status = "Fail"
                $result.Notes = "Exit code $exitCode"
                $failedProjects++
            }
        }
    } catch {
        Write-Host "  ERROR: $($_.Exception.Message)" -ForegroundColor Red
        $result.Status = "Error"
        $result.Error = $_.Exception.Message
        $failedProjects++
    }

    $results += $result
}

# Generate markdown report
$reportLines = @()
$reportLines += "# Test Report: v3.0.27-beta - All Examples"
$reportLines += "**Date**: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
$reportLines += "**Version**: 3.0.27-beta"
$reportLines += ""
$reportLines += "## Executive Summary"
$reportLines += ""
$reportLines += "| Metric | Count | Percentage |"
$reportLines += "|--------|-------|------------|"
$reportLines += "| **Total Projects** | $totalProjects | 100% |"
$reportLines += "| **Passed** | $passedProjects | $([math]::Round(($passedProjects/$totalProjects)*100, 1))% |"
$reportLines += "| **Failed** | $failedProjects | $([math]::Round(($failedProjects/$totalProjects)*100, 1))% |"
$reportLines += "| **Skipped** | $skippedProjects | $([math]::Round(($skippedProjects/$totalProjects)*100, 1))% |"
$reportLines += ""
$reportLines += "## Critical Fixes Validated"
$reportLines += ""
$reportLines += "### Issue #1: AccessViolationException with Future Dates"
$issue1Result = ($results | Where-Object { $_.Name -eq "HistoricalFutureDates.Test" })
$reportLines += "- **Status**: $(if ($issue1Result.Status -eq 'Pass') { 'VERIFIED' } else { 'NOT VERIFIED' })"
$reportLines += "- **Test Project**: HistoricalFutureDates.Test"
$reportLines += "- **Result**: $($issue1Result.Status)"
$reportLines += ""
$reportLines += "### Issue #4: InstrumentDefMessage.InstrumentClass Always 0"
$issue4Result = ($results | Where-Object { $_.Name -eq "AllSymbolsInstrumentClass.Example" })
$reportLines += "- **Status**: $(if ($issue4Result.Status -eq 'Pass') { 'VERIFIED' } else { 'NOT VERIFIED' })"
$reportLines += "- **Test Project**: AllSymbolsInstrumentClass.Example"
$reportLines += "- **Result**: $($issue4Result.Status)"
$reportLines += ""
$reportLines += "## Detailed Results"
$reportLines += ""

foreach ($result in $results) {
    $statusIcon = switch ($result.Status) {
        "Pass" { "PASS" }
        "Fail" { "FAIL" }
        "Timeout" { "TIMEOUT" }
        "Skipped" { "SKIP" }
        "Error" { "ERROR" }
        default { "UNKNOWN" }
    }

    $reportLines += "### [$($result.Number)] $statusIcon - $($result.Name)"
    $reportLines += "- Status: $($result.Status)"
    $reportLines += "- Path: $($result.Path)"
    $reportLines += "- Duration: $($result.Duration)s"
    $reportLines += "- Exit Code: $($result.ExitCode)"

    if ($result.Notes) {
        $reportLines += "- Notes: $($result.Notes)"
    }

    $reportLines += ""
}

$reportLines += "---"
$reportLines += ""
$reportLines += "## Summary by Status"
$reportLines += ""
$reportLines += "### Passed Projects ($passedProjects)"
$reportLines += ""
$passedList = $results | Where-Object { $_.Status -eq "Pass" }
if ($passedList) {
    foreach ($p in $passedList) {
        $reportLines += "- $($p.Name)"
    }
} else {
    $reportLines += "None"
}
$reportLines += ""

$reportLines += "### Failed Projects ($failedProjects)"
$reportLines += ""
$failedList = $results | Where-Object { $_.Status -in @("Fail", "Timeout", "Error") }
if ($failedList) {
    foreach ($f in $failedList) {
        $reportLines += "- $($f.Name) ($($f.Status))"
    }
} else {
    $reportLines += "None"
}
$reportLines += ""

$reportLines += "### Skipped Projects ($skippedProjects)"
$reportLines += ""
$skippedList = $results | Where-Object { $_.Status -eq "Skipped" }
if ($skippedList) {
    foreach ($s in $skippedList) {
        $reportLines += "- $($s.Name)"
    }
} else {
    $reportLines += "None"
}
$reportLines += ""
$reportLines += "---"
$reportLines += ""
$reportLines += "## Conclusion"
$reportLines += ""
$reportLines += "**Overall Status**: $(if ($failedProjects -eq 0) { 'ALL TESTS PASSED' } else { "$failedProjects TESTS FAILED" })"
$reportLines += ""
$reportLines += "The v3.0.27-beta release has been validated with $passedProjects/$($totalProjects - $skippedProjects) examples passing."
$reportLines += ""
$reportLines += "### Critical Fixes Status:"
$reportLines += "- Issue #1 (AccessViolationException): $(if ($issue1Result.Status -eq 'Pass') { 'FIXED' } else { 'NOT VERIFIED' })"
$reportLines += "- Issue #4 (InstrumentClass field): $(if ($issue4Result.Status -eq 'Pass') { 'FIXED' } else { 'NOT VERIFIED' })"
$reportLines += ""
$reportLines += "**Recommendation**: $(if ($failedProjects -eq 0) { 'Ready for release' } else { 'Review failures before release' })"

# Save report
$reportLines | Out-File -FilePath $reportFile -Encoding UTF8

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "Test Run Complete" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "Total:   $totalProjects projects" -ForegroundColor White
Write-Host "Passed:  $passedProjects projects" -ForegroundColor Green
Write-Host "Failed:  $failedProjects projects" -ForegroundColor Red
Write-Host "Skipped: $skippedProjects projects" -ForegroundColor Gray
Write-Host ""
Write-Host "Report saved to: $reportFile" -ForegroundColor Cyan
Write-Host ""
