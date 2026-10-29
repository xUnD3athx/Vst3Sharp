using Vst3.Plugin;

namespace Vst3.Editor;

/// <summary>Author-facing editor. Draw in <see cref="OnRender"/>; input arrives through the pointer/key callbacks.</summary>
public abstract class EditorView
{
    public PluginViewContext Context { get; private set; } = null!;

    public UiInput Input { get; } = new();

    public int Width { get; private set; }

    public int Height { get; private set; }

    protected ParameterCollection Parameters => Context.Parameters;

    protected void SetParameter(uint id, double normalized) => Context.SetParameter(id, normalized);

    protected void BeginEdit(uint id) => Context.BeginEdit(id);

    protected void EndEdit(uint id) => Context.EndEdit(id);

    protected internal virtual void OnAttach()
    {
    }

    protected internal virtual void OnDetach()
    {
    }

    public virtual void OnRender(UiCanvas canvas)
    {
    }

    public virtual void OnPointerDown(in UiPointerEvent e)
    {
    }

    public virtual void OnPointerUp(in UiPointerEvent e)
    {
    }

    public virtual void OnPointerMove(in UiPointerEvent e)
    {
    }

    public virtual void OnWheel(in UiWheelEvent e)
    {
    }

    public virtual void OnKey(in UiKeyEvent e)
    {
    }

    /// <summary>Printable character from WM_CHAR; IME is not supported yet.</summary>
    public virtual void OnText(char c)
    {
    }

    public virtual void OnFocusChanged(bool focused)
    {
    }

    public virtual void OnResize(int width, int height)
    {
    }

    internal void Attach(PluginViewContext context, int width, int height)
    {
        Context = context;
        Width = width;
        Height = height;
        OnAttach();
    }

    internal void Detach() => OnDetach();

    internal void Resize(int width, int height)
    {
        Width = width;
        Height = height;
        OnResize(width, height);
    }
}
