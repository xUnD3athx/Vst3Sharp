using System.Runtime.InteropServices;
using Vst3.Interop;

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: Host.Manual <path-to-plugin.dll>");
    return 1;
}

string path = Path.GetFullPath(args[0]);
if (!File.Exists(path))
{
    Console.Error.WriteLine($"not found: {path}");
    return 2;
}

unsafe
{
    nint library = NativeLibrary.Load(path);
    try
    {
        if (NativeLibrary.TryGetExport(library, "InitDll", out nint initDll))
        {
            byte ok = ((delegate* unmanaged[Stdcall]<byte>)initDll)();
            Console.WriteLine($"InitDll -> {ok}");
        }

        if (!NativeLibrary.TryGetExport(library, "GetPluginFactory", out nint getFactoryPtr))
        {
            Console.Error.WriteLine("GetPluginFactory is not exported");
            return 3;
        }

        void* factory = ((delegate* unmanaged[Stdcall]<void*>)getFactoryPtr)();
        if (factory == null)
        {
            Console.Error.WriteLine("GetPluginFactory returned null");
            return 4;
        }

        nint* vt = Vtable.Of(factory);
        var release = (delegate* unmanaged[Stdcall]<void*, uint>)vt[2];
        var getFactoryInfo = (delegate* unmanaged[Stdcall]<void*, PFactoryInfo*, int>)vt[3];
        var countClasses = (delegate* unmanaged[Stdcall]<void*, int>)vt[4];
        var getClassInfo = (delegate* unmanaged[Stdcall]<void*, int, PClassInfo*, int>)vt[5];

        PFactoryInfo info = default;
        int hr = getFactoryInfo(factory, &info);
        Console.WriteLine(
            $"getFactoryInfo -> 0x{hr:X8} vendor=\"{info.GetVendor()}\" url=\"{info.GetUrl()}\" email=\"{info.GetEmail()}\" flags=0x{info.Flags:X}");

        int count = countClasses(factory);
        Console.WriteLine($"countClasses -> {count}");

        for (int i = 0; i < count; i++)
        {
            PClassInfo classInfo = default;
            int classHr = getClassInfo(factory, i, &classInfo);
            Console.WriteLine(
                $"[{i}] hr=0x{classHr:X8} cid={classInfo.Cid} category=\"{classInfo.GetCategory()}\" name=\"{classInfo.GetName()}\"");
        }

        uint remaining = release(factory);
        Console.WriteLine($"release -> {remaining}");

        if (NativeLibrary.TryGetExport(library, "ExitDll", out nint exitDll))
        {
            byte ok = ((delegate* unmanaged[Stdcall]<byte>)exitDll)();
            Console.WriteLine($"ExitDll -> {ok}");
        }
    }
    finally
    {
        NativeLibrary.Free(library);
    }
}

return 0;
