# Migration Guide: v3.0.29-beta → v4.0.0-beta

## Breaking Change

`InstrumentDefMessage.RawInstrumentId` changed from `uint` (32-bit) to `ulong` (64-bit).

**Reason**: Some venues like Eurex use 64-bit instrument IDs exceeding `uint.MaxValue` (4,294,967,295).

## Am I Affected?

**No** if you:
- Use `var` for type inference
- Don't use `RawInstrumentId` at all
- Only query US equities (IDs fit in uint)

**Yes** if you:
- Explicitly declare `uint rawId = instrumentDef.RawInstrumentId`
- Store RawInstrumentId in database INT column
- Serialize/deserialize this property

## How to Fix

### 1. Update Type Declarations

```csharp
// OLD
uint rawId = instrumentDef.RawInstrumentId;

// NEW
ulong rawId = instrumentDef.RawInstrumentId;
```

### 2. Update Database Schema (if applicable)

**SQL Server:**
```sql
ALTER TABLE Instruments ALTER COLUMN RawInstrumentId BIGINT;
```

**PostgreSQL:**
```sql
ALTER TABLE instruments ALTER COLUMN raw_instrument_id TYPE BIGINT;
```

**MySQL:**
```sql
ALTER TABLE instruments MODIFY COLUMN raw_instrument_id BIGINT UNSIGNED;
```

### 3. Update Serialization (if applicable)

```csharp
public class InstrumentData
{
    // Change from uint to ulong
    [JsonProperty("rawInstrumentId")]
    public ulong RawInstrumentId { get; set; }
}
```

## Finding Affected Code

```bash
# Search for explicit uint declarations
grep -r "uint.*RawInstrumentId" .

# PowerShell
Get-ChildItem -Recurse -Include *.cs | Select-String "uint.*RawInstrumentId"
```

## FAQ

**Q: Will my code break?**
A: Only if you have explicit `uint` declarations. Code using `var` is unaffected.

**Q: Why not keep backwards compatibility?**
A: The data IS 64-bit from venues like Eurex. Truncating would lose information.

**Q: I only use US equities. Do I need to change?**
A: If your code compiles, you're fine. The change adds headroom with no downside.

**Q: What's the migration time?**
A: 5-30 minutes depending on codebase size. Typically just find/replace `uint` with `ulong`.

## Migration Checklist

- [ ] Search for `uint.*RawInstrumentId` in codebase
- [ ] Change explicit `uint` declarations to `ulong`
- [ ] Update database schema (if storing this field)
- [ ] Update serialization code (if any)
- [ ] Build and test: `dotnet build`

## Need Help?

Open an issue with your compiler error: https://github.com/Alparse/databento-dotnet/issues
