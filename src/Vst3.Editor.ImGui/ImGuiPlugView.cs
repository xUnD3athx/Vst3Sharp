using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ImGuiNET;
using Vst3.Interop;
using Vst3.Plugin;


namespace Vst3.Editor;

/// <summary>Native <c>IPlugView</c> implementation: child HWND + ImGui + Skia software renderer.</summary>
internal unsafe sealed class ImGuiPlugView : ComObject
{
    private const int VtableSlots = 15;
    private const int TimerId = 1;

    private static nint* s_vtable;
    private static bool s_classRegistered;
    private static nint s_className;
    private static nint s_windowTitle;
    private static readonly object s_lock = new();

    private readonly PluginViewContext _context;
    private readonly EditorView _editorView;
    private int _width;
    private int _height;
    private nint _hwnd;
    private nint _frame;
    private GCHandle _windowHandle;
    private nint _imguiContext;
    private SkiaRenderer? _renderer;
    private long _lastTick;
    private string? _snapshotPath;
    private bool _snapshotTaken;

    private UiInput Input => _editorView.Input;

    static ImGuiPlugView()
    {
        NativeLibraryBootstrap.Ensure();

        s_vtable = BuildVtable(new nint[]
        {
            (nint)(delegate* unmanaged[Stdcall]<void*, Guid*, void**, int>)&QueryInterface,
            (nint)(delegate* unmanaged[Stdcall]<void*, uint>)&AddRef,
            (nint)(delegate* unmanaged[Stdcall]<void*, uint>)&Release,
            (nint)(delegate* unmanaged[Stdcall]<void*, byte*, int>)&IsPlatformTypeSupported,
            (nint)(delegate* unmanaged[Stdcall]<void*, void*, byte*, int>)&Attached,
            (nint)(delegate* unmanaged[Stdcall]<void*, int>)&Removed,
            (nint)(delegate* unmanaged[Stdcall]<void*, float, int>)&OnWheel,
            (nint)(delegate* unmanaged[Stdcall]<void*, char, short, short, int>)&OnKeyDown,
            (nint)(delegate* unmanaged[Stdcall]<void*, char, short, short, int>)&OnKeyUp,
            (nint)(delegate* unmanaged[Stdcall]<void*, ViewRect*, int>)&GetSize,
            (nint)(delegate* unmanaged[Stdcall]<void*, ViewRect*, int>)&OnSize,
            (nint)(delegate* unmanaged[Stdcall]<void*, byte, int>)&OnFocus,
            (nint)(delegate* unmanaged[Stdcall]<void*, void*, int>)&SetFrame,
            (nint)(delegate* unmanaged[Stdcall]<void*, int>)&CanResize,
            (nint)(delegate* unmanaged[Stdcall]<void*, ViewRect*, int>)&CheckSizeConstraint,
        });
    }

    private ImGuiPlugView(PluginViewContext context, EditorView editorView)
    {
        _context = context;
        _editorView = editorView;
        _width = context.Width;
        _height = context.Height;
        Initialize(new nint[] { (nint)s_vtable });
    }

    public static nint Create(PluginViewContext context, EditorView editorView) =>
        (nint)new ImGuiPlugView(context, editorView).View(0);

    private static nint* BuildVtable(nint[] entries)
    {
        nint* vtable = (nint*)NativeMemory.Alloc((nuint)entries.Length, (nuint)sizeof(nint));
        for (int i = 0; i < entries.Length; i++)
            vtable[i] = entries[i];

        return vtable;
    }

    public override bool TryGetView(Guid iid, out void* view)
    {
        if (iid == IIds.FUnknown || iid == IIds.IPlugView)
        {
            view = View(0);
            return true;
        }

        view = null;
        return false;
    }

