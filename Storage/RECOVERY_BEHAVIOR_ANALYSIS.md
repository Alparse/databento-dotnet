# Recovery Behavior Analysis: After AccessViolationException

## Critical Question

**If we catch AccessViolationException, can the user recover and continue running code?**

## Short Answer

**YES - with caveats. The specific operation fails, but the application and other operations can continue.**

---

## What Happens When We Catch AccessViolationException?

### Normal Flow (No Error)

```
User Code → Our Wrapper → Native Code → databento-cpp → API
                                     ↓
                                  Success
                                     ↓
User Code ← Our Wrapper ← Native Code ← databento-cpp ← API
```

### Error Flow (AccessViolationException)

```
User Code → Our Wrapper → Native Code → databento-cpp → API
                                     ↓
                            CRASH (memory corruption)
                                     ↓
                          AccessViolationException
                                     ↓
              [HandleProcessCorruptedStateExceptions]
                                     ↓
                            We catch it here
                                     ↓
                         Convert to DbentoException
                                     ↓
User Code ← DbentoException thrown
```

---

## What State is the Process In?

### ✅ SAFE: Application State

**Managed (.NET) code state**: ✅ **INTACT**
- Your variables: Safe
- Your objects: Safe
- Other HistoricalClient instances: Safe
- Other operations: Safe
- Application logic: Safe

**Why?** The crash happened in native (databento-cpp) code, not managed code.

### ⚠️ UNCERTAIN: Native Client State

**The specific HistoricalClient instance that crashed**: ⚠️ **POTENTIALLY CORRUPTED**
- Native handle: May be in bad state
- Internal buffers: May be corrupted
- Native connections: May be broken

**Why?** AccessViolationException indicates memory corruption in databento-cpp.

### ✅ SAFE: Other Native Instances

**Other client instances**: ✅ **SAFE**
- Different HistoricalClient: Safe
- Different LiveClient: Safe
- They have separate native handles and memory

---

## Recovery Scenarios

### Scenario 1: Single Operation Fails

```csharp
var client = new HistoricalClientBuilder()
    .WithApiKey(apiKey)
    .Build();

try
{
    // This crashes
    await foreach (var record in client.GetRangeAsync(
        "GLBX.MDP3", Schema.Ohlcv1D, ["CL"], startTime, endTime))
    {
        Console.WriteLine(record);
    }
}
catch (DbentoException ex)
{
    Console.WriteLine($"Operation failed: {ex.Message}");
    // ✅ Application continues
    // ⚠️ This client instance may be corrupted
}

// Question: Can we use the same client again?
// Answer: UNSAFE - should create new client
```

**Status**: ✅ Application continues, ⚠️ Client instance unsafe

---

### Scenario 2: Using Same Client After Crash

```csharp
var client = new HistoricalClientBuilder()
    .WithApiKey(apiKey)
    .Build();

try
{
    // First call - crashes
    await client.GetRangeAsync("GLBX.MDP3", Schema.Ohlcv1D, ["CL"], ...);
}
catch (DbentoException ex)
{
    Console.WriteLine("First call failed");
}

try
{
    // Second call - using SAME client
    await client.GetRangeAsync("GLBX.MDP3", Schema.Ohlcv1D, ["CLZ5"], ...);
    // ❓ Will this work?
}
catch (DbentoException ex)
{
    Console.WriteLine("Second call failed");
}
```

**Risk Assessment**:
- **If native state corrupted**: Second call may crash again or return garbage
- **If crash was isolated**: Second call may work
- **Cannot guarantee**: Native state is unpredictable after AVE

**Recommendation**: ❌ **DO NOT reuse client after crash**

---

### Scenario 3: Creating New Client After Crash

```csharp
try
{
    var client1 = new HistoricalClientBuilder()
        .WithApiKey(apiKey)
        .Build();

    // This crashes
    await client1.GetRangeAsync("GLBX.MDP3", Schema.Ohlcv1D, ["CL"], ...);
}
catch (DbentoException ex)
{
    Console.WriteLine("First client crashed");
}

// Create NEW client with NEW native handle
var client2 = new HistoricalClientBuilder()
    .WithApiKey(apiKey)
    .Build();

try
{
    // This should work - fresh native state
    await client2.GetRangeAsync("GLBX.MDP3", Schema.Ohlcv1D, ["CLZ5"], ...);
    Console.WriteLine("✅ Second client works!");
}
catch (DbentoException ex)
{
    Console.WriteLine("Second client also failed");
}
```

