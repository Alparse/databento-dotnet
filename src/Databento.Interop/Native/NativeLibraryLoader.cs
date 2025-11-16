using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Databento.Interop.Native;

/// <summary>
/// Handles loading of native libraries with proper path resolution
/// </summary>
internal static class NativeLibraryLoader
{
    private static readonly object Lock = new();
    private static bool _initialized;

    [ModuleInitializer]
    internal static void Initialize()
    {
        if (_initialized)
            return;

        lock (Lock)
        {
            if (_initialized)
                return;

            NativeLibrary.SetDllImportResolver(typeof(NativeMethods).Assembly, DllImportResolver);
            _initialized = true;
        }
    }

    private static IntPtr DllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        // Only handle databento_native, let other DLLs load normally
        if (libraryName != "databento_native")
            return IntPtr.Zero;

        // Try to load from multiple locations
        var locations = GetSearchLocations();

        foreach (var location in locations)
        {
            var dllPath = Path.Combine(location, GetPlatformLibraryName(libraryName));

            if (File.Exists(dllPath))
            {
                if (NativeLibrary.TryLoad(dllPath, out var handle))
                {
                    return handle;
                }
            }
        }

        // Fallback to default resolution
        return IntPtr.Zero;
    }

    private static string GetPlatformLibraryName(string libraryName)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return $"{libraryName}.dll";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return $"lib{libraryName}.so";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return $"lib{libraryName}.dylib";

        return libraryName;
    }

    private static IEnumerable<string> GetSearchLocations()
    {
        var assemblyLocation = Path.GetDirectoryName(typeof(NativeMethods).Assembly.Location);
        var appBaseDirectory = AppContext.BaseDirectory;

        // 1. Application base directory (most common for published apps)
        yield return appBaseDirectory;

        // 2. Assembly location (where Databento.Interop.dll is)
        if (assemblyLocation != null && assemblyLocation != appBaseDirectory)
            yield return assemblyLocation;

        // 3. runtimes/{rid}/native folder in app base directory
        var rid = GetRuntimeIdentifier();
        if (rid != null)
        {
            yield return Path.Combine(appBaseDirectory, "runtimes", rid, "native");

            if (assemblyLocation != null)
                yield return Path.Combine(assemblyLocation, "runtimes", rid, "native");
        }

        // 4. Current working directory
        yield return Directory.GetCurrentDirectory();
    }

    private static string? GetRuntimeIdentifier()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "win-x64",
                Architecture.X86 => "win-x86",
                Architecture.Arm64 => "win-arm64",
                _ => null
            };
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "linux-x64",
                Architecture.Arm64 => "linux-arm64",
                _ => null
            };
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "osx-x64",
                Architecture.Arm64 => "osx-arm64",
                _ => null
            };
        }

        return null;
    }
}