    // ----- FUnknown -----

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int QueryInterface(void* self, Guid* iid, void** obj)
    {
        if (iid == null || obj == null)
            return Result.InvalidArgument;

        *obj = null;
        var instance = Self<ImGuiPlugView>(self);
        if (!instance.TryGetView(*iid, out void* view))
            return Result.NoInterface;

        instance.AddRefCore();
        *obj = view;
        return Result.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static uint AddRef(void* self) => Self<ImGuiPlugView>(self).AddRefCore();

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static uint Release(void* self) => Self<ImGuiPlugView>(self).ReleaseCore();

    // ----- IPlugView -----

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int IsPlatformTypeSupported(void* self, byte* type) => IsHwnd(type) ? Result.Ok : Result.False;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int Attached(void* self, void* parent, byte* type)
    {
        var view = Self<ImGuiPlugView>(self);
        if (parent == null || !IsHwnd(type))
            return Result.InvalidArgument;

        if (view._hwnd != 0)
            return Result.Ok;

        EnsureWindowClass();

        view._hwnd = Win32.CreateWindowExW(
            0, s_className, s_windowTitle,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_CLIPSIBLINGS | Win32.WS_CLIPCHILDREN,
            0, 0, view._width, view._height,
            (nint)parent, 0, Win32.GetModuleHandleW(null), null);

        if (view._hwnd == 0)
            return Result.False;

        view._windowHandle = GCHandle.Alloc(view);
        Win32.SetWindowLongPtrW(view._hwnd, Win32.GWLP_USERDATA, GCHandle.ToIntPtr(view._windowHandle));

        view._imguiContext = ImGui.CreateContext();
        ImGui.SetCurrentContext(view._imguiContext);

        var io = ImGui.GetIO();
        io.NativePtr->IniFilename = null;
        io.DisplaySize = new Vector2(view._width, view._height);
        view._renderer = new SkiaRenderer(view._width, view._height, io.Fonts);
        view._editorView.Attach(view._context, view._width, view._height);
        view._lastTick = Stopwatch.GetTimestamp();

        Win32.SetTimer(view._hwnd, TimerId, 16, 0);
        view.Render();
        return Result.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int Removed(void* self)
    {
        var view = Self<ImGuiPlugView>(self);
        view._editorView.Detach();

        if (view._hwnd != 0)
        {
            Win32.KillTimer(view._hwnd, TimerId);
            Win32.DestroyWindow(view._hwnd);
            view._hwnd = 0;
        }

        view._renderer?.Dispose();
        view._renderer = null;

        if (view._imguiContext != 0)
        {
            ImGui.DestroyContext(view._imguiContext);
            view._imguiContext = 0;
        }

        if (view._windowHandle.IsAllocated)
            view._windowHandle.Free();

        return Result.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int OnWheel(void* self, float distance)
    {
        var view = Self<ImGuiPlugView>(self);
        if (view._imguiContext != 0)
        {
            ImGui.SetCurrentContext(view._imguiContext);
            ImGui.GetIO().AddMouseWheelEvent(0.0f, distance);
        }

        return Result.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int OnKeyDown(void* self, char key, short keyCode, short modifiers) => Result.False;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int OnKeyUp(void* self, char key, short keyCode, short modifiers) => Result.False;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int GetSize(void* self, ViewRect* size)
    {
        if (size == null)
            return Result.InvalidArgument;

        var view = Self<ImGuiPlugView>(self);
        size->Left = 0;
        size->Top = 0;
        size->Right = view._width;
        size->Bottom = view._height;
        return Result.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int OnSize(void* self, ViewRect* newSize)
    {
        if (newSize == null)
            return Result.InvalidArgument;

        var view = Self<ImGuiPlugView>(self);
        int width = newSize->Right - newSize->Left;
        int height = newSize->Bottom - newSize->Top;
        if (width <= 0 || height <= 0)
            return Result.InvalidArgument;

        view._width = width;
        view._height = height;
        view._renderer?.Resize(width, height);
        view._editorView.Resize(width, height);
        return Result.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int OnFocus(void* self, byte state)
    {
        var view = Self<ImGuiPlugView>(self);
        if (view._imguiContext != 0)
        {
            ImGui.SetCurrentContext(view._imguiContext);
            ImGui.GetIO().AddFocusEvent(state != 0);
        }

        return Result.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int SetFrame(void* self, void* frame)
    {
        Self<ImGuiPlugView>(self)._frame = (nint)frame;
        return Result.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int CanResize(void* self) => Result.False;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int CheckSizeConstraint(void* self, ViewRect* rect) => Result.Ok;

    // ----- window / rendering -----

    private static void EnsureWindowClass()
    {
        lock (s_lock)
        {
            if (s_classRegistered)
                return;

            s_className = Marshal.StringToHGlobalUni("Vst3SharpImGuiView");
            s_windowTitle = Marshal.StringToHGlobalUni("Vst3Sharp");

            var windowClass = new Win32.WNDCLASSEXW
            {
                Size = (uint)sizeof(Win32.WNDCLASSEXW),
                WndProc = (nint)(delegate* unmanaged[Stdcall]<nint, uint, nint, nint, nint>)&WndProc,
                Instance = Win32.GetModuleHandleW(null),
                ClassName = s_className,
            };

            Win32.RegisterClassExW(&windowClass);
            s_classRegistered = true;
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        nint userData = Win32.GetWindowLongPtrW(hwnd, Win32.GWLP_USERDATA);
        if (userData != 0)
        {
            var handle = GCHandle.FromIntPtr(userData);
            if (handle.Target is ImGuiPlugView view && view.HandleMessage(hwnd, msg, wParam, lParam))
                return 0;
        }

        return Win32.DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    private bool HandleMessage(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        if (_imguiContext == 0)
            return false;

        ImGui.SetCurrentContext(_imguiContext);
        var io = ImGui.GetIO();

        switch (msg)
        {
            case Win32.WM_TIMER:
                Render();
                return true;

            case Win32.WM_PAINT:
            {
                Win32.PAINTSTRUCT paint;
                Win32.BeginPaint(hwnd, &paint);
                Render();
                Win32.EndPaint(hwnd, &paint);
                return true;
            }

            case Win32.WM_ERASEBKGND:
                return true;

            case Win32.WM_SIZE:
            {
                int width = (int)(lParam & 0xFFFF);
                int height = (int)((lParam >> 16) & 0xFFFF);
                if (width > 0 && height > 0)
                {
                    _width = width;
                    _height = height;
                    _renderer?.Resize(width, height);
                    _editorView.Resize(width, height);
                }

                return true;
            }

            case Win32.WM_MOUSEMOVE:
                io.AddMousePosEvent(MouseX(lParam), MouseY(lParam));
                DispatchPointer(UiPointerAction.Move, UiMouseButton.None, lParam);
                return true;

            case Win32.WM_LBUTTONDOWN:
                io.AddMousePosEvent(MouseX(lParam), MouseY(lParam));
                io.AddMouseButtonEvent(0, true);
                Win32.SetCapture(hwnd);
                DispatchButton(UiMouseButton.Left, true, lParam);
                return true;

            case Win32.WM_LBUTTONUP:
                io.AddMouseButtonEvent(0, false);
                Win32.ReleaseCapture();
                DispatchButton(UiMouseButton.Left, false, lParam);
                return true;

            case Win32.WM_RBUTTONDOWN:
                io.AddMousePosEvent(MouseX(lParam), MouseY(lParam));
                io.AddMouseButtonEvent(1, true);
                Win32.SetCapture(hwnd);
                DispatchButton(UiMouseButton.Right, true, lParam);
                return true;

            case Win32.WM_RBUTTONUP:
                io.AddMouseButtonEvent(1, false);
                Win32.ReleaseCapture();
                DispatchButton(UiMouseButton.Right, false, lParam);
                return true;

            case Win32.WM_MBUTTONDOWN:
                io.AddMousePosEvent(MouseX(lParam), MouseY(lParam));
                io.AddMouseButtonEvent(2, true);
                Win32.SetCapture(hwnd);
                DispatchButton(UiMouseButton.Middle, true, lParam);
                return true;

            case Win32.WM_MBUTTONUP:
                io.AddMouseButtonEvent(2, false);
                Win32.ReleaseCapture();
                DispatchButton(UiMouseButton.Middle, false, lParam);
                return true;

            case Win32.WM_MOUSEWHEEL:
            {
                short delta = (short)((wParam >> 16) & 0xFFFF);
                float steps = delta / 120.0f;
                io.AddMouseWheelEvent(0.0f, steps);
                Input.SetModifiers(CurrentModifiers());
                Input.SetWheel(steps);
                var wheel = new UiWheelEvent(Input.MouseX, Input.MouseY, steps, Input.Modifiers);
                _editorView.OnWheel(in wheel);
                return true;
            }

            case Win32.WM_KEYDOWN:
                UpdateModifiers(io);
                UpdateInputModifiers();
                DispatchKey(wParam, true, (lParam & (1 << 30)) != 0);
                io.AddKeyEvent(MapImGuiKey(wParam), true);
                return true;

            case Win32.WM_KEYUP:
                UpdateModifiers(io);
                UpdateInputModifiers();
                DispatchKey(wParam, false, false);
                io.AddKeyEvent(MapImGuiKey(wParam), false);
                return true;

            case Win32.WM_CHAR:
                io.AddInputCharacter((uint)wParam);
                DispatchText((char)wParam);
                return true;

            case Win32.WM_SETFOCUS:
                io.AddFocusEvent(true);
                _editorView.OnFocusChanged(true);
                return true;

            case Win32.WM_KILLFOCUS:
                io.AddFocusEvent(false);
                _editorView.OnFocusChanged(false);
                return true;
        }

        return false;
    }

    private static void UpdateModifiers(ImGuiIOPtr io)
    {
        io.AddKeyEvent(ImGuiKey.ModShift, (Win32.GetKeyState(Win32.VK_SHIFT) & 0x8000) != 0);
        io.AddKeyEvent(ImGuiKey.ModCtrl, (Win32.GetKeyState(Win32.VK_CONTROL) & 0x8000) != 0);
        io.AddKeyEvent(ImGuiKey.ModAlt, (Win32.GetKeyState(Win32.VK_MENU) & 0x8000) != 0);
    }

    private void UpdateInputModifiers() => Input.SetModifiers(CurrentModifiers());

    private void DispatchPointer(UiPointerAction action, UiMouseButton button, nint lParam)
    {
        float x = MouseX(lParam);
        float y = MouseY(lParam);
        Input.SetPointer(x, y);
        UpdateInputModifiers();

        var e = new UiPointerEvent(action, button, x, y, Input.Modifiers);
        switch (action)
        {
            case UiPointerAction.Down:
                _editorView.OnPointerDown(in e);
                break;
            case UiPointerAction.Up:
                _editorView.OnPointerUp(in e);
                break;
            default:
                _editorView.OnPointerMove(in e);
                break;
        }
    }

    private void DispatchButton(UiMouseButton button, bool down, nint lParam)
    {
        Input.SetButton(button, down);
        DispatchPointer(down ? UiPointerAction.Down : UiPointerAction.Up, button, lParam);
    }

    private void DispatchKey(nint virtualKey, bool down, bool repeat)
    {
        int key = (int)virtualKey;
        var e = new UiKeyEvent(MapUiKey(key), key, down, repeat, Input.Modifiers);
        _editorView.OnKey(in e);
    }

    private void DispatchText(char c)
    {
        if (c >= 32 && c != 127)
            _editorView.OnText(c);
    }

    private static UiModifiers CurrentModifiers()
    {
        UiModifiers modifiers = UiModifiers.None;
        if ((Win32.GetKeyState(Win32.VK_SHIFT) & 0x8000) != 0)
            modifiers |= UiModifiers.Shift;
        if ((Win32.GetKeyState(Win32.VK_CONTROL) & 0x8000) != 0)
            modifiers |= UiModifiers.Control;
        if ((Win32.GetKeyState(Win32.VK_MENU) & 0x8000) != 0)
            modifiers |= UiModifiers.Alt;
        if ((Win32.GetKeyState(Win32.VK_LWIN) & 0x8000) != 0 || (Win32.GetKeyState(Win32.VK_RWIN) & 0x8000) != 0)
            modifiers |= UiModifiers.Win;

        return modifiers;
    }

    private static UiKey MapUiKey(int virtualKey)
    {
        if (virtualKey is >= 0x30 and <= 0x39)
            return (UiKey)((int)UiKey.D0 + (virtualKey - 0x30));
        if (virtualKey is >= 0x41 and <= 0x5A)
            return (UiKey)((int)UiKey.A + (virtualKey - 0x41));

        return virtualKey switch
        {
            0x20 => UiKey.Space,
            0x1B => UiKey.Escape,
            0x0D => UiKey.Enter,
            0x09 => UiKey.Tab,
            0x08 => UiKey.Backspace,
            0x2E => UiKey.Delete,
            0x25 => UiKey.Left,
            0x27 => UiKey.Right,
            0x26 => UiKey.Up,
            0x28 => UiKey.Down,
            0x24 => UiKey.Home,
            0x23 => UiKey.End,
            0x21 => UiKey.PageUp,
            0x22 => UiKey.PageDown,
            _ => UiKey.Unknown
        };
    }

    private static ImGuiKey MapImGuiKey(nint virtualKey)
    {
        int key = (int)virtualKey;
        if (key is >= 0x30 and <= 0x39)
            return ImGuiKey._0 + (key - 0x30);
        if (key is >= 0x41 and <= 0x5A)
            return ImGuiKey.A + (key - 0x41);

        return key switch
        {
            0x1B => ImGuiKey.Escape,
            0x0D => ImGuiKey.Enter,
            0x08 => ImGuiKey.Backspace,
            0x09 => ImGuiKey.Tab,
            0x2E => ImGuiKey.Delete,
            0x25 => ImGuiKey.LeftArrow,
            0x27 => ImGuiKey.RightArrow,
            0x26 => ImGuiKey.UpArrow,
            0x28 => ImGuiKey.DownArrow,
            _ => ImGuiKey.None
        };
    }

    private static float MouseX(nint lParam) => (short)(lParam & 0xFFFF);

    private static float MouseY(nint lParam) => (short)((lParam >> 16) & 0xFFFF);

    private static bool IsHwnd(byte* type)
    {
        if (type == null)
            return false;

        return type[0] == (byte)'H' && type[1] == (byte)'W' && type[2] == (byte)'N' &&
               type[3] == (byte)'D' && type[4] == 0;
    }

    private void Render()
    {
        if (_renderer == null || _imguiContext == 0)
            return;

        ImGui.SetCurrentContext(_imguiContext);
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2(_width, _height);

        long now = Stopwatch.GetTimestamp();
        float delta = (float)((now - _lastTick) / (double)Stopwatch.Frequency);
        _lastTick = now;
        io.DeltaTime = Math.Clamp(delta, 1.0f / 1000.0f, 0.25f);

        ImGui.NewFrame();
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.SetNextWindowPos(Vector2.Zero);
        ImGui.SetNextWindowSize(io.DisplaySize);
        ImGui.Begin("##root",
            ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoScrollbar |
            ImGuiWindowFlags.NoScrollWithMouse);

        // Texture-based AA lines sample blank atlas texels in this custom renderer, so keep them vertex-colored.
        ImDrawListPtr drawList = ImGui.GetWindowDrawList();
        drawList.Flags &= ~ImDrawListFlags.AntiAliasedLinesUseTex;
        _editorView.OnRender(new UiCanvas(drawList, _width, _height));
        ImGui.End();
        ImGui.PopStyleVar();
        ImGui.Render();
        _renderer.Render(ImGui.GetDrawData(), _hwnd);

        if (!_snapshotTaken)
        {
            _snapshotPath ??= Environment.GetEnvironmentVariable("VST3SHARP_EDITOR_SNAPSHOT");
            if (!string.IsNullOrEmpty(_snapshotPath))
            {
                _renderer.SaveSnapshot(_snapshotPath);
                _snapshotTaken = true;
            }
        }
    }

}
