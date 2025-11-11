using System.Runtime.InteropServices;
using Databento.Interop.Native;

namespace Databento.Interop.Handles;

/// <summary>
/// SafeHandle wrapper for native TsSymbolMap handle
/// </summary>
public sealed class TsSymbolMapHandle : SafeHandle
{
    public TsSymbolMapHandle() : base(IntPtr.Zero, ownsHandle: true)
    {
    }

    public TsSymbolMapHandle(IntPtr handle) : base(IntPtr.Zero, ownsHandle: true)
    {
        SetHandle(handle);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        if (!IsInvalid)
        {
            NativeMethods.dbento_ts_symbol_map_destroy(handle);
        }
        return true;
    }
}
