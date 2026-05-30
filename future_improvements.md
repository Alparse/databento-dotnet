# Future Improvements

## Polish

### Safe (non-breaking)

**1. Add logging to `SubscribeWithSnapshotAsync` in `LiveClient.cs`**
- File: `src/Databento.Client/Live/LiveClient.cs` (lines ~327-370)
- `SubscribeAsync` logs on entry and success. `SubscribeWithSnapshotAsync` has zero logging.
- Fix: Add `_logger.LogInformation(...)` for entry and success to match `SubscribeAsync` pattern.

**2. Inline unnecessary local variable in `BlockUntilStoppedAsync` in `LiveClient.cs`**
- File: `src/Databento.Client/Live/LiveClient.cs` (line ~880)
- `var streamTask = Interlocked.CompareExchange(ref _streamTask, null, null);` assigns to a variable only used for a null check.
- Fix: Change to `if (Interlocked.CompareExchange(ref _streamTask, null, null) == null)`.
- Note: Same pattern exists in the `BlockUntilStoppedAsync(TimeSpan timeout, ...)` overload.

### Potentially Breaking

**3. Add `ConfigureAwait(false)` to `StreamAsync` in `LiveClient.cs`**
- File: `src/Databento.Client/Live/LiveClient.cs` (line ~615)
- `await foreach (var record in _recordChannel.Reader.ReadAllAsync(cancellationToken))` is missing `.ConfigureAwait(false)`.
- Standard best practice for library code — not having it can cause deadlocks in environments with a `SynchronizationContext`.
- **Why breaking:** Changes thread affinity for callers using the library in WPF, WinForms, or ASP.NET apps with a `SynchronizationContext`. Their `await foreach` would resume on a thread pool thread instead of their original context. We don't know all use cases of this library.

**4. Remove `async`/`await Task.CompletedTask` from `ResubscribeAsync` in `LiveClient.cs`**
- File: `src/Databento.Client/Live/LiveClient.cs` (lines ~568-586)
- Method is marked `async` and ends with `await Task.CompletedTask`. The `async` keyword generates a state machine for no reason — the method is entirely synchronous (P/Invoke call).
- Correct pattern: remove `async`, return `Task.CompletedTask` (same as `SubscribeAsync` does).
- **Why breaking:** With `async`, exceptions from the P/Invoke are captured into the returned `Task` and thrown at the `await` site. Without `async`, exceptions throw synchronously at the call site. For callers who `await` immediately, no difference. For callers who capture the `Task` and `await` later, the exception timing changes.

**5. Replace `ConcurrentBag` with `List` for `_subscriptions` in `LiveClient.cs`**
- File: `src/Databento.Client/Live/LiveClient.cs` (line ~38)
- `ConcurrentBag` was a defensive choice. All writes (`_subscriptions.Add`) happen during `SubscribeAsync` which is synchronous and completes before `StartAsync`. After `Start`, the collection is read-only. `List` is safe for concurrent reads with no concurrent writes.
- databento-cpp uses a plain `std::vector` — `ConcurrentBag` is a fidelity divergence.
- The comment "HIGH FIX: Use thread-safe collection for concurrent subscription operations" was added during a code quality pass but the concurrent write scenario doesn't materialize in the current code. `ResubscribeAsync` uses native resubscribe (`dbento_live_resubscribe`), not the managed collection.
- `ConcurrentBag` is on the cold setup path (not the hot data path) and has zero overhead when uncontended, so it's harmless. It differs from `SemaphoreSlim` (which would serialize P/Invoke operations on the hot path).
- **Why breaking:** If a code path exists that we haven't identified where a background thread accesses `_subscriptions`, replacing with `List` introduces a race condition. Requires thorough analysis and testing before changing.

### Cosmetic (low priority)

**6. Shorten `Models.Dbn.DbnMetadata` references in `LiveClient.cs`**
- File: `src/Databento.Client/Live/LiveClient.cs`
- Fully qualified `Models.Dbn.DbnMetadata` is used throughout. Could add a `using` directive and use `DbnMetadata`.
- Purely cosmetic, zero runtime impact. Not worth the churn unless touching the file for other reasons.
