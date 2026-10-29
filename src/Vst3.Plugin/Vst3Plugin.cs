namespace Vst3.Plugin;

/// <summary>Entry points used by the module exports of a plugin assembly.</summary>
public static unsafe class Vst3Plugin
{
    private static PluginDescriptor? s_descriptor;

    public static void Configure(PluginDescriptor descriptor) => s_descriptor = descriptor;

    /// <summary>
    /// Returns a new <c>IPluginFactory</c>. Never throws: an exception crossing an
    /// <c>[UnmanagedCallersOnly]</c> boundary terminates the host process.
    /// </summary>
    public static void* GetFactory()
    {
        try
        {
            var descriptor = s_descriptor;
            return descriptor is null ? null : PluginFactory.Create(descriptor);
        }
        catch
        {
            return null;
        }
    }
}
