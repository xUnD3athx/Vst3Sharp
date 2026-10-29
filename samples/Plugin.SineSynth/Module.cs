using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vst3.Plugin;

namespace Plugin.SineSynth;

public static unsafe class Module
{
    private static readonly PluginDescriptor Descriptor = new(
        Name: "SineSynth",
        ClassId: new Guid("B2C3D4E5-0000-4000-8000-000000000002"),
        Vendor: "UnD3ath",
        Url: "https://example.invalid",
        Email: "dev@example.invalid",
        SubCategories: "Instrument|Synth",
        Version: "1.0.0",
        Parameters: new ParameterDescriptor[]
        {
            new(0, "Level", "Level", "", Min: 0.0, Max: 1.0, Default: 0.5),
        },
        CreateProcessor: static () => new SineSynthProcessor(),
        Kind: PluginKind.Instrument);

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
