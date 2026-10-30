using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ImGuiNET;
using SkiaSharp;

namespace Vst3.Editor;

/// <summary>
/// NativeAOT resolves DllImport names from the host executable directory, not from the
/// plugin module directory. Preload cimgui/Skia from the module directory by absolute path.
/// </summary>
internal static unsafe class NativeLibraryBootstrap
{
    private static bool s_initialized;
    private static string? s_directory;

    internal static string? ModuleDirectory
    {
        get
        {
            s_directory ??= GetModuleDirectory();
            return s_directory;
        }
    }

    public static void Ensure()
    {
        if (s_initialized)
            return;

        s_initialized = true;
        if (ModuleDirectory == null)
            return;

        Preload("cimgui.dll");
        Preload("libSkiaSharp.dll");

        TryRegister(typeof(ImGui).Assembly);
        TryRegister(typeof(SKBitmap).Assembly);
    }

    private static void Preload(string fileName)
    {
        string candidate = Path.Combine(s_directory!, fileName);
        if (File.Exists(candidate))
            NativeLibrary.TryLoad(candidate, out _);
    }

    private static void TryRegister(Assembly assembly)
    {
        try
        {
            NativeLibrary.SetDllImportResolver(assembly, Resolve);
        }
        catch (InvalidOperationException)
        {
            // Another resolver is already installed for this assembly; preloading is enough.
        }
    }

    private static nint Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (s_directory == null)
            return 0;

        string fileName = name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? name : name + ".dll";
        string candidate = Path.Combine(s_directory, fileName);
        return File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out nint handle) ? handle : 0;
    }

    private static string? GetModuleDirectory()
    {
        nint address = (nint)(delegate* unmanaged[Stdcall]<void>)&ModuleAnchor;
        if (GetModuleHandleExW(0x00000004 | 0x00000002, address, out nint module) == 0)
            return AppContext.BaseDirectory;

        char* buffer = stackalloc char[1024];
        uint length = GetModuleFileNameW(module, buffer, 1024);
        if (length == 0)
            return AppContext.BaseDirectory;

        return Path.GetDirectoryName(new string(buffer, 0, (int)length));
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static void ModuleAnchor()
    {
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int GetModuleHandleExW(uint flags, nint address, out nint module);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetModuleFileNameW(nint module, char* fileName, uint size);
}
