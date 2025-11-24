# Live API: Invalid Symbol Behavior

## Question

**What happens if you give a bad ticker to the Live API?**

## Short Answer

**Different behavior than Historical API:**
- **Historical API**: Crashes with AccessViolationException
- **Live API (normal)**: Gracefully returns invalid symbols in metadata.not_found
- **Live API (replay)**: ⚠️ Unknown - may crash (needs testing)

---

## Scenario 1: Live API - Normal Mode (Real-Time Streaming)

### User Code

```csharp
var client = new LiveClientBuilder()
    .WithApiKey(apiKey)
    .WithDataset("GLBX.MDP3")
    .Build();

// Subscribe with invalid symbol
await client.SubscribeAsync(
    dataset: "GLBX.MDP3",
    schema: Schema.Trades,
    symbols: ["CL", "AAPL", "CLZ5"]);  // "CL" and "AAPL" are invalid

// Start streaming
var metadata = await client.StartAsync();

Console.WriteLine($"Valid symbols: {string.Join(", ", metadata.Symbols)}");
Console.WriteLine($"Not found: {string.Join(", ", metadata.NotFound)}");

// Stream data
await foreach (var record in client.StreamAsync())
{
    Console.WriteLine(record);
}
```

---

### What Happens Step-by-Step

```
1. User calls SubscribeAsync("CL", "AAPL", "CLZ5")
   ↓
2. Native code stores subscription request
   ↓
3. ✅ Returns immediately - NO ERROR
   ↓
4. User calls StartAsync()
   ↓
5. Native code connects to Databento WebSocket
   ↓
6. Sends subscription request to server
   ↓
7. Server processes symbols:
   - "CL" → Invalid (not a valid continuous symbol)
   - "AAPL" → Invalid (not in GLBX.MDP3 dataset)
   - "CLZ5" → Valid
   ↓
8. Server sends metadata message
   {
     "symbols": ["CLZ5"],
     "not_found": ["CL", "AAPL"],
     ...
   }
   ↓
9. Native code receives metadata
   ↓
10. Deserializes to Metadata object
    ↓
11. ✅ Returns metadata to user - NO CRASH
    ↓
12. User checks metadata.NotFound
    ↓
13. Stream receives data only for valid symbols ("CLZ5")
```

---

### Expected Output

```
Valid symbols: CLZ5
Not found: CL, AAPL

[Trade records for CLZ5 only...]
```

**Result**: ✅ **NO CRASH** - graceful handling

---

### Why No Crash in Normal Live Mode?

**Key Difference**: Protocol handling

| Protocol | Error Delivery | Crash Risk |
|----------|---------------|------------|
| **HTTP (Historical)** | Error response body (JSON) | 🔴 HIGH - databento-cpp crashes parsing it |
| **WebSocket (Live)** | Metadata message (structured) | 🟢 LOW - designed for this |

**Live API metadata format**:
```json
{
  "version": 3,
  "dataset": "GLBX.MDP3",
  "schema": 160,
  "symbols": ["CLZ5"],           // Valid symbols
  "not_found": ["CL", "AAPL"],   // Invalid symbols
  "partial": [],                 // Partially available
  "mappings": []
}
```

This is **expected, normal data** - not an error response. databento-cpp handles it correctly.

---

## Scenario 2: Live API - Replay Mode ⚠️

### User Code

```csharp
var client = new LiveClientBuilder()
    .WithApiKey(apiKey)
    .WithDataset("GLBX.MDP3")
    .Build();

// Subscribe with REPLAY and invalid symbol
await client.SubscribeAsync(
    dataset: "GLBX.MDP3",
    schema: Schema.Trades,
    symbols: ["CL"],  // Invalid
    startTime: DateTimeOffset.Now.AddDays(-1));  // REPLAY mode

// Start streaming
var metadata = await client.StartAsync();

await foreach (var record in client.StreamAsync())
{
    Console.WriteLine(record);
}
```

---

### What Happens (Hypothesis)

```
1. User calls SubscribeAsync with startTime (REPLAY mode)
   ↓
2. Native code stores subscription
   ↓
3. User calls StartAsync()
   ↓
4. Native code connects and sends replay request
   ↓
5. Server validates symbol "CL" against historical data
   ↓
6. Server finds symbol invalid/not available
   ↓
7. ❓ Two possibilities:

   A) Server returns metadata with not_found
      → ✅ Graceful handling (like normal live)

   B) Server returns HTTP error (like Historical API)
      → 💥 databento-cpp crashes processing error
      → AccessViolationException
```

**Status**: ⚠️ **UNKNOWN** - needs testing

