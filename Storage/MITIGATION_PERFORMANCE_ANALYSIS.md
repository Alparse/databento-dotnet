# Mitigation Performance Analysis

## Question

**Will universal AccessViolationException mitigation slow us down?**

## Short Answer

**No. The overhead is negligible (~0.0001%) compared to native call and network costs.**

---

## Performance Breakdown

### What We're Adding

```csharp
[HandleProcessCorruptedStateExceptions]
[SecurityCritical]
private object ExecuteNativeCall(Func<object> nativeCall)
{
    try {
        return nativeCall();  // ← Actual work happens here
    }
    catch (AccessViolationException ex) {
        // Only runs on crash (never in normal operation)
        throw new DbentoException(...);
    }
}
```

### Cost Components

| Component | Cost | Frequency |
|-----------|------|-----------|
| Method call overhead | ~2-5 ns | Per call |
| Try block overhead | ~1 ns | Per call |
| Lambda allocation | ~10-20 ns | Per call (can be optimized) |
| Attribute processing | 0 ns | One-time at JIT |
| Catch block | 0 ns | Never executes in normal operation |
| **TOTAL OVERHEAD** | **~10-25 ns** | **Per native call** |

---

## Comparison with Actual Costs

### Reality Check: What's Actually Expensive?

```
Component                           Cost             % of Total
─────────────────────────────────────────────────────────────────
Network I/O (API call)              1-100 ms         99.9999%
Native marshaling overhead          10-50 ns         0.00005%
Our mitigation overhead             10-25 ns         0.00002%
─────────────────────────────────────────────────────────────────
TOTAL                               ~50 ms           100%
```

### Concrete Example: GetRangeAsync()

```csharp
await client.GetRangeAsync("GLBX.MDP3", Schema.Ohlcv1D, ["CLZ5"],
    startTime, endTime);
```

**Actual costs**:
1. Network latency: 10-50 ms
2. Server processing: 100-1000 ms
3. Data transfer: 50-500 ms
4. Deserialization: 10-100 ms (per 1000 records)
5. **Our mitigation: 0.000025 ms (25 nanoseconds)**

**Result**: Mitigation is **0.0001% of total time** (1 part in 1,000,000)

---

## Micro-Benchmark

### Test Setup

```csharp
[Benchmark]
public void DirectNativeCall()
{
    // No mitigation
    NativeMethods.dbento_some_method(...);
}

[Benchmark]
public void WithMitigation()
{
    // With universal mitigation wrapper
    ExecuteNativeCall(() => NativeMethods.dbento_some_method(...));
}
```

### Expected Results

```
Method              Mean       Error    StdDev
───────────────────────────────────────────────
DirectNativeCall    45.2 ns    0.8 ns   1.2 ns
WithMitigation      57.8 ns    1.2 ns   1.8 ns
───────────────────────────────────────────────
Difference:         12.6 ns (27.9% overhead)
```

**But**: Native call does network I/O → 10,000,000+ ns → 12.6 ns is **0.0001% overhead**

---

## Optimization Options

### Option 1: Universal Wrapper (Recommended)

**Code**:
```csharp
[HandleProcessCorruptedStateExceptions]
[SecurityCritical]
private T ExecuteNativeCall<T>(Func<T> nativeCall)
{
    try {
        return nativeCall();
    }
    catch (AccessViolationException ex) {
        _logger?.LogError(ex, "Native crash");
        throw new DbentoException("Native library crashed");
    }
}
```

**Pros**:
- ✅ Centralized logic
- ✅ Easy to maintain
- ✅ Consistent error handling
- ✅ Apply to all methods easily

**Cons**:
- ⚠️ ~25 ns overhead per call
- ⚠️ Lambda allocation (GC pressure - minimal)

**Overhead**: 0.0001% of actual operation time

---

### Option 2: Inline Protection (Maximum Performance)

**Code**:
```csharp
[HandleProcessCorruptedStateExceptions]
[SecurityCritical]
public async IAsyncEnumerable<Record> GetRangeAsync(...)
{
    try {
        // Native call directly here
        var result = NativeMethods.dbento_historical_get_range(...);
        // ...
    }
    catch (AccessViolationException ex) {
        _logger?.LogError(ex, "Native crash");
        throw new DbentoException("Native library crashed");
    }
}
```

**Pros**:
- ✅ Zero wrapper overhead
- ✅ No lambda allocations
- ✅ Compiler can inline

**Cons**:
- ❌ Code duplication (20+ methods)
- ❌ Harder to maintain
- ❌ Error handling inconsistencies

**Overhead**: ~0 ns (negligible)

---

### Option 3: Compile-Time Code Generation

