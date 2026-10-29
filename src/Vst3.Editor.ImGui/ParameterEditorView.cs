using System;
using System.Globalization;
using Vst3.Plugin;

namespace Vst3.Editor;

/// <summary>Default editor: one row per parameter, drawn on the shared canvas.</summary>
internal sealed class ParameterEditorView : EditorView
{
    private const float Margin = 16.0f;
    private const float Top = 52.0f;
    private const float RowHeight = 46.0f;
    private const float LabelWidth = 140.0f;

    private int _dragIndex = -1;
    private float _trackX;
    private float _trackWidth;

    public override void OnRender(UiCanvas ui)
    {
        ui.Rect(0, 0, ui.Width, ui.Height, new UiColor(24, 24, 28));
        ui.Text(Margin, 16, "Vst3Sharp", new UiColor(230, 230, 236), 20);
        ui.Line(Margin, 42, ui.Width - Margin, 42, new UiColor(52, 52, 60));

        _trackX = Margin + LabelWidth;
        _trackWidth = Math.Max(80.0f, ui.Width - _trackX - Margin);

        var parameters = Parameters;
        for (int i = 0; i < parameters.Count; i++)
        {
            var descriptor = parameters.GetDescriptor(i);
            double normalized = parameters.GetNormalized(i);
            float y = Top + i * RowHeight;

            ui.Text(Margin, y + 6, descriptor.Title, new UiColor(200, 200, 208), 15);

            float trackY = y + 10;
            ui.RoundRect(_trackX, trackY, _trackWidth, 8, 4, new UiColor(46, 46, 54));
            ui.RoundRect(_trackX, trackY, (float)(_trackWidth * normalized), 8, 4, new UiColor(64, 132, 220));
            ui.Circle(_trackX + (float)(_trackWidth * normalized), trackY + 4, 7, new UiColor(220, 230, 245));

            ui.Text(_trackX, y + 24, FormatValue(parameters, i, descriptor, normalized), new UiColor(150, 150, 160), 13);
        }
    }

    public override void OnPointerDown(in UiPointerEvent e)
    {
        if (e.Button != UiMouseButton.Left)
            return;

        int index = RowAt(e.Y);
        if (index < 0 || e.X < _trackX)
            return;

        _dragIndex = index;
        BeginEdit(Parameters.GetDescriptor(index).Id);
        SetFromX(index, e.X);
    }

    public override void OnPointerMove(in UiPointerEvent e)
    {
        if (_dragIndex >= 0)
            SetFromX(_dragIndex, e.X);
    }

    public override void OnPointerUp(in UiPointerEvent e)
    {
        if (_dragIndex < 0)
            return;

        EndEdit(Parameters.GetDescriptor(_dragIndex).Id);
        _dragIndex = -1;
    }

    public override void OnWheel(in UiWheelEvent e)
    {
        int index = RowAt(e.Y);
        if (index < 0)
            return;

        double value = Math.Clamp(Parameters.GetNormalized(index) + e.Delta * 0.02, 0.0, 1.0);
        SetParameter(Parameters.GetDescriptor(index).Id, value);
    }

    private int RowAt(float y)
    {
        int index = (int)MathF.Floor((y - Top) / RowHeight);
        return index >= 0 && index < Parameters.Count ? index : -1;
    }

    private void SetFromX(int index, float x)
    {
        if (_trackWidth <= 0.0f)
            return;

        double value = Math.Clamp((x - _trackX) / _trackWidth, 0.0, 1.0);
        SetParameter(Parameters.GetDescriptor(index).Id, value);
    }

    private static string FormatValue(
        ParameterCollection parameters, int index, ParameterDescriptor descriptor, double normalized)
    {
        string text = parameters.ToPlain(index, normalized).ToString("0.###", CultureInfo.InvariantCulture);
        return descriptor.Units.Length == 0 ? text : text + " " + descriptor.Units;
    }
}
