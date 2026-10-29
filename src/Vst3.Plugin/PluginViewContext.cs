namespace Vst3.Plugin;

/// <summary>Everything an editor implementation needs from the plugin instance.</summary>
public sealed class PluginViewContext
{
    public required ParameterCollection Parameters { get; init; }

    /// <summary>Applies a normalized value and notifies the host (performEdit).</summary>
    public required Action<uint, double> SetParameter { get; init; }

    public required Action<uint> BeginEdit { get; init; }

    public required Action<uint> EndEdit { get; init; }

    public int Width { get; init; } = 520;

    public int Height { get; init; } = 320;
}

/// <summary>Creates a native <c>IPlugView</c> for a plugin instance.</summary>
public interface IPluginViewFactory
{
    nint CreateView(PluginViewContext context);
}