**Status**: ✅ **SAFE** - new native handle, fresh state

**Recommendation**: ✅ **Create new client after crash**

---

### Scenario 4: Multiple Concurrent Clients

```csharp
// Client 1 for trades
var tradesClient = new HistoricalClientBuilder()
    .WithApiKey(apiKey)
    .Build();

// Client 2 for quotes
var quotesClient = new HistoricalClientBuilder()
    .WithApiKey(apiKey)
    .Build();

// Start both concurrently
var tradesTask = Task.Run(async () =>
{
    try
    {
        // This crashes
        await tradesClient.GetRangeAsync("GLBX.MDP3", Schema.Trades, ["CL"], ...);
    }
    catch (DbentoException ex)
    {
        Console.WriteLine("Trades client crashed");
    }
});

var quotesTask = Task.Run(async () =>
{
    try
    {
        // This continues normally - different native handle
        await foreach (var record in quotesClient.GetRangeAsync(
            "GLBX.MDP3", Schema.Mbp1, ["CLZ5"], ...))
        {
            Console.WriteLine($"Quote: {record}");
        }
        Console.WriteLine("✅ Quotes client completed successfully");
    }
    catch (DbentoException ex)
    {
        Console.WriteLine("Quotes client failed");
    }
});

await Task.WhenAll(tradesTask, quotesTask);
// ✅ Application continues
// ✅ Quotes client unaffected by trades crash
```

**Status**: ✅ **SAFE** - separate native handles, isolated failure

**Result**: One client crashes, others continue normally

---

### Scenario 5: Long-Running Service

```csharp
// Web API service
public class MarketDataService
{
    private readonly ILogger<MarketDataService> _logger;

    [HttpGet("ohlc/{symbol}")]
    public async Task<IActionResult> GetOHLC(string symbol)
    {
        try
        {
            // Create fresh client for each request
            await using var client = new HistoricalClientBuilder()
                .WithApiKey(_apiKey)
                .Build();

            var data = new List<Record>();
            await foreach (var record in client.GetRangeAsync(
                "GLBX.MDP3", Schema.Ohlcv1D, [symbol],
                DateTimeOffset.Now.AddDays(-30),
                DateTimeOffset.Now))
            {
                data.Add(record);
            }

            return Ok(data);
        }
        catch (DbentoException ex)
        {
            _logger.LogError(ex, "Query failed for symbol {Symbol}", symbol);

            // ✅ Return error to THIS request
            return BadRequest(new { error = ex.Message });
        }
        // ✅ Service continues running
        // ✅ Next request uses fresh client
    }
}
```

**Status**: ✅ **SAFE** - each request isolated, service stays up

**Result**:
- Failed request returns error to client
- Service continues processing other requests
- No cascading failures

---

## Best Practices for Recovery

### ✅ DO

1. **Create new client after crash**
   ```csharp
   catch (DbentoException ex)
   {
       // Don't reuse crashed client
       await client.DisposeAsync();

       // Create fresh client
       client = new HistoricalClientBuilder()
           .WithApiKey(apiKey)
           .Build();
   }
   ```

2. **Isolate operations**
   ```csharp
   // Use separate clients for separate operations
   await using var client = CreateClient();
   await ProcessSymbol(client, symbol);
   // Client disposed - any corruption cleaned up
   ```

3. **Log crashes for debugging**
   ```csharp
   catch (DbentoException ex)
   {
       _logger.LogError(ex,
           "Native crash with dataset={Dataset}, symbols={Symbols}",
           dataset, string.Join(",", symbols));

       // Report to telemetry for analysis
       _telemetry.TrackException(ex);
   }
   ```

4. **Provide fallback**
   ```csharp
   try
   {
       return await GetDataFromDatabento(symbol);
   }
   catch (DbentoException ex)
   {
       _logger.LogWarning("Databento failed, using cache");
       return await GetDataFromCache(symbol);
   }
   ```

### ❌ DON'T

1. **Don't reuse crashed client**
   ```csharp
   catch (DbentoException ex)
   {
       // Bad - client may be corrupted
       await client.GetRangeAsync(...);  // ❌ Unsafe
   }
   ```

