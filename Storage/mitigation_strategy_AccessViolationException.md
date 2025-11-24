# Mitigation Strategy: AccessViolationException in Native Code

## Problem Summary

databento-cpp crashes with `AccessViolationException` when calling `TimeseriesGetRange()` with invalid parameters. This is a hardware-level exception that bypasses normal exception handling and crashes the entire process.

## Can We Catch AccessViolationException?

**Yes, but with significant caveats.**

### .NET AccessViolationException Handling

By default, .NET **does not allow** catching `AccessViolationException` because it indicates memory corruption that could leave the process in an unstable state. However, .NET provides mechanisms to catch these "corrupted state exceptions" if absolutely necessary.

### Warning ⚠️

Microsoft explicitly warns against catching corrupted state exceptions:
> "An AccessViolationException exception is always thrown by the runtime and is never thrown by application code. Attempting to catch this exception is extremely dangerous because the exception often indicates serious memory corruption. You should not attempt to handle this exception because the application state is unpredictable after such an exception is thrown."

However, for **library code wrapping potentially buggy native code**, catching and reporting these exceptions may be preferable to crashing the entire host application.

---

## Mitigation Options

### Option 1: Catch AccessViolationException with HandleProcessCorruptedStateExceptions ✅ RECOMMENDED

**How it works**: Use the `[HandleProcessCorruptedStateExceptions]` attribute to allow catching corrupted state exceptions.

**Implementation**:

```csharp
using System.Runtime.ExceptionServices;
using System.Security;

public async IAsyncEnumerable<Record> GetRangeAsync(
    string dataset,
    Schema schema,
    IEnumerable<string> symbols,
    DateTimeOffset startTime,
    DateTimeOffset endTime,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    // ... setup code ...

    var queryTask = Task.Run(() =>
    {
        try
        {
            return ExecuteNativeCall(
                dataset, schema.ToSchemaString(), symbolArray,
                startTimeNs, endTimeNs, recordCallback);
        }
        catch (Exception ex)
        {
            // This will catch AccessViolationException if ExecuteNativeCall
            // has the proper attributes
            return ex;
        }
    }, cancellationToken);

    // ... rest of code ...
}

[HandleProcessCorruptedStateExceptions]
[SecurityCritical]
private object ExecuteNativeCall(
    string dataset,
    string schema,
    string[] symbols,
    long startTimeNs,
    long endTimeNs,
    RecordCallbackDelegate callback)
{
    try
    {
        byte[] errorBuffer = new byte[Utilities.Constants.ErrorBufferSize];

        var result = NativeMethods.dbento_historical_get_range(
            _handle,
            dataset,
            schema,
            symbols,
            (nuint)symbols.Length,
            startTimeNs,
            endTimeNs,
            callback,
            IntPtr.Zero,
            errorBuffer,
            (nuint)errorBuffer.Length);

        if (result != 0)
        {
            var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
            return DbentoException.CreateFromErrorCode($"Historical query failed: {error}", result);
        }

        return null; // Success
    }
    catch (AccessViolationException ex)
    {
        _logger?.LogError(ex,
            "Native code crashed with AccessViolationException. " +
            "This is likely a bug in databento-cpp. Dataset={Dataset}, Symbols={Symbols}",
            dataset, string.Join(",", symbols));

        return new DbentoException(
            $"Native library crashed while executing query. This may be caused by invalid parameters " +
            $"(dataset='{dataset}', symbols='{string.Join(",", symbols)}'). " +
            $"Please verify your parameters and report this issue if the problem persists. " +
            $"Technical details: AccessViolationException in databento_historical_get_range",
            -1);
    }
    catch (Exception ex)
    {
        return ex;
    }
}
```

**Pros**:
- ✅ Prevents process crash
- ✅ Provides detailed error message to user
- ✅ Logs exception for debugging
- ✅ Application can continue running
- ✅ Works without requiring user configuration

**Cons**:
- ⚠️ Process state may be corrupted after catching AccessViolationException
- ⚠️ Requires `[SecurityCritical]` attribute (may require full trust in some scenarios)
- ⚠️ Not a true "fix" - just prevents crash

**Risk Assessment**: **MEDIUM**
- The crash happens in isolated native code
- Our managed state should remain intact
- Only the specific historical query fails
- Other operations should continue working

---

### Option 2: Pre-Validation to Avoid Invalid Calls ⚠️ PARTIAL SOLUTION

**How it works**: Validate parameters before calling native code to reduce likelihood of triggering the bug.

**Implementation**:

