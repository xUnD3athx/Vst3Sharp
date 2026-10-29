using Vst3.Plugin;

namespace Vst3.Editor;

public sealed class ImGuiEditorFactory : IPluginViewFactory
{
    public nint CreateView(PluginViewContext context) => ImGuiPlugView.Create(context);
}