2. **Don't ignore crashes silently**
   ```csharp
   catch (DbentoException ex)
   {
       // Bad - hiding the problem
       return null;  // ❌ User doesn't know what happened
   }
   ```

3. **Don't hold onto client long-term**
   ```csharp
   // Bad - long-lived client increases corruption risk
   private readonly HistoricalClient _client;  // ❌

   // Good - create client per operation
   public async Task DoWork()
   {
       await using var client = CreateClient();  // ✅
       await ProcessData(client);
   }
   ```

---

## Enhanced Mitigation with Auto-Recovery

### Option: Mark Client as Faulted

```csharp
public class HistoricalClient
{
    private volatile bool _isFaulted = false;

    [HandleProcessCorruptedStateExceptions]
    [SecurityCritical]
    protected T ExecuteNativeCall<T>(Func<T> nativeCall)
    {
        if (_isFaulted)
        {
            throw new InvalidOperationException(
                "This client instance has faulted and cannot be reused. " +
                "Please create a new client instance.");
        }

        try {
            return nativeCall();
        }
        catch (AccessViolationException ex)
        {
            _isFaulted = true;  // Mark as faulted

            _logger?.LogError(ex, "Native crash - client is now faulted");

            throw new DbentoException(
                "Native library crashed. This client instance is no longer usable. " +
                "Please create a new client instance to continue.", ex);
        }
    }
}
```

**Usage**:
```csharp
var client = CreateClient();

try
{
    await client.GetRangeAsync(...);
}
catch (DbentoException ex)
{
    Console.WriteLine(ex.Message);
    // Client is now faulted
}

try
{
    // This throws immediately
    await client.GetRangeAsync(...);
}
catch (InvalidOperationException ex)
{
    Console.WriteLine("Client is faulted, creating new one");
    client = CreateClient();
}
```

**Benefits**:
- ✅ Prevents accidental reuse of corrupted client
- ✅ Clear error message to user
- ✅ Forces creation of new client

---

## Memory Safety Analysis

### Question: Is Process Memory Corrupted?

**Answer**: **Partially - only the crashed client's native memory**

```
┌─────────────────────────────────────────┐
│ .NET Process Memory                     │
├─────────────────────────────────────────┤
│ Managed Heap (Your Objects)             │  ✅ SAFE
│  - Application state                    │
│  - Other client instances               │
│  - Variables, lists, etc.               │
├─────────────────────────────────────────┤
│ Native Heap (databento-cpp)             │
│                                         │
│  ┌─────────────────────────┐           │
│  │ Crashed Client State    │           │  ⚠️ CORRUPTED
│  │  - Native buffers       │           │
│  │  - Connection state     │           │
│  └─────────────────────────┘           │
│                                         │
│  ┌─────────────────────────┐           │
│  │ Other Client State      │           │  ✅ SAFE
│  │  - Independent memory   │           │
│  └─────────────────────────┘           │
└─────────────────────────────────────────┘
```

**Implication**: Disposing crashed client cleans up corrupted native memory.

---

## Testing Recovery Behavior

### Test 1: Can Continue After Crash?

```csharp
[Test]
public async Task Application_ContinuesAfter_NativeCrash()
{
    var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");

    // First client - will crash
    try
    {
        await using var client1 = new HistoricalClientBuilder()
            .WithApiKey(apiKey)
            .Build();

        await foreach (var record in client1.GetRangeAsync(
            "GLBX.MDP3", Schema.Ohlcv1D, ["CL"], startTime, endTime))
        {
            // Should not reach here
            Assert.Fail("Expected crash");
        }
    }
    catch (DbentoException ex)
    {
        Assert.Contains("Native library crashed", ex.Message);
    }

    // Second client - should work
    await using var client2 = new HistoricalClientBuilder()
        .WithApiKey(apiKey)
        .Build();

    var records = new List<Record>();
    await foreach (var record in client2.GetRangeAsync(
        "GLBX.MDP3", Schema.Ohlcv1D, ["CLZ5"], startTime, endTime))
    {
        records.Add(record);
    }

    Assert.NotEmpty(records);
    // ✅ Test passes - application recovered
}
```

### Test 2: Reusing Crashed Client Fails Safely