```csharp
public async IAsyncEnumerable<Record> GetRangeAsync(
    string dataset,
    Schema schema,
    IEnumerable<string> symbols,
    DateTimeOffset startTime,
    DateTimeOffset endTime,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    // Validate parameters before calling native code
    ValidateHistoricalQueryParameters(dataset, symbols, startTime, endTime);

    // ... rest of implementation ...
}

private void ValidateHistoricalQueryParameters(
    string dataset,
    IEnumerable<string> symbols,
    DateTimeOffset startTime,
    DateTimeOffset endTime)
{
    // Check date range (common trigger for bugs)
    var duration = endTime - startTime;
    if (duration.TotalDays > 365)
    {
        throw new ArgumentException(
            $"Date range too large: {duration.TotalDays} days. Maximum recommended range is 365 days.",
            nameof(endTime));
    }

    if (startTime > endTime)
    {
        throw new ArgumentException(
            "Start time must be before end time.",
            nameof(startTime));
    }

    if (endTime > DateTimeOffset.UtcNow)
    {
        throw new ArgumentException(
            "End time cannot be in the future.",
            nameof(endTime));
    }

    // Check symbols (basic validation)
    var symbolArray = symbols.ToArray();
    if (symbolArray.Length == 0)
    {
        throw new ArgumentException("At least one symbol must be provided.", nameof(symbols));
    }

    if (symbolArray.Length > 2000)
    {
        throw new ArgumentException(
            $"Too many symbols: {symbolArray.Length}. Maximum is 2000.",
            nameof(symbols));
    }

    // Validate symbol format (heuristic - may not catch all invalid symbols)
    foreach (var symbol in symbolArray)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol cannot be null or whitespace.", nameof(symbols));
        }

        if (symbol.Length > 64)
        {
            throw new ArgumentException(
                $"Symbol '{symbol}' is too long ({symbol.Length} characters). Maximum is 64.",
                nameof(symbols));
        }
    }
}
```

**Pros**:
- ✅ Catches obvious invalid parameters early
- ✅ Provides clear error messages
- ✅ No risk of corrupted state
- ✅ Fast feedback to users

**Cons**:
- ❌ Cannot detect all invalid symbols without querying API
- ❌ May reject valid parameters (false positives)
- ❌ Doesn't solve the underlying native crash bug
- ❌ Still crashes on unanticipated invalid inputs

**Risk Assessment**: **LOW**
- Safe to implement
- Reduces frequency of crashes
- But doesn't eliminate them

---

### Option 3: Separate Process Isolation 🛡️ NUCLEAR OPTION

**How it works**: Run native calls in a separate process that can crash without taking down the main application.

**Implementation**:

```csharp
// Main process
public async IAsyncEnumerable<Record> GetRangeAsync(...)
{
    var workerProcess = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = "Databento.Worker.exe",
            Arguments = $"--dataset {dataset} --schema {schema} ...",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        }
    };

    workerProcess.Start();

    // Read records from worker process via IPC
    await foreach (var record in ReadRecordsFromProcess(workerProcess))
    {
        yield return record;
    }

    await workerProcess.WaitForExitAsync(cancellationToken);

    if (workerProcess.ExitCode != 0)
    {
        var errorOutput = await workerProcess.StandardError.ReadToEndAsync();
        throw new DbentoException(
            $"Worker process crashed with exit code {workerProcess.ExitCode}: {errorOutput}");
    }
}
```

**Pros**:
- ✅ Complete isolation - main process never crashes
- ✅ Can detect and report crashes
- ✅ Can restart worker process automatically

**Cons**:
- ❌ Massive complexity increase
- ❌ Performance overhead (process creation, IPC)
- ❌ Difficult to debug
- ❌ Requires separate executable
- ❌ Overkill for this problem

**Risk Assessment**: **HIGH COMPLEXITY**
- Only use if Options 1 & 2 are insufficient
- Not recommended for most scenarios

---

### Option 4: Document Known Limitation 📝 MINIMUM VIABLE

**How it works**: Document the issue and provide guidance to users.

**Implementation**:

Add to README.md:

````markdown
## Known Limitations

### AccessViolationException with Invalid Parameters

**Issue**: When calling `GetRangeAsync()` with certain invalid parameters (e.g., invalid symbol names),
the underlying native library (databento-cpp) may crash with an `AccessViolationException` instead of
returning a proper error.

**Affected Methods**:
- `HistoricalClient.GetRangeAsync()`
- `HistoricalClient.GetRangeToFileAsync()`

**Trigger Conditions**:
- Invalid symbol names (e.g., `"CL"` instead of `"CLZ5"`)
- Malformed dataset names
- Other validation errors that result in HTTP 400/422 responses from Databento API

**Workarounds**:
1. **Validate symbols** using Databento's symbology API before querying historical data
2. **Use try/catch** around historical queries and log crashes for debugging
3. **Test with small requests** first to validate parameters
4. **Report crashes** to help us track down problematic parameter combinations

