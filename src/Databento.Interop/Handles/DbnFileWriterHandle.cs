using System.Runtime.InteropServices;
using Databento.Interop.Native;

namespace Databento.Interop.Handles;

/// <summary>
/// SafeHandle wrapper for native DBN file writer
/// </summary>
public sealed class DbnFileWriterHandle : SafeHandle
{
    public DbnFileWriterHandle() : base(IntPtr.Zero, ownsHandle: true)
    {
    }

    public DbnFileWriterHandle(IntPtr handle) : base(IntPtr.Zero, ownsHandle: true)
    {
        SetHandle(handle);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        if (!IsInvalid)
        {
            NativeMethods.dbento_dbn_file_close_writer(handle);
        }
        return true;
    }
}
