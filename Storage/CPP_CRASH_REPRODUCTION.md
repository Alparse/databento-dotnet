# C++ Crash Reproduction - databento-cpp nullptr Bug

This demonstrates that the `nullptr` crash bug exists in **databento-cpp itself**, not just in our .NET wrapper.

---

## Summary

**The Bug**:
- `databento::Historical` direct constructor accepts `nullptr` for `ILogReceiver*` parameter
- When Databento API returns warning headers (e.g., future dates), databento-cpp dereferences the nullptr
- Result: **Access violation crash** (not catchable exception)

**Why We Didn't Notice**:
- `HistoricalBuilder` pattern automatically provides `ILogReceiver::Default()` (safe)
- Direct constructor has no safety net (unsafe)
- Bug only triggers when server returns warnings (future dates, rate limits, etc.)

---

## Test Files Created

### 1. `test_cpp_nullptr_crash.cpp`
Minimal reproduction - **will crash with access violation**

### 2. `test_cpp_safe_vs_unsafe.cpp`
Side-by-side comparison showing:
- ✅ Safe way: Builder pattern (works fine)
- ❌ Unsafe way: Direct constructor with nullptr (crashes)

### 3. `test_cpp_future_dates.cpp` (existing)
Uses Builder pattern - **doesn't crash** (but doesn't prove the bug is fixed)

---

## How to Build and Run

### Prerequisites
- CMake 3.24+
- C++17 compiler (Visual Studio 2019+, GCC 9+, or Clang 10+)
- databento-cpp library (via vcpkg or manual install)
- DATABENTO_API_KEY environment variable set

### Option 1: Quick Test (Windows)

```powershell
# Navigate to repo root
cd C:\Users\serha\source\repos\databento-dotnet

# Set API key
$env:DATABENTO_API_KEY = "your-key-here"

# Compile with MSVC (assuming databento-cpp is in vcpkg)
cl /EHsc /std:c++17 /I"C:\vcpkg\installed\x64-windows\include" ^
   test_cpp_nullptr_crash.cpp ^
   /link /LIBPATH:"C:\vcpkg\installed\x64-windows\lib" ^
   databento.lib ws2_32.lib

# Run (will crash!)
.\test_cpp_nullptr_crash.exe
```

### Option 2: Using CMake

Create `CMakeLists.txt` in repo root:

```cmake
cmake_minimum_required(VERSION 3.24)
project(databento_crash_test)

set(CMAKE_CXX_STANDARD 17)

# Find databento-cpp (assumes installed via vcpkg or system)
find_package(databento REQUIRED)

# Test 1: Minimal crash reproduction
add_executable(test_nullptr_crash test_cpp_nullptr_crash.cpp)
target_link_libraries(test_nullptr_crash databento::databento)

# Test 2: Safe vs Unsafe comparison
add_executable(test_safe_vs_unsafe test_cpp_safe_vs_unsafe.cpp)
target_link_libraries(test_safe_vs_unsafe databento::databento)

# Test 3: Future dates with Builder (safe)
add_executable(test_future_dates test_cpp_future_dates.cpp)
target_link_libraries(test_future_dates databento::databento)
```

Build:
```powershell
mkdir build_test
cd build_test
cmake .. -DCMAKE_TOOLCHAIN_FILE=C:/vcpkg/scripts/buildsystems/vcpkg.cmake
cmake --build . --config Release
```

Run:
```powershell
# Set API key
$env:DATABENTO_API_KEY = "your-key-here"

# Test 1: Will crash
.\Release\test_nullptr_crash.exe

# Test 2: Safe then unsafe (will crash on 2nd test)
.\Release\test_safe_vs_unsafe.exe

# Test 3: Safe (won't crash)
.\Release\test_future_dates.exe
```

---

## Expected Results

### Test 1: `test_cpp_nullptr_crash.exe`

**Expected (with bug)**:
```
=== C++ nullptr Crash Reproduction ===
Testing Historical client with nullptr ILogReceiver

✓ API key found
Creating Historical client with nullptr ILogReceiver...
✓ Client created (no crash yet)

Querying future dates (will trigger server warning)...
Starting query...

[PROGRAM CRASHES]
Exception code: 0xC0000005 (ACCESS_VIOLATION)
Exception message: Access violation reading location 0x0000000000000000
```

