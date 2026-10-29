namespace Vst3.Plugin;

/// <summary>Static description of the plugin class exposed by a module.</summary>
public sealed record PluginDescriptor(
    string Name,
    Guid ClassId,
    string Vendor = "Unknown",
    string Url = "",
    string Email = "",
    string Category = "Audio Module Class",
    string SubCategories = "Fx",
    string Version = "1.0.0",
    IReadOnlyList<ParameterDescriptor>? Parameters = null,
    Func<AudioProcessor>? CreateProcessor = null);
