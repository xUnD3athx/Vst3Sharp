using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vst3.Plugin;

namespace Plugin.Passthrough;

public static unsafe class Module
{
    private static readonly PluginDescriptor Descriptor = new(
        Name: "Passthrough",
        ClassId: new Guid("A1B2C3D4-0000-4000-8000-000000000001"),
        Vendor: "UnD3ath",
        Url: "https://example.invalid",
        Email: "dev@example.invalid",
        SubCategories: "Fx",
        Version: "1.0.0",
        Parameters: new ParameterDescriptor[]
        {
            new(0, "Gain", "Gain", "x", Min: 0.0, Max: 2.0, Default: 1.0),
            new(1, "SC Amount", "SC", "%", Min: 0.0, Max: 100.0, Default: 0.0),
        },
        CreateProcessor: static () => new GainProcessor());

    [UnmanagedCallersOnly(EntryPoint = "GetPluginFactory", CallConvs = new[] { typeof(CallConvStdcall) })]
    public static void* GetPluginFactory()
    {
        Vst3Plugin.Configure(Descriptor);
        return Vst3Plugin.GetFactory();
    }

    [UnmanagedCallersOnly(EntryPoint = "InitDll", CallConvs = new[] { typeof(CallConvStdcall) })]
    public static byte InitDll() => 1;

    [UnmanagedCallersOnly(EntryPoint = "ExitDll", CallConvs = new[] { typeof(CallConvStdcall) })]
    public static byte ExitDll() => 1;
}