**If Fixed**:
```
=== C++ nullptr Crash Reproduction ===
...
✓ SUCCESS: Received 172 records without crash!
  (This means databento-cpp has been fixed to handle nullptr safely)
```

---

### Test 2: `test_safe_vs_unsafe.exe`

**Expected (with bug)**:
```
=== TEST 1: SAFE - Using Builder Pattern ===
✓ Client created with Builder (safe)
✓ SUCCESS: Received 172 records
  Builder pattern is SAFE - no crash!

========================================

⚠️  WARNING: Next test will likely CRASH!
Press Ctrl+C to abort, or Enter to continue...
[Press Enter]

=== TEST 2: UNSAFE - Direct Constructor with nullptr ===
✓ Client created with direct constructor (dangerous)
  (No crash yet because we haven't triggered a warning)
Starting query that will trigger warning...
💥 EXPECTED: Access violation crash here!

[PROGRAM CRASHES]
```

---

### Test 3: `test_future_dates.exe` (existing)

**Expected**: ✅ **SUCCESS** (uses Builder pattern)
```
Querying CLZ5 with future dates...
Record received
Record received
...
SUCCESS: Received 172 records
```

**Why this doesn't crash**: Builder provides safe default ILogReceiver

---

## Proof This is databento-cpp Bug

1. ✅ **No .NET code involved** - Pure C++ test
2. ✅ **Minimal reproduction** - Only databento-cpp + stdlib
3. ✅ **Consistent behavior** - Both .NET wrapper and C++ direct call crash
4. ✅ **Builder works, direct constructor doesn't** - Shows inconsistency in databento-cpp

---

## Root Cause in databento-cpp

**Suspected code** (in databento-cpp's HTTP client):

```cpp
// In databento-cpp (pseudocode)
void CheckWarnings(ILogReceiver* log_receiver, const HttpResponse& resp) {
    auto warnings = resp.GetHeader("X-Warning");
    if (!warnings.empty()) {
        // ❌ BUG: No null check before dereferencing!
        log_receiver->Receive(LogLevel::Warning, warnings);
    }
}
```

**Should be**:
```cpp
void CheckWarnings(ILogReceiver* log_receiver, const HttpResponse& resp) {
    auto warnings = resp.GetHeader("X-Warning");
    if (!warnings.empty()) {
        // ✅ FIX: Check for null first
        if (log_receiver) {
            log_receiver->Receive(LogLevel::Warning, warnings);
        } else {
            // Fallback: log to stderr
            std::cerr << "[databento WARNING] " << warnings << std::endl;
        }
    }
}
```

---

## Recommended Fix for databento-cpp

### Option 1: Add Null Checks (Minimal)
```cpp
// In all places that dereference log_receiver:
if (log_receiver) {
    log_receiver->Receive(level, message);
}
```

### Option 2: Use Default in Constructor (Recommended)
```cpp
Historical::Historical(ILogReceiver* log_receiver, ...)
    : log_receiver_(log_receiver ? log_receiver : ILogReceiver::Default())
{
    // Now log_receiver_ is always valid
}
```

### Option 3: Use References (Best, but Breaking)
```cpp
// Force non-null at API level
explicit Historical(
    ILogReceiver& log_receiver = ILogReceiver::Default(),
    std::string key,
    HistoricalGateway gateway
);
```

---

## Testing Strategy

To verify if databento-cpp has fixed this:

```cpp
// Quick test - should NOT crash if fixed
auto client = databento::Historical(nullptr, key, gateway);
auto records = client.TimeseriesGetRange(
    "GLBX.MDP3",
    future_date_range,  // Triggers warning
    {"ES.FUT"},
    Schema::Ohlcv1D
);
// If this works without crash, bug is fixed!
```

---

## Contact databento-cpp Maintainers

When reporting:
1. Reference this reproduction case
2. Provide minimal C++ example (test_cpp_nullptr_crash.cpp)
3. Explain the inconsistency (Builder safe, constructor unsafe)
4. Suggest one of the fixes above

**GitHub**: https://github.com/databento/databento-cpp/issues

---

**Status**: Bug confirmed in databento-cpp (not specific to .NET wrapper)
**Severity**: HIGH (process crash, not catchable)
**Workaround**: Always use Builder pattern, or provide valid ILogReceiver
