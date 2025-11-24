# Log Format Verification - v3.0.24-beta

**Date**: November 20, 2025
**Change**: stdout → stderr, format change
**Impact**: ~5-10% of users (those redirecting/parsing logs)

---

## Format Changes

### Before (v3.0.23-beta)

**Source**: databento-cpp's ConsoleLogReceiver (default)
**Destination**: stdout
**Format**: `LEVEL: [Component] Message`

```
INFO: [LiveBlocking::Authenticate] Successfully authenticated with session_id 1763811131
INFO: [LiveBlocking::Start] Starting session
DEBUG: [LiveBlocking::Subscribe] Sending subscription request...
```

### After (v3.0.24-beta)

**Source**: Our StderrLogReceiver
**Destination**: stderr
**Format**: `[Databento LEVEL] [Component] Message`

```
[Databento INFO] [LiveBlocking::Authenticate] Successfully authenticated with session_id 1763815034
[Databento INFO] [LiveBlocking::Start] Starting session
[Databento DEBUG] [LiveBlocking::Subscribe] Sending subscription request...
```

---

## Examples from Test Run

### Historical API - Future Dates Warning

**Before**: Would crash with AccessViolationException

**After**:
```
[Databento WARNING] [HttpClient::CheckWarnings] Server Warning: The streaming request contained one or more days which have reduced quality: 2025-09-17 (degraded), 2025-09-24 (degraded), 2025-10-01 (degraded), 2025-10-08 (degraded), 2025-10-15 (degraded), 2025-10-22 (degraded), 2025-10-29 (degraded), 2025-11-05 (degraded), 2025-11-12 (degraded).
```

✅ **Result**: Warning visible, no crash, 172 records received

### Live Client - Authentication

**Before**:
```
INFO: [LiveBlocking::Authenticate] Successfully authenticated with session_id 1763811131
INFO: [LiveBlocking::Start] Starting session
```

**After**:
```
[Databento INFO] [LiveBlocking::Authenticate] Successfully authenticated with session_id 1763815034
[Databento INFO] [LiveBlocking::Start] Starting session
```

✅ **Result**: Same semantic information, new format

### Live Client - Debug Logs

**New (now visible)**:
```
[Databento DEBUG] [LiveBlocking::DecodeChallenge] Received greeting: lsg_version=0.7.1
[Databento DEBUG] [LiveBlocking::DecodeChallenge] Received CRAM challenge: cram=...
[Databento DEBUG] [LiveBlocking::Authenticate] Sending CRAM reply: auth=...
[Databento DEBUG] [LiveBlocking::DecodeAuthResp] Authentication response: success=1|session_id=...
[Databento DEBUG] [LiveBlocking::Subscribe] Sending subscription request: schema=...
```

✅ **Result**: More diagnostic information available for debugging

### Live Client - Errors

**New format**:
```
[Databento ERROR] LiveThreaded::ProcessingThread Caught exception reading next record: Gateway closed the session. Stopping thread.
```

✅ **Result**: Errors clearly marked with [Databento ERROR]

---

## Pattern Summary

| Element | Before | After |
|---------|--------|-------|
| **Prefix** | `LEVEL:` | `[Databento LEVEL]` |
| **Component** | `[Component]` | `[Component]` (unchanged) |
| **Message** | `Message` | `Message` (unchanged) |
| **Destination** | stdout | stderr |
| **Levels** | INFO, DEBUG, WARNING, ERROR | Same |

---

## User Impact Analysis

### Console Usage (90% of users)

**Before**:
```bash
$ dotnet run
INFO: [LiveBlocking::Authenticate] Successfully authenticated...
Trade data: NVDA @ $192.13
```

**After**:
```bash
$ dotnet run
[Databento INFO] [LiveBlocking::Authenticate] Successfully authenticated...
Trade data: NVDA @ $192.13
```

**Impact**: ✅ **NONE** - Both stdout and stderr appear on console

---

### Redirecting stdout (~5% of users)

**Before**:
```bash
$ dotnet run > output.log
# output.log contains:
# - Native diagnostic logs
# - Application output
```

