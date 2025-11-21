# Historical Future Dates Test

This test reproduces and verifies the fix for the AccessViolationException bug that occurs when requesting historical data for future dates.

## Bug Description

Requesting historical data for future dates (e.g., May-Nov 2025) caused AccessViolationException due to NULL pointer dereference in databento-cpp's `CheckWarnings()` function when processing API warnings.

## Running in Visual Studio

### Option 1: Command Line (Quickest)

```bash
cd C:\Users\serha\source\repos\databento-dotnet\examples\HistoricalFutureDates.Test
dotnet run
```

### Option 2: Visual Studio

1. **Open Solution**:
   - Open `C:\Users\serha\source\repos\databento-dotnet\databento-dotnet.sln` in Visual Studio 2022

2. **Set as Startup Project**:
   - Right-click `HistoricalFutureDates.Test` in Solution Explorer
   - Select "Set as Startup Project"

3. **Ensure API Key is Set**:
   - Make sure `DATABENTO_API_KEY` environment variable is set
   - OR restart Visual Studio if you just set it

4. **Run**:
   - Press `F5` (Debug) or `Ctrl+F5` (Run without debugging)

### Option 3: VS Developer Command Prompt

```cmd
cd C:\Users\serha\source\repos\databento-dotnet
msbuild examples\HistoricalFutureDates.Test\HistoricalFutureDates.Test.csproj
examples\HistoricalFutureDates.Test\bin\Debug\net8.0\HistoricalFutureDates.Test.exe
```

## Expected Results

### Before Fix
```
Testing Historical API with future dates (May-Nov 2025)...
Fetching data...

Fatal error. System.AccessViolationException: Attempted to read or write protected memory.
```

### After Fix
```
Testing Historical API with future dates (May-Nov 2025)...
Fetching data...

Record 1: OHLCV: O:56.81 H:57.73 L:55.17 C:57.14 V:18031 [2025-05-01...]
Record 2: OHLCV: O:57.25 H:58.03 L:56.32 C:57.12 V:11917 [2025-05-02...]
...
✓ SUCCESS: Received 172 records without crashing!

The bug is fixed if you see this message.
```

## Debugging in Visual Studio

If you want to see the crash before applying the fix:

1. Make sure you're using the **original (unfixed)** native DLL
2. Set a breakpoint in `Program.cs` at line with `await foreach`
3. Press F5 to debug
4. Step through to see where the AccessViolationException occurs

## Testing the Fix

This test uses the **unfixed version** of the code (current state in repo). It will demonstrate the AccessViolationException crash.

After applying the fix (see `BUG_FIX_SUMMARY.md` in repo root):

1. Apply the fix to `src/Databento.Native/src/historical_client_wrapper.cpp`:
   - Add `StderrLogReceiver` class implementation
   - Pass `log_receiver.get()` instead of `nullptr` to Historical client
2. Rebuild native library:
   ```bash
   cd src/Databento.Native/build
   cmake --build . --config Release
   ```
3. Copy updated DLL to runtime locations:
   ```bash
   cp src/Databento.Native/build/Release/databento_native.dll src/Databento.Native/runtimes/win-x64/native/
   ```
4. Run this test again
5. Verify you see "✓ SUCCESS: Received 172 records without crashing!"

## Additional Test Cases

You can modify `Program.cs` to test other scenarios:

```csharp
// Test 1: Past dates (should always work)
startTime = new DateTimeOffset(DateTime.Parse("1/1/2024"), TimeSpan.Zero);
endTime = new DateTimeOffset(DateTime.Parse("1/31/2024"), TimeSpan.Zero);

// Test 2: Mix of past and future (may have warnings)
startTime = new DateTimeOffset(DateTime.Parse("11/1/2024"), TimeSpan.Zero);
endTime = new DateTimeOffset(DateTime.Parse("5/1/2025"), TimeSpan.Zero);

// Test 3: Far future (likely all degraded)
startTime = new DateTimeOffset(DateTime.Parse("1/1/2026"), TimeSpan.Zero);
endTime = new DateTimeOffset(DateTime.Parse("12/31/2026"), TimeSpan.Zero);
```
