using System.Numerics;
using Vst3.Editor;

namespace Plugin.SineSynth;

/// <summary>Example custom editor: one hand-drawn level knob.</summary>
public sealed class SineSynthEditor : EditorView
{
    private const int LevelIndex = 0;
    private const uint LevelId = 0;
    private const float KnobRadius = 54.0f;

    private bool _dragging;
    private float _centerX;
    private float _centerY;

    public override void OnRender(UiCanvas ui)
    {
        ui.Rect(0, 0, ui.Width, ui.Height, new UiColor(22, 22, 26));
        ui.RoundRect(16, 16, ui.Width - 32, ui.Height - 32, 10, new UiColor(32, 32, 38));
        ui.RectOutline(16, 16, ui.Width - 32, ui.Height - 32, new UiColor(70, 70, 84), 2, 10);

        ui.Text(36, 32, "SineSynth", new UiColor(238, 238, 242), 24);
        ui.Text(36, 64, "16-voice sine", new UiColor(132, 132, 144), 14);

        _centerX = ui.Width * 0.5f;
        _centerY = ui.Height * 0.54f;

        double level = Parameters.GetNormalized(LevelIndex);
        float angle = (float)(-Math.PI * 0.75 + level * Math.PI * 1.5);

        ui.Circle(_centerX, _centerY, KnobRadius + 8, new UiColor(46, 46, 54));
        ui.Circle(_centerX, _centerY, KnobRadius, new UiColor(28, 28, 34));
        ui.Line(
            _centerX,
            _centerY,
            _centerX + MathF.Cos(angle) * (KnobRadius - 10),
            _centerY + MathF.Sin(angle) * (KnobRadius - 10),
            new UiColor(0, 200, 255),
            4.0f);

        string value = (level * 100.0).ToString("0") + "%";
        Vector2 size = ui.MeasureText(value, 20);
        ui.Text(_centerX - size.X / 2, _centerY + KnobRadius + 18, value, new UiColor(220, 220, 228), 20);

        ui.Text(36, ui.Height - 40, "drag the knob or use the wheel", new UiColor(110, 110, 122), 13);
    }

    public override void OnPointerDown(in UiPointerEvent e)
    {
        if (e.Button != UiMouseButton.Left)
            return;

        float dx = e.X - _centerX;
        float dy = e.Y - _centerY;
        float reach = KnobRadius + 12;
        if (dx * dx + dy * dy > reach * reach)
            return;

        _dragging = true;
        BeginEdit(LevelId);
        SetFromY(e.Y);
    }

    public override void OnPointerMove(in UiPointerEvent e)
    {
        if (_dragging)
            SetFromY(e.Y);
    }

    public override void OnPointerUp(in UiPointerEvent e)
    {
        if (!_dragging)
            return;

        _dragging = false;
        EndEdit(LevelId);
    }

    public override void OnWheel(in UiWheelEvent e)
    {
        double level = Parameters.GetNormalized(LevelIndex) + e.Delta * 0.02;
        SetParameter(LevelId, Math.Clamp(level, 0.0, 1.0));
    }

    private void SetFromY(float y)
    {
        float top = _centerY - KnobRadius;
        float value = 1.0f - (y - top) / (KnobRadius * 2.0f);
        SetParameter(LevelId, Math.Clamp(value, 0.0f, 1.0f));
    }
}
