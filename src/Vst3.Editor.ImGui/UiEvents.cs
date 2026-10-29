using System;
using System.Numerics;

namespace Vst3.Editor;

[Flags]
public enum UiModifiers
{
    None = 0,
    Shift = 1,
    Control = 2,
    Alt = 4,
    Win = 8,
}

public enum UiMouseButton
{
    None = 0,
    Left = 1,
    Middle = 2,
    Right = 3,
}

public enum UiPointerAction
{
    Move,
    Down,
    Up,
}

/// <summary>Keys the framework knows by name; everything else arrives as <see cref="Unknown"/> with the raw virtual key.</summary>
public enum UiKey
{
    Unknown = 0,
    A = 1, B, C, D, E, F, G, H, I, J, K, L, M,
    N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
    D0 = 27, D1, D2, D3, D4, D5, D6, D7, D8, D9,
    Space = 37,
    Escape,
    Enter,
    Tab,
    Backspace,
    Delete,
    Left,
    Right,
    Up,
    Down,
    Home,
    End,
    PageUp,
    PageDown,
}

public readonly struct UiPointerEvent
{
    internal UiPointerEvent(UiPointerAction action, UiMouseButton button, float x, float y, UiModifiers modifiers)
    {
        Action = action;
        Button = button;
        X = x;
        Y = y;
        Modifiers = modifiers;
    }

    public UiPointerAction Action { get; }

    /// <summary>The button that changed; <see cref="UiMouseButton.None"/> for move events.</summary>
    public UiMouseButton Button { get; }

    /// <summary>View-local position in physical pixels.</summary>
    public float X { get; }

    public float Y { get; }

    public UiModifiers Modifiers { get; }

    public Vector2 Position => new(X, Y);
}

public readonly struct UiWheelEvent
{
    internal UiWheelEvent(float x, float y, float delta, UiModifiers modifiers)
    {
        X = x;
        Y = y;
        Delta = delta;
        Modifiers = modifiers;
    }

    public float X { get; }

    public float Y { get; }

    /// <summary>Wheel steps; 1 is one notch up, negative is down.</summary>
    public float Delta { get; }

    public UiModifiers Modifiers { get; }

    public Vector2 Position => new(X, Y);
}

public readonly struct UiKeyEvent
{
    internal UiKeyEvent(UiKey key, int virtualKey, bool isDown, bool isRepeat, UiModifiers modifiers)
    {
        Key = key;
        VirtualKey = virtualKey;
        IsDown = isDown;
        IsRepeat = isRepeat;
        Modifiers = modifiers;
    }

    public UiKey Key { get; }

    public int VirtualKey { get; }

    public bool IsDown { get; }

    public bool IsRepeat { get; }

    public UiModifiers Modifiers { get; }
}

public sealed class UiInput
{
    private int _buttons;

    public float MouseX { get; private set; }

    public float MouseY { get; private set; }

    public Vector2 MousePosition => new(MouseX, MouseY);

    public UiModifiers Modifiers { get; private set; }

    /// <summary>Delta of the most recent wheel event.</summary>
    public float WheelDelta { get; private set; }

    public bool IsDown(UiMouseButton button) =>
        button != UiMouseButton.None && (_buttons & (1 << (int)button)) != 0;

    internal void SetPointer(float x, float y)
    {
        MouseX = x;
        MouseY = y;
    }

    internal void SetButton(UiMouseButton button, bool down)
    {
        if (button == UiMouseButton.None)
            return;

        int mask = 1 << (int)button;
        _buttons = down ? _buttons | mask : _buttons & ~mask;
    }

    internal void SetModifiers(UiModifiers modifiers) => Modifiers = modifiers;

    internal void SetWheel(float delta) => WheelDelta = delta;
}
