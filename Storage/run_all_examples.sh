#!/bin/bash
# Run all example projects and generate test report
# v3.0.27-beta validation

TIMEOUT_SECONDS=45
timestamp=$(date +%Y-%m-%d_%H-%M-%S)
report_file="TEST_REPORT_v3.0.27-beta_$timestamp.md"

# Find all example projects
mapfile -t examples < <(find examples -name "*.csproj" -not -path "*/obj/*" -not -path "*/bin/*" | sort)

total_projects=${#examples[@]}
passed_projects=0
failed_projects=0
skipped_projects=0
timeout_projects=0

# Arrays to store results
declare -a results_names
declare -a results_statuses
declare -a results_durations
declare -a results_exitcodes

echo "================================================================"
echo "Running All Examples - v3.0.27-beta Validation"
echo "================================================================"
echo "Total projects found: $total_projects"
echo "Timeout per project: $TIMEOUT_SECONDS seconds"
echo ""

# Skip list
skip_list=("ApiTests.Internal" "TestsScratchpad.Internal" "DiagnosticTest" "DiagnosticTest2")

count=0
for project_path in "${examples[@]}"; do
    count=$((count + 1))
    project_dir=$(dirname "$project_path")
    project_name=$(basename "$project_dir")

    echo "[$count/$total_projects] Testing: $project_name"

    # Check if should skip
    should_skip=false
    for skip_item in "${skip_list[@]}"; do
        if [[ "$project_name" == "$skip_item" ]]; then
            should_skip=true
            break
        fi
    done

    if [ "$should_skip" = true ]; then
        echo "  SKIP (internal/diagnostic project)"
        results_names+=("$project_name")
        results_statuses+=("Skipped")
        results_durations+=("0")
        results_exitcodes+=("")
        skipped_projects=$((skipped_projects + 1))
        continue
    fi

    # Run with timeout
    start_time=$(date +%s)
    timeout ${TIMEOUT_SECONDS}s bash -c "cd '$project_dir' && dotnet run --configuration Release > /tmp/example_out_$count.txt 2>&1"
    exit_code=$?
    end_time=$(date +%s)
    duration=$((end_time - start_time))

    results_names+=("$project_name")
    results_durations+=("$duration")

    if [ $exit_code -eq 124 ]; then
        # Timeout
        echo "  TIMEOUT (exceeded $TIMEOUT_SECONDS seconds)"
        results_statuses+=("Timeout")
        results_exitcodes+=("124")
        timeout_projects=$((timeout_projects + 1))
        failed_projects=$((failed_projects + 1))
    elif [ $exit_code -eq 0 ]; then
        echo "  PASS (exit code 0, ${duration}s)"
        results_statuses+=("Pass")
        results_exitcodes+=("0")
        passed_projects=$((passed_projects + 1))
    else
        echo "  FAIL (exit code $exit_code, ${duration}s)"
        results_statuses+=("Fail")
        results_exitcodes+=("$exit_code")
        failed_projects=$((failed_projects + 1))
    fi
done

echo ""
echo "================================================================"
echo "Test Run Complete"
echo "================================================================"
echo "Total:   $total_projects projects"
echo "Passed:  $passed_projects projects"
echo "Failed:  $failed_projects projects (including $timeout_projects timeouts)"
echo "Skipped: $skipped_projects projects"
echo ""

# Generate report
{
    echo "# Test Report: v3.0.27-beta - All Examples"
    echo "**Date**: $(date '+%Y-%m-%d %H:%M:%S')"
    echo "**Version**: 3.0.27-beta"
    echo ""
    echo "## Executive Summary"
    echo ""
    echo "| Metric | Count | Percentage |"
    echo "|--------|-------|------------|"
    echo "| **Total Projects** | $total_projects | 100% |"
    printf "| **Passed** | %d | %.1f%% |\n" "$passed_projects" "$(echo "scale=1; $passed_projects * 100 / $total_projects" | bc)"
    printf "| **Failed** | %d | %.1f%% |\n" "$failed_projects" "$(echo "scale=1; $failed_projects * 100 / $total_projects" | bc)"
    printf "| **Skipped** | %d | %.1f%% |\n" "$skipped_projects" "$(echo "scale=1; $skipped_projects * 100 / $total_projects" | bc)"
    echo ""

    # Check critical fixes
    issue1_status="NOT VERIFIED"
    issue4_status="NOT VERIFIED"
    for i in "${!results_names[@]}"; do
        if [[ "${results_names[$i]}" == "HistoricalFutureDates.Test" ]] && [[ "${results_statuses[$i]}" == "Pass" ]]; then
            issue1_status="✅ VERIFIED"
        fi
        if [[ "${results_names[$i]}" == "AllSymbolsInstrumentClass.Example" ]] && [[ "${results_statuses[$i]}" == "Pass" ]]; then
            issue4_status="✅ VERIFIED"
        fi
    done

    echo "## Critical Fixes Validated"
    echo ""
    echo "### Issue #1: AccessViolationException with Future Dates"
    echo "- **Status**: $issue1_status"
    echo "- **Test Project**: HistoricalFutureDates.Test"
    echo ""
    echo "### Issue #4: InstrumentDefMessage.InstrumentClass Always 0"
    echo "- **Status**: $issue4_status"
    echo "- **Test Project**: AllSymbolsInstrumentClass.Example"
    echo ""
    echo "## Detailed Results"
    echo ""

    for i in "${!results_names[@]}"; do
        num=$((i + 1))
        name="${results_names[$i]}"
        status="${results_statuses[$i]}"
        duration="${results_durations[$i]}"
        exitcode="${results_exitcodes[$i]}"

        echo "### [$num] $status - $name"
        echo "- Status: $status"
        echo "- Duration: ${duration}s"
        echo "- Exit Code: $exitcode"
        echo ""
    done

    echo "---"
    echo ""
    echo "## Conclusion"
    echo ""
    if [ $failed_projects -eq 0 ]; then
        echo "**Overall Status**: ✅ ALL TESTS PASSED"
    else
        echo "**Overall Status**: ⚠️ $failed_projects TESTS FAILED"
    fi
    echo ""
    echo "The v3.0.27-beta release has been validated with $passed_projects/$((total_projects - skipped_projects)) examples passing."
    echo ""
    echo "### Critical Fixes Status:"
    echo "- Issue #1 (AccessViolationException): $issue1_status"
    echo "- Issue #4 (InstrumentClass field): $issue4_status"
    echo ""
    if [ $failed_projects -eq 0 ]; then
        echo "**Recommendation**: Ready for release"
    else
        echo "**Recommendation**: Review failures before release"
    fi
} > "$report_file"

echo "Report saved to: $report_file"
echo ""
