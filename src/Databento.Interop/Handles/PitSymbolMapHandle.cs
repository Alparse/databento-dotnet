using System.Runtime.InteropServices;
using Databento.Interop.Native;

namespace Databento.Interop.Handles;

/// <summary>
/// SafeHandle wrapper for native PitSymbolMap handle
/// </summary>
public sealed class PitSymbolMapHandle : SafeHandle
{
    public PitSymbolMapHandle() : base(IntPtr.Zero, ownsHandle: true)
    {
    }

    public PitSymbolMapHandle(IntPtr handle) : base(IntPtr.Zero, ownsHandle: true)
    {
        SetHandle(handle);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        if (!IsInvalid)
        {
            NativeMethods.dbento_pit_symbol_map_destroy(handle);
        }
        return true;
    }
}