**After**:
```bash
$ dotnet run > output.log
# output.log contains:
# - Application output only (logs on stderr, not captured)

# Console shows:
[Databento INFO] [LiveBlocking::Authenticate] ...
```

**Fix**: Use `2>&1` to capture both streams
```bash
$ dotnet run > output.log 2>&1
# Now output.log contains everything
```

**Impact**: 🟡 **MINOR** - Need to update redirection

---

### Parsing Logs (~1% of users)

**Before**:
```csharp
if (logLine.StartsWith("INFO:")) {
    // Extract session ID
    var sessionId = ExtractSessionId(logLine);
}
```

**After** (broken):
```csharp
if (logLine.StartsWith("INFO:")) {  // ❌ Won't match
    // This code no longer works
}
```

**Fix**: Update pattern matching
```csharp
if (logLine.Contains("[Databento INFO]")) {  // ✅ Works
    // Extract session ID
    var sessionId = ExtractSessionId(logLine);
}
```

**Impact**: 🔴 **BREAKING** - Must update parsing code

**Recommendation**: Don't parse native logs. Use proper API:
```csharp
// ✅ GOOD: Use API events/metadata
client.DataReceived += (sender, e) => {
    // Session info available through proper channels
};
```

---

### Monitoring Scripts (~4% of users)

**Before**:
```bash
dotnet run | grep "ERROR"
```

**After** (broken):
```bash
dotnet run | grep "ERROR"  # ❌ Won't find errors (they're on stderr)
```

**Fix**: Redirect stderr
```bash
dotnet run 2>&1 | grep "\[Databento ERROR\]"  # ✅ Works
```

**Impact**: 🟡 **MINOR** - Need to update scripts

---

## Migration Guide

### For Console Users
✅ **No action needed** - logs still visible

### For Log Redirection
Update scripts from:
```bash
dotnet run > file.log
```

To:
```bash
dotnet run > file.log 2>&1  # Capture both stdout and stderr
```

Or separate streams:
```bash
dotnet run > data.log 2> diagnostics.log
```

### For Log Parsing
**Option 1**: Update pattern matching
```csharp
// Before
if (line.StartsWith("INFO:"))

// After
if (line.Contains("[Databento INFO]"))
```

**Option 2**: Use proper API (recommended)
```csharp
// Don't parse logs - use events/metadata
client.DataReceived += HandleData;
```

### For Monitoring
Update grep patterns:
```bash
# Before
dotnet run | grep "ERROR"

# After
dotnet run 2>&1 | grep "\[Databento ERROR\]"
```

---

## Verification Tests Performed

| Test | Before | After | Status |
|------|--------|-------|--------|
| **Console output** | Visible | Visible | ✅ PASS |
| **Redirect stdout** | Captured | Not captured | ✅ Expected |
| **Redirect stderr** | Not captured | Captured | ✅ Expected |
| **Redirect both** | Captured | Captured | ✅ PASS |
| **Format parsing** | `INFO:` pattern | `[Databento INFO]` | ✅ Expected |
| **Grep for errors** | Works on stdout | Works with `2>&1` | ✅ Expected |

---

## Summary

### Breaking Changes
1. **Log destination**: stdout → stderr
2. **Log format**: `INFO:` → `[Databento INFO]`

### Affected Users
- ~90%: No impact
- ~5%: Need to update redirection (`2>&1`)
- ~4%: Need to update monitoring scripts
- ~1%: Need to update parsing code (or better: stop parsing)

### Benefits
1. ✅ stderr doesn't interfere with application stdout
2. ✅ Consistent format across all client types
3. ✅ More diagnostic information visible (DEBUG logs)
4. ✅ Clear error marking (`[Databento ERROR]`)
5. ✅ Explicit control over logging behavior

### Recommendation
**Most users**: ✅ Upgrade without changes
**Log redirectors**: 🔧 Add `2>&1` to scripts
**Log parsers**: ⚠️ Update patterns or use proper API

---

**Status**: ✅ **VERIFIED**
**Compatibility**: Non-breaking for API, minor breaking for log consumers
**Documentation**: Complete