**Risk Level**: 🟡 **MEDIUM-HIGH**
- Replay mode queries historical data
- May use similar error handling as Historical API
- If so, will crash like Historical API

---

## Scenario 3: Live API - Invalid Dataset

### User Code

```csharp
var client = new LiveClientBuilder()
    .WithApiKey(apiKey)
    .WithDataset("INVALID.DATASET")  // Bad dataset
    .Build();

await client.SubscribeAsync(
    dataset: "INVALID.DATASET",
    schema: Schema.Trades,
    symbols: ["CLZ5"]);

var metadata = await client.StartAsync();  // What happens here?
```

---

### What Happens (Hypothesis)

```
1. Client connects to server
   ↓
2. Sends subscription with invalid dataset
   ↓
3. Server rejects: "Dataset 'INVALID.DATASET' not found"
   ↓
4. ❓ Two possibilities:

   A) Server sends error via metadata/error message
      → May be handled gracefully

   B) Server sends error and closes connection
      → May trigger crash in databento-cpp
```

**Status**: ⚠️ **UNKNOWN** - needs testing

**Risk Level**: 🟡 **MEDIUM**

---

## Scenario 4: Live API - Symbol That Exists But Wrong Dataset

### User Code

```csharp
var client = new LiveClientBuilder()
    .WithApiKey(apiKey)
    .WithDataset("GLBX.MDP3")  // Futures dataset
    .Build();

await client.SubscribeAsync(
    dataset: "GLBX.MDP3",
    schema: Schema.Trades,
    symbols: ["AAPL"]);  // Stock symbol in futures dataset

var metadata = await client.StartAsync();
```

---

### Expected Behavior

```
Metadata {
  symbols: [],
  not_found: ["AAPL"],  // Not in this dataset
  ...
}
```

**Result**: ✅ Likely graceful (returned in not_found)

---

## Testing Plan

### Test 1: Normal Live with Invalid Symbol

```csharp
[Test]
public async Task LiveAPI_InvalidSymbol_NormalMode_ReturnsInNotFound()
{
    var client = new LiveClientBuilder()
        .WithApiKey(TestApiKey)
        .WithDataset("GLBX.MDP3")
        .Build();

    await client.SubscribeAsync(
        "GLBX.MDP3",
        Schema.Trades,
        ["CL", "CLZ5"]);  // "CL" invalid, "CLZ5" valid

    var metadata = await client.StartAsync();

    Assert.Contains("CLZ5", metadata.Symbols);
    Assert.Contains("CL", metadata.NotFound);
    // ✅ Test should pass - graceful handling
}
```

---

### Test 2: Replay Mode with Invalid Symbol ⚠️

```csharp
[Test]
public async Task LiveAPI_InvalidSymbol_ReplayMode_BehaviorUnknown()
{
    var client = new LiveClientBuilder()
        .WithApiKey(TestApiKey)
        .WithDataset("GLBX.MDP3")
        .Build();

    await client.SubscribeAsync(
        "GLBX.MDP3",
        Schema.Trades,
        ["CL"],  // Invalid
        startTime: DateTimeOffset.Now.AddDays(-1));  // REPLAY

    // ❓ Will this crash or return gracefully?
    try
    {
        var metadata = await client.StartAsync();

        // If we get here, it's graceful
        Assert.Contains("CL", metadata.NotFound);
        Console.WriteLine("✅ Replay mode handles invalid symbols gracefully");
    }
    catch (DbentoException ex)
    {
        // If we catch, check if it's a crash
        if (ex.Message.Contains("Native library crashed"))
        {
            Console.WriteLine("💥 Replay mode CRASHES with invalid symbols");
            Assert.Fail("Replay mode vulnerable to crash bug");
        }
        else
        {
            Console.WriteLine("✅ Replay mode throws proper exception");
        }
    }
}
```

---

### Test 3: Invalid Dataset

```csharp
[Test]
public async Task LiveAPI_InvalidDataset_BehaviorUnknown()
{
    var client = new LiveClientBuilder()
        .WithApiKey(TestApiKey)
        .WithDataset("INVALID.DATASET")
        .Build();

    await client.SubscribeAsync(
        "INVALID.DATASET",
        Schema.Trades,
        ["CLZ5"]);

    try
    {
        var metadata = await client.StartAsync();
        Console.WriteLine("✅ Invalid dataset handled gracefully");
    }
    catch (DbentoException ex)
    {
        if (ex.Message.Contains("Native library crashed"))
        {
            Console.WriteLine("💥 Invalid dataset CRASHES");
        }
        else
        {
            Console.WriteLine("✅ Invalid dataset throws proper exception");
        }
    }
}
```