**Example**:
```csharp
try
{
    // Verify symbol exists first
    var resolution = await client.SymbologyResolveAsync(
        dataset: "GLBX.MDP3",
        symbols: new[] { "CL" },
        stypeIn: SType.RawSymbol,
        stypeOut: SType.InstrumentId,
        startDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)),
        endDate: DateOnly.FromDateTime(DateTime.UtcNow)
    );

    if (resolution.NotFound.Count > 0)
    {
        Console.WriteLine($"Invalid symbols: {string.Join(", ", resolution.NotFound)}");
        return;
    }

    // Now safe to query
    await foreach (var record in client.GetRangeAsync(...))
    {
        // Process records
    }
}
catch (AccessViolationException ex)
{
    // Native library crashed - log and handle gracefully
    _logger.LogError(ex, "Native library crashed during historical query");
    throw new InvalidOperationException(
        "Historical query failed due to native library error. " +
        "Please verify your parameters and try again.", ex);
}
```

**Status**: Bug reported to databento-cpp maintainers. Will be resolved in a future version
of the native library.

**Issue Tracking**:
- databento-dotnet: https://github.com/Alparse/databento-dotnet/issues/1
- databento-cpp: [Link to be added when reported]
````

**Pros**:
- ✅ Simple to implement
- ✅ Sets proper user expectations
- ✅ Provides workarounds

**Cons**:
- ❌ Users' applications may still crash
- ❌ Poor user experience
- ❌ Not a real solution

---

## Recommended Approach

**Implement Options 1, 2, and 4 together:**

### Phase 1: Immediate (This PR)
1. ✅ **Option 1**: Add `[HandleProcessCorruptedStateExceptions]` to catch crashes
2. ✅ **Option 2**: Add pre-validation for obvious invalid parameters
3. ✅ **Option 4**: Document known limitation in README

### Phase 2: After Native Fix
1. Remove AccessViolationException handling once databento-cpp is fixed
2. Keep pre-validation (good practice regardless)
3. Update documentation to reflect fix

---

## Implementation Priority

| Priority | Task | Effort | Impact |
|----------|------|--------|--------|
| **P0** | Report bug to databento-cpp | 1 hour | High (gets fix started) |
| **P1** | Implement Option 1 (catch AVE) | 4 hours | High (prevents crashes) |
| **P1** | Implement Option 2 (validation) | 2 hours | Medium (reduces crashes) |
| **P1** | Document in README (Option 4) | 1 hour | Low (sets expectations) |
| **P2** | Add logging/telemetry | 2 hours | Medium (helps debugging) |
| **P3** | Consider Option 3 if needed | 20+ hours | High (but complex) |

---

## Testing Strategy

### Test Cases

1. **Valid query** - should work normally:
   ```csharp
   await client.GetRangeAsync("GLBX.MDP3", Schema.Ohlcv1D, ["CLZ5"], ...)
   ```

2. **Invalid symbol** - should throw DbentoException (not crash):
   ```csharp
   await client.GetRangeAsync("GLBX.MDP3", Schema.Ohlcv1D, ["CL"], ...)
   ```

3. **Date range too large** - should throw DbentoException (not crash):
   ```csharp
   await client.GetRangeAsync("GLBX.MDP3", Schema.Ohlcv1D, ["CLZ5"],
       startTime: DateTime.Now.AddYears(-5),
       endTime: DateTime.Now)
   ```

4. **Empty symbols** - should throw ArgumentException (before native call):
   ```csharp
   await client.GetRangeAsync("GLBX.MDP3", Schema.Ohlcv1D, [], ...)
   ```

5. **Null symbols** - should throw ArgumentException (before native call):
   ```csharp
   await client.GetRangeAsync("GLBX.MDP3", Schema.Ohlcv1D, [null], ...)
   ```

### Success Criteria

- ✅ No process crashes (exit code 139/-1073741819)
- ✅ Clear error messages for all failure cases
- ✅ Application can continue after error
- ✅ Logging captures all crashes for analysis

---

## Rollback Plan

If the mitigation causes issues:

1. Remove `[HandleProcessCorruptedStateExceptions]` attribute
2. Keep pre-validation (safe to keep)
3. Fall back to Option 4 only (document limitation)
4. Advise users to upgrade databento-cpp when fix is available

---

## Long-Term Solution

The **real fix** must come from databento-cpp:
- Fix memory safety bug in error response handling
- Add comprehensive tests for invalid parameters
- Ensure all API errors are handled gracefully

Until then, our mitigation provides the best possible user experience given the constraints.

---

## Decision

**Proceed with Options 1 + 2 + 4?** Yes / No / Discuss

If yes, I'll implement the changes immediately.
