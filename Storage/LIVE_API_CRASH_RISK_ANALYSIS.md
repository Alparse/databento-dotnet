# Live API Crash Risk Analysis

## Question

**Does the AccessViolationException bug in Historical API also affect Live API (including Replay mode)?**

## Answer

**Yes, potentially - but with LOWER risk.**

---

## Why Live API Has the Same Vulnerability

### Native Code Structure

Both Historical and Live APIs use the same pattern:

#### Historical API (CRASHES)
```cpp
// historical_client_wrapper.cpp:124
try {
    wrapper->client->TimeseriesGetRange(...);  // ← Crashes here with invalid symbols
    return 0;
}
catch (const std::exception& e) {
    // Never reached - AccessViolationException bypasses this
    return -1;
}
```

#### Live API (SAME PATTERN)
```cpp
// live_client_wrapper.cpp:206
try {
    wrapper->client->Subscribe(...);  // ← Could crash here
    return 0;
}
catch (const std::exception& e) {
    // Would not catch AccessViolationException
    return -1;
}
```

```cpp
// live_client_wrapper.cpp:458
try {
    wrapper->client->Start(...);  // ← Could crash here
    return 0;
}
catch (const std::exception& e) {
    // Would not catch AccessViolationException
    return -1;
}
```

**Both use try/catch that CANNOT catch hardware exceptions.**

---

## Why Live API Risk is LOWER

### Different Error Handling Model

| Aspect | Historical API | Live API |
|--------|---------------|----------|
| **Error Source** | HTTP error response from API | WebSocket protocol / streaming |
| **Invalid Symbol Handling** | Server returns HTTP 422 → databento-cpp crashes | Server includes in `metadata.not_found` → graceful |
| **Error Timing** | During query execution | During metadata phase |
| **Observed Behavior** | **Crashes 100%** with invalid symbols | **Unknown** - needs testing |

### Live API Error Handling

When you subscribe to invalid symbol in Live API:

```csharp
await client.SubscribeAsync(
    dataset: "GLBX.MDP3",
    schema: Schema.Trades,
    symbols: new[] { "CL" }  // Invalid
);

var metadata = await client.StartAsync();
// metadata.NotFound == ["CL"]  ← Returned gracefully
```

The Databento Live API server handles invalid symbols **differently**:
- ✅ Subscription succeeds (no error)
- ✅ Start succeeds
- ✅ Metadata includes invalid symbols in `not_found` array
- ✅ No records received for invalid symbols
- ✅ **No HTTP error response** that could trigger native crash

---

## When Could Live API Crash?

### Scenario 1: WebSocket Error Messages

If databento Live server sends **error messages** over WebSocket:
```
ErrorMessage (RType 0x15) → databento-cpp processes error → potential crash
```

**Risk**: MEDIUM
- Depends on how databento-cpp handles ErrorMessage records in streaming mode
- If same buggy code path as Historical API → crash
- If different code path → may be safe

### Scenario 2: Replay Mode with Invalid Date Range

```csharp
await client.SubscribeAsync(
    dataset: "GLBX.MDP3",
    schema: Schema.Trades,
    symbols: new[] { "CLZ5" },
    startTime: new DateTimeOffset(DateTime.Parse("2000-01-01"), TimeSpan.Zero)  // Too old
);

await client.StartAsync();  // ← Could crash if server returns error
```

**Risk**: MEDIUM-HIGH
- Replay mode queries historical data
- May use similar error handling as Historical API
- Could crash on date range validation errors

### Scenario 3: Connection/Authentication Errors

```csharp
var client = new LiveClientBuilder()
    .WithApiKey("invalid-key")
    .WithDataset("INVALID.DATASET")
    .Build();

await client.SubscribeAsync(...);
await client.StartAsync();  // ← Could crash on authentication/dataset error
```

**Risk**: LOW-MEDIUM
- Authentication happens before data streaming
- May have better error handling
- Less likely to hit buggy code path

---

## Managed Code Protection Status

### Historical API
- ❌ **NO PROTECTION**: Callbacks throw exceptions (causes crashes)
- ❌ Needs mitigation (HandleProcessCorruptedStateExceptions)

### Live API
- ✅ **PARTIAL PROTECTION**: Callbacks DO NOT throw exceptions
- ✅ Uses `SafeInvokeEvent` to prevent subscriber exceptions
- ⚠️ BUT: Native code can still crash BEFORE callback is invoked

#### Live API Callback (Safe)
```csharp
// LiveClient.cs:545-549
catch (Exception ex)
{
    // Does NOT rethrow - good!
    SafeInvokeEvent(ErrorOccurred, new Events.ErrorEventArgs(ex));
}
```