```csharp
[Test]
public async Task CrashedClient_CannotBeReused()
{
    var client = new HistoricalClientBuilder()
        .WithApiKey(apiKey)
        .Build();

    // First call crashes
    try
    {
        await client.GetRangeAsync("GLBX.MDP3", Schema.Ohlcv1D, ["CL"], ...);
    }
    catch (DbentoException) { }

    // Second call on same client
    var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
    {
        await client.GetRangeAsync("GLBX.MDP3", Schema.Ohlcv1D, ["CLZ5"], ...);
    });

    Assert.Contains("faulted", ex.Message);
    // ✅ Safe failure - clear error message
}
```

---

## Production Monitoring

### Metrics to Track

```csharp
public class DbentoClientMetrics
{
    public Counter NativeCrashes { get; set; }
    public Counter ClientsCreated { get; set; }
    public Counter ClientsRecovered { get; set; }
    public Histogram OperationDuration { get; set; }
}

[HandleProcessCorruptedStateExceptions]
[SecurityCritical]
protected T ExecuteNativeCall<T>(Func<T> nativeCall)
{
    var stopwatch = Stopwatch.StartNew();

    try {
        return nativeCall();
    }
    catch (AccessViolationException ex)
    {
        _metrics.NativeCrashes.Inc();

        _logger.LogError(ex,
            "Native crash after {Duration}ms. " +
            "Total crashes: {TotalCrashes}",
            stopwatch.ElapsedMilliseconds,
            _metrics.NativeCrashes.Value);

        throw new DbentoException("Native library crashed", ex);
    }
    finally
    {
        _metrics.OperationDuration.Observe(stopwatch.ElapsedMilliseconds);
    }
}
```

### Alerts to Configure

```yaml
alerts:
  - name: high_native_crash_rate
    condition: rate(native_crashes[5m]) > 0.01
    severity: warning
    message: "More than 1% of operations crashing in native code"

  - name: consecutive_crashes
    condition: native_crashes without recovery > 3
    severity: critical
    message: "Multiple consecutive crashes - databento-cpp bug"
```

---

## Conclusion

### Can User Recover After Crash?

**YES** ✅ - with proper handling:

1. **✅ Application continues** - no process termination
2. **✅ Other operations work** - isolation per client instance
3. **⚠️ Crashed client unsafe** - must create new client
4. **✅ New client works** - fresh native state
5. **✅ Service stays up** - failed requests don't cascade

### Recovery Pattern

```csharp
// Safe pattern
HistoricalClient client = CreateClient();

while (hasMoreWork)
{
    try
    {
        await ProcessWithClient(client, workItem);
    }
    catch (DbentoException ex) when (ex.Message.Contains("Native library crashed"))
    {
        _logger.LogWarning("Native crash, creating new client");

        // Clean up crashed client
        await client.DisposeAsync();

        // Create fresh client
        client = CreateClient();

        // Retry with new client (or skip item)
        continue;
    }
}
```

### Bottom Line

**With universal mitigation:**
- ❌ **Without**: Process crashes → entire application dies
- ✅ **With**: Operation fails → throw exception → user handles it → application continues

**This is exactly what we want** - graceful degradation instead of catastrophic failure.

---

## Recommendation

**Implement enhanced mitigation with faulted client detection:**

```csharp
public class HistoricalClient : IHistoricalClient
{
    private volatile bool _isFaulted = false;

    [HandleProcessCorruptedStateExceptions]
    [SecurityCritical]
    protected T ExecuteNativeCall<T>(Func<T> nativeCall, [CallerMemberName] string memberName = "")
    {
        if (_isFaulted)
        {
            throw new InvalidOperationException(
                $"This {GetType().Name} instance has faulted due to a previous native crash " +
                "and cannot be reused. Please create a new client instance.");
        }

        try {
            return nativeCall();
        }
        catch (AccessViolationException ex)
        {
            _isFaulted = true;

            _logger?.LogError(ex,
                "Native crash in {Method}. Client is now faulted and must be recreated.",
                memberName);

            throw new DbentoException(
                $"Native library crashed during {memberName}. " +
                "This client instance is no longer usable. " +
                "Please create a new client instance to continue.", ex);
        }
    }
}
```

This ensures:
- ✅ Crashes are caught and reported
- ✅ User cannot accidentally reuse corrupted client
- ✅ Clear guidance to create new client
- ✅ Application continues running
- ✅ Full recovery with fresh client

**User experience after crash**: Clean error, clear recovery path, application stays up.
