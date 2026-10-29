using Vst3.Plugin;

namespace Vst3.Editor;

public sealed class ImGuiEditorFactory : IPluginViewFactory
{
    public nint CreateView(PluginViewContext context) => ImGuiPlugView.Create(context, new ParameterEditorView());
}

/// <summary>Hosts an author-provided <see cref="EditorView"/> in the shared window/renderer shell.</summary>
public sealed class ImGuiEditorFactory<TView> : IPluginViewFactory
    where TView : EditorView, new()
{
    public nint CreateView(PluginViewContext context) => ImGuiPlugView.Create(context, new TView());
}