**However**: If databento-cpp crashes in `Subscribe()` or `Start()` before reaching the callback, the process still crashes.

---

## Testing Needed

### Test Cases for Live API

1. **Invalid Symbol (Normal)**
   ```csharp
   await client.SubscribeAsync("GLBX.MDP3", Schema.Trades, ["CL"]);
   var metadata = await client.StartAsync();
   // Expected: metadata.NotFound == ["CL"] (no crash)
   ```

2. **Invalid Symbol (Replay)**
   ```csharp
   await client.SubscribeAsync(
       "GLBX.MDP3", Schema.Trades, ["CL"],
       startTime: DateTimeOffset.Now.AddDays(-1));
   var metadata = await client.StartAsync();
   // Unknown: crash or graceful error?
   ```

3. **Invalid Date Range (Replay)**
   ```csharp
   await client.SubscribeAsync(
       "GLBX.MDP3", Schema.Trades, ["CLZ5"],
       startTime: new DateTimeOffset(DateTime.Parse("1900-01-01"), TimeSpan.Zero));
   var metadata = await client.StartAsync();
   // Unknown: crash or graceful error?
   ```

4. **Invalid Dataset**
   ```csharp
   var client = new LiveClientBuilder()
       .WithDataset("INVALID.DATASET")
       .Build();
   await client.SubscribeAsync(...);
   var metadata = await client.StartAsync();
   // Unknown: crash or graceful error?
   ```

5. **Invalid API Key**
   ```csharp
   var client = new LiveClientBuilder()
       .WithApiKey("invalid-key")
       .Build();
   await client.SubscribeAsync(...);
   var metadata = await client.StartAsync();
   // Unknown: crash or graceful error?
   ```

---

## Mitigation Strategy

### For Historical API (CRITICAL)
✅ **MUST IMPLEMENT**:
1. Option 1: `[HandleProcessCorruptedStateExceptions]`
2. Option 2: Pre-validation
3. Option 4: Documentation

### For Live API (RECOMMENDED)
✅ **SHOULD IMPLEMENT**:
1. **Same mitigations as Historical API** (defense in depth)
2. **Add integration tests** for error scenarios
3. **Monitor for crashes** in production

**Reasoning**:
- Live API has better managed code protection (no rethrow in callback)
- Native code can still crash before callback
- Same mitigation approach works for both APIs
- Low cost to implement, high value for robustness

---

## Recommendations

### Immediate Actions

1. ✅ **Implement AccessViolationException handling** for BOTH Historical and Live APIs
2. ✅ **Add pre-validation** for BOTH APIs
3. ⚠️ **Test Live API** with invalid parameters (especially Replay mode)
4. ✅ **Document limitation** for both APIs

### Testing Priority

| Priority | Test Case | Expected Result |
|----------|-----------|-----------------|
| **P0** | Historical + invalid symbol | Currently crashes → should catch |
| **P1** | Live Replay + invalid symbol | Unknown → test |
| **P1** | Live Replay + invalid date range | Unknown → test |
| **P2** | Live + invalid dataset | Probably graceful → verify |
| **P2** | Live + invalid API key | Probably graceful → verify |

### Code Changes

Apply mitigation to **both** `HistoricalClient` and `LiveClient`:

```csharp
[HandleProcessCorruptedStateExceptions]
[SecurityCritical]
private object ExecuteNativeCall(...)  // Used by both Historical and Live
{
    try {
        // Native call
    }
    catch (AccessViolationException ex) {
        _logger?.LogError(ex, "Native code crashed");
        return new DbentoException("Native library crashed...");
    }
}
```

---

## Conclusion

**Does the bug affect Live API?**

- **Definitely**: YES - same native code vulnerability exists
- **In practice**: UNKNOWN - depends on server error handling
- **Risk level**: LOWER than Historical API, but still exists
- **Mitigation**: Apply same fixes to both APIs

**Next Steps**:
1. Implement mitigation for **both** Historical and Live APIs
2. Test Live API with invalid parameters
3. Report findings to databento-cpp maintainers
4. Monitor for crashes in production

---

## Update After Testing

*[To be filled in after running Live API tests]*

### Test Results

- [ ] Live + invalid symbol: ___________
- [ ] Live Replay + invalid symbol: ___________
- [ ] Live Replay + invalid date range: ___________
- [ ] Live + invalid dataset: ___________
- [ ] Live + invalid API key: ___________

### Conclusion After Testing

*[To be determined]*
