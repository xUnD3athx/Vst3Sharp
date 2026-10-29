namespace Vst3.Interop;

/// <summary>
/// Interface IDs from the VST3 headers. On Windows the TUID bytes have COM layout,
/// so they round-trip to <see cref="Guid"/> directly.
/// </summary>
public static class IIds
{
    public static readonly Guid FUnknown = new("00000000-0000-0000-C000-000000000046");
    public static readonly Guid IPluginBase = new("22888DDB-156E-45AE-8358-B34808190625");
    public static readonly Guid IPluginFactory = new("7A4D811C-5211-4A1F-AED9-D2EE0B43BF9F");
    public static readonly Guid IPluginFactory2 = new("0007B650-F24B-4C0B-A464-EDB9F00B2ABB");
    public static readonly Guid IComponent = new("E831FF31-F2D5-4301-928E-BBEE25697802");
    public static readonly Guid IAudioProcessor = new("42043F99-B7DA-453C-A569-E79D9AAEC33D");
    public static readonly Guid IEditController = new("DCD7BBE3-7742-448D-A874-AACC979C759E");
    public static readonly Guid IPlugView = new("5BC32507-D060-49EA-A615-1B522B755B29");
    public static readonly Guid IBStream = new("C3BF6EA2-3099-4752-9B6B-F9901EE33E9B");
    public static readonly Guid IComponentHandler = new("93A0BEA3-0BD0-45DB-8E89-0B0CC1E46AC6");
}