**Code**:
```csharp
// Use source generator to inject protection
[NativeCallProtection]
public async IAsyncEnumerable<Record> GetRangeAsync(...)
{
    // Generator adds try/catch automatically
    var result = NativeMethods.dbento_historical_get_range(...);
}
```

**Pros**:
- ✅ Zero runtime overhead
- ✅ No code duplication
- ✅ Centralized logic in generator

**Cons**:
- ⚠️ Requires C# source generator (complexity)
- ⚠️ Debugging is harder
- ⚠️ Build time increase

**Overhead**: 0 ns (compile-time only)

---

### Option 4: Hybrid Approach

Apply different strategies based on call frequency:

```csharp
// HOT PATH (called millions of times): Inline
[HandleProcessCorruptedStateExceptions]
public async IAsyncEnumerable<Record> GetRangeAsync(...)
{
    try {
        // Direct call - no wrapper
    }
    catch (AccessViolationException ex) { ... }
}

// COLD PATH (called occasionally): Universal wrapper
public async Task<BatchJob> BatchSubmitJobAsync(...)
{
    return await ExecuteNativeCall(() =>
        NativeMethods.dbento_batch_submit_job(...));
}
```

**Pros**:
- ✅ Optimal performance for hot paths
- ✅ Maintainability for cold paths

**Cons**:
- ⚠️ Mixed approach (complexity)
- ⚠️ Need to identify hot vs cold paths

---

## Real-World Impact

### Scenario 1: Historical Query (1 million records)

```
Operation breakdown:
- Network latency: 50 ms
- Server query: 500 ms
- Data transfer: 300 ms
- Deserialization: 200 ms (1M records)
- Mitigation overhead: 0.000025 ms (ONE native call)
────────────────────────────────────────────
Total: 1050.000025 ms

Overhead: 0.0000024%
```

**Impact**: NONE - completely unmeasurable

---

### Scenario 2: Live Streaming (continuous)

```
Operation per second:
- Network packets: 1000/sec
- Record processing: 10,000/sec
- Native callback: 10,000/sec
- Mitigation overhead: 10,000 × 25 ns = 0.25 ms/sec

CPU usage: 0.025% additional
```

**Impact**: NEGLIGIBLE - 0.025% CPU increase

---

### Scenario 3: Batch Operations (one-time)

```
Operation:
- API call: 500 ms
- File download: 5000 ms
- Mitigation overhead: 0.000025 ms
────────────────────────────────────────────
Total: 5500.000025 ms

Overhead: 0.00000045%
```

**Impact**: NONE - imperceptible

---

## Memory Impact

### Option 1: Universal Wrapper

**Allocations per call**:
- Lambda/delegate: 32-40 bytes (GC Gen0)
- Captured variables: 0-8 bytes (if any)

**For 1 million calls**:
- Total: ~40 MB allocated
- GC Gen0 collections: +2-3
- Impact: Minimal (Gen0 is very fast)

### Option 2: Inline

**Allocations per call**: 0 bytes

---

## Recommendations by Use Case

### For databento-dotnet:

**RECOMMENDED: Option 1 (Universal Wrapper)**

**Reasoning**:
1. Network I/O dominates (99.999% of time)
2. ~25 ns overhead is imperceptible
3. Maintainability is critical
4. Consistent error handling
5. Easy to apply to all methods

**Performance cost**: **Effectively zero** in real-world usage

---

### When to Use Option 2 (Inline):

**Only if**:
- You have real benchmarks showing 25 ns matters
- You're calling native code in a tight loop (not the case here)
- Every nanosecond counts (real-time trading, HFT)

**For databento-dotnet**: **NOT NEEDED** - all calls do network I/O

---

### When to Use Option 3 (Code Generation):

**Only if**:
- You have 100+ methods to protect
- Performance AND maintainability both critical
- Team comfortable with source generators

**For databento-dotnet**: **OVERKILL** - only ~20 methods to protect

---

## Benchmarking Strategy

### If you want to measure impact:

```csharp
[SimpleJob(RuntimeMoniker.Net80, baseline: true)]
public class MitigationBenchmark
{
    private HistoricalClient _client;

    [GlobalSetup]
    public void Setup()
    {
        _client = new HistoricalClientBuilder()
            .WithApiKey(ApiKey)
            .Build();
    }

    [Benchmark]
    public async Task WithoutMitigation()
    {
        // Direct native call (hypothetical)
        await _client.GetRangeAsync(...);
    }

    [Benchmark]
    public async Task WithMitigation()
    {
        // With universal wrapper
        await _client.GetRangeAsync(...);
    }
}
```

**Expected result**: Difference will be **unmeasurable** due to network noise.

---

## Final Recommendation

### ✅ USE OPTION 1: Universal Wrapper

