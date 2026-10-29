using System.Numerics;
using ImGuiNET;

namespace Vst3.Editor;

/// <summary>Thin drawing surface over ImGui's draw list. Valid only inside <see cref="EditorView.OnRender"/>.</summary>
public readonly unsafe ref struct UiCanvas
{
    private readonly ImDrawListPtr _drawList;

    internal UiCanvas(ImDrawListPtr drawList, float width, float height)
    {
        _drawList = drawList;
        Width = width;
        Height = height;
    }

    public float Width { get; }

    public float Height { get; }

    public void Rect(float x, float y, float width, float height, UiColor color) =>
        _drawList.AddRectFilled(new Vector2(x, y), new Vector2(x + width, y + height), color.ToPacked());

    public void RoundRect(float x, float y, float width, float height, float radius, UiColor color) =>
        _drawList.AddRectFilled(
            new Vector2(x, y), new Vector2(x + width, y + height), color.ToPacked(), radius);

    public void RectOutline(float x, float y, float width, float height, UiColor color, float thickness = 1.0f) =>
        _drawList.AddRect(
            new Vector2(x, y), new Vector2(x + width, y + height), color.ToPacked(), 0.0f, ImDrawFlags.None, thickness);

    public void Circle(float centerX, float centerY, float radius, UiColor color, bool filled = true)
    {
        var center = new Vector2(centerX, centerY);
        if (filled)
            _drawList.AddCircleFilled(center, radius, color.ToPacked());
        else
            _drawList.AddCircle(center, radius, color.ToPacked());
    }

    public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness = 1.0f) =>
        _drawList.AddLine(new Vector2(x1, y1), new Vector2(x2, y2), color.ToPacked(), thickness);

    public void Text(float x, float y, string text, UiColor color, float size = 0.0f)
    {
        ImFontPtr font = ImGui.GetFont();
        float fontSize = size > 0.0f ? size : ImGui.GetFontSize();
        _drawList.AddText(font, fontSize, new Vector2(x, y), color.ToPacked(), text);
    }

    public Vector2 MeasureText(string text, float size = 0.0f)
    {
        ImFontPtr font = ImGui.GetFont();
        float fontSize = size > 0.0f ? size : ImGui.GetFontSize();
        return font.CalcTextSizeA(fontSize, float.MaxValue, 0.0f, text);
    }

    public void PushClip(float x, float y, float width, float height) =>
        _drawList.PushClipRect(new Vector2(x, y), new Vector2(x + width, y + height), true);

    public void PopClip() => _drawList.PopClipRect();
}
