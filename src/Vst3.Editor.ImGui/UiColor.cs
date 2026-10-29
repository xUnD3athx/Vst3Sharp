namespace Vst3.Editor;

/// <summary>Straight RGBA color; converted to ImGui's ABGR on the way to the draw list.</summary>
public readonly struct UiColor
{
    public UiColor(byte r, byte g, byte b, byte a = 255)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public byte R { get; }

    public byte G { get; }

    public byte B { get; }

    public byte A { get; }

    internal uint ToPacked() => (uint)((A << 24) | (B << 16) | (G << 8) | R);
}