**Reasoning**:
```
Benefit: Protects ALL APIs from crashes
Cost:    ~25 nanoseconds per call
Impact:  0.0001% of actual operation time

25 ns / 50,000,000 ns (50ms network) = 0.0005%
```

**This is a NO-BRAINER trade-off.**

### Implementation

```csharp
// Base class for all clients
public abstract class DbentoClientBase
{
    [HandleProcessCorruptedStateExceptions]
    [SecurityCritical]
    protected T ExecuteNativeCall<T>(Func<T> nativeCall, string methodName = "")
    {
        try {
            return nativeCall();
        }
        catch (AccessViolationException ex) {
            _logger?.LogError(ex,
                "Native library crashed in {Method}. " +
                "This is a bug in databento-cpp.", methodName);

            throw new DbentoException(
                $"Native library crashed during {methodName}. " +
                "This may be caused by invalid parameters. " +
                "Please verify your inputs and report this issue.");
        }
    }

    protected async Task<T> ExecuteNativeCallAsync<T>(
        Func<Task<T>> nativeCall, string methodName = "")
    {
        try {
            return await nativeCall();
        }
        catch (AccessViolationException ex) {
            _logger?.LogError(ex,
                "Native library crashed in {Method}", methodName);

            throw new DbentoException(
                $"Native library crashed during {methodName}.");
        }
    }
}

// Usage in HistoricalClient
public class HistoricalClient : DbentoClientBase
{
    public async IAsyncEnumerable<Record> GetRangeAsync(...)
    {
        var result = ExecuteNativeCall(() =>
            NativeMethods.dbento_historical_get_range(...),
            nameof(GetRangeAsync));
        // ...
    }
}
```

---

## Performance Testing Results (Hypothetical)

If we ran benchmarks:

```
| Method                | Mean       | StdDev    | Median    |
|-----------------------|------------|-----------|-----------|
| DirectCall            | 50.234 ms  | 2.456 ms  | 49.982 ms |
| WithMitigation        | 50.234 ms  | 2.451 ms  | 49.985 ms |
| Difference            | 0.000 ms   | -         | 0.003 ms  |
```

**Conclusion**: Difference is within measurement noise (network jitter).

---

## Conclusion

**Will universal mitigation slow us down?**

**NO.**

- Overhead: ~25 nanoseconds per call
- Actual cost: 10-100 milliseconds (network I/O)
- Impact: 0.0001% (1 in 1,000,000)
- **NEGLIGIBLE and UNMEASURABLE in real-world usage**

**Should we use it?**

**ABSOLUTELY YES.**

- Prevents process crashes
- Provides error messages to users
- Minimal performance cost
- Maximum reliability benefit

**Trade-off**: Give up 0.0001% performance to prevent 100% crashes → **EXCELLENT DEAL**

---

## Additional Considerations

### GC Pressure from Lambdas

**Concern**: Lambda allocations in hot path?

**Reality**:
- Allocation: 32-40 bytes (Gen0)
- Gen0 collection: ~1ms per 16MB
- 1M calls = 40MB = 2-3 Gen0 collections = ~3ms total
- Spread over query time: 3ms / 5000ms = 0.06% impact

**Conclusion**: Not a concern for our use case.

### Alternative: No-Allocation Wrapper

If you're really concerned about allocations:

```csharp
// Use struct delegate (no allocation)
[HandleProcessCorruptedStateExceptions]
[SecurityCritical]
protected T ExecuteNativeCall<T, TState>(
    TState state,
    Func<TState, T> nativeCall) where TState : struct
{
    try {
        return nativeCall(state);
    }
    catch (AccessViolationException ex) {
        throw new DbentoException("Native crashed");
    }
}

// Usage (zero allocations)
var result = ExecuteNativeCall(
    (handle, dataset, symbols),
    static ctx => NativeMethods.dbento_call(ctx.handle, ctx.dataset, ctx.symbols));
```

**But**: This is premature optimization. Network I/O dominates anyway.

---

## Decision Matrix

| Approach | Performance | Maintainability | Coverage | Complexity |
|----------|------------|-----------------|----------|------------|
| **Universal Wrapper** | 99.9999% ✅ | ⭐⭐⭐⭐⭐ | 100% ✅ | Low ✅ |
| Inline Protection | 100% ✅ | ⭐⭐ | 100% ✅ | Medium |
| Code Generation | 100% ✅ | ⭐⭐⭐⭐ | 100% ✅ | High ❌ |
| Hybrid | 99.999% ✅ | ⭐⭐⭐ | 100% ✅ | Medium-High |
| Do Nothing | 100% ✅ | N/A | 0% ❌ | None ✅ |

**Winner**: Universal Wrapper - best balance of all factors.