---

## Comparison: Historical vs Live

| Scenario | Historical API | Live API (Normal) | Live API (Replay) |
|----------|---------------|-------------------|-------------------|
| **Invalid symbol** | 💥 **CRASHES** | ✅ **Graceful** (not_found) | ❓ **Unknown** (likely crash?) |
| **Invalid dataset** | 💥 **CRASHES** | ❓ **Unknown** | ❓ **Unknown** |
| **Date range too large** | 💥 **CRASHES** | N/A | ❓ **Unknown** |
| **Symbol wrong dataset** | 💥 **CRASHES** | ✅ **Graceful** (not_found) | ❓ **Unknown** |

---

## Why Live Normal Mode is Safer

### Design Difference

**Historical API (HTTP)**:
```
Request → Server validates → Returns error HTTP response
                              ↓
                         Error JSON body
                              ↓
                    databento-cpp parses error
                              ↓
                         💥 CRASH (bug)
```

**Live API (WebSocket)**:
```
Subscribe → Server validates → Returns metadata
                               ↓
                          Structured message
                               ↓
                       not_found: ["CL"]
                               ↓
                          ✅ Handled
```

**Key**: Live protocol **expects** invalid symbols and has a field for them. Historical protocol treats them as **errors**.

---

## Current Implementation in C#

### Live API Callback Handles Errors Safely

```csharp
// LiveClient.cs:545-549
catch (Exception ex)
{
    // Does NOT rethrow - safe!
    SafeInvokeEvent(ErrorOccurred, new Events.ErrorEventArgs(ex));
}
```

But this only helps **after** the callback is invoked. If databento-cpp crashes **before** calling the callback, we still crash.

---

## Recommendation

### For Live API Normal Mode

**Status**: ✅ Probably safe (needs confirmation testing)

**Action**: Test to confirm, but low priority

### For Live API Replay Mode

**Status**: ⚠️ Unknown, likely vulnerable

**Action**:
1. ⚡ **Test immediately** with invalid symbols
2. If crashes, apply same mitigation as Historical API
3. If doesn't crash, document why (different code path?)

### For All Live API Operations

**Action**: Apply universal mitigation to be safe

```csharp
public async Task SubscribeAsync(...)
{
    ExecuteNativeCall(() =>
        NativeMethods.dbento_live_subscribe(...),
        nameof(SubscribeAsync));
}

public async Task<Metadata> StartAsync(...)
{
    return await Task.Run(() =>
        ExecuteNativeCall(() =>
            NativeMethods.dbento_live_start_ex(...),
            nameof(StartAsync)));
}
```

---

## Best Practice: Always Validate Symbols First

```csharp
public async Task<bool> ValidateSymbols(
    string dataset,
    string[] symbols,
    DateTimeOffset date)
{
    await using var client = new HistoricalClientBuilder()
        .WithApiKey(_apiKey)
        .Build();

    // Use symbology API to validate
    var resolution = await client.SymbologyResolveAsync(
        dataset: dataset,
        symbols: symbols,
        stypeIn: SType.RawSymbol,
        stypeOut: SType.InstrumentId,
        startDate: DateOnly.FromDateTime(date.DateTime),
        endDate: DateOnly.FromDateTime(date.DateTime));

    if (resolution.NotFound.Count > 0)
    {
        Console.WriteLine($"Invalid symbols: {string.Join(", ", resolution.NotFound)}");
        return false;
    }

    return true;
}

// Usage
if (await ValidateSymbols("GLBX.MDP3", ["CL"], DateTime.Now))
{
    // Safe to subscribe
    await client.SubscribeAsync(...);
}
```

---

## Summary

### What Happens with Bad Ticker in Live API?

**Normal Mode (Real-time)**:
```
✅ Graceful handling
   ↓
Metadata returned with not_found: ["CL"]
   ↓
No data received for invalid symbols
   ↓
Application continues normally
```

**Replay Mode**:
```
❓ Unknown - needs testing

Possibility A: Graceful (like normal mode)
Possibility B: Crash (like Historical API)
```

**Recommendation**: Apply mitigation to Live API as well, especially for Replay mode.

---

## Action Items

- [ ] Test Live API normal mode with invalid symbols (low priority - likely safe)
- [ ] **Test Live API replay mode with invalid symbols** (high priority - may crash)
- [ ] Test Live API with invalid dataset
- [ ] Apply universal mitigation to all Live API operations
- [ ] Document actual behavior after testing
- [ ] Update bug report with Live API findings

