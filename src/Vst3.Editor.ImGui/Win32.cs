using System.Runtime.InteropServices;

namespace Vst3.Editor;

internal static unsafe class Win32
{
    public const int WS_CHILD = 0x40000000;
    public const int WS_VISIBLE = 0x10000000;
    public const int WS_CLIPSIBLINGS = 0x04000000;
    public const int WS_CLIPCHILDREN = 0x02000000;

    public const uint WM_DESTROY = 0x0002;
    public const uint WM_SIZE = 0x0005;
    public const uint WM_SETFOCUS = 0x0007;
    public const uint WM_KILLFOCUS = 0x0008;
    public const uint WM_PAINT = 0x000F;
    public const uint WM_ERASEBKGND = 0x0014;
    public const uint WM_SETCURSOR = 0x0020;
    public const uint WM_KEYDOWN = 0x0100;
    public const uint WM_KEYUP = 0x0101;
    public const uint WM_CHAR = 0x0102;
    public const uint WM_TIMER = 0x0113;
    public const uint WM_MOUSEMOVE = 0x0200;
    public const uint WM_LBUTTONDOWN = 0x0201;
    public const uint WM_LBUTTONUP = 0x0202;
    public const uint WM_RBUTTONDOWN = 0x0204;
    public const uint WM_RBUTTONUP = 0x0205;
    public const uint WM_MBUTTONDOWN = 0x0207;
    public const uint WM_MBUTTONUP = 0x0208;
    public const uint WM_MOUSEWHEEL = 0x020A;

    public const int GWLP_USERDATA = -21;
    public const int VK_SHIFT = 0x10;
    public const int VK_CONTROL = 0x11;
    public const int VK_MENU = 0x12;
    public const int VK_LWIN = 0x5B;
    public const int VK_RWIN = 0x5C;

    public const uint SRCCOPY = 0x00CC0020;
    public const uint DIB_RGB_COLORS = 0;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PAINTSTRUCT
    {
        public nint Hdc;
        public int Erase;
        public RECT Paint;
        public int Restore;
        public int IncUpdate;
        public fixed byte Reserved[32];
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFOHEADER
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ClrUsed;
        public uint ClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFO
    {
        public BITMAPINFOHEADER Header;
        public uint Colors;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WNDCLASSEXW
    {
        public uint Size;
        public uint Style;
        public nint WndProc;
        public int ClsExtra;
        public int WndExtra;
        public nint Instance;
        public nint Icon;
        public nint Cursor;
        public nint Background;
        public nint MenuName;
        public nint ClassName;
        public nint IconSmall;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern nint GetModuleHandleW(string? moduleName);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern ushort RegisterClassExW(WNDCLASSEXW* windowClass);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern nint CreateWindowExW(
        int exStyle, nint className, nint windowName, int style,
        int x, int y, int width, int height,
        nint parent, nint menu, nint instance, void* param);

    [DllImport("user32.dll")]
    public static extern nint DefWindowProcW(nint hwnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    public static extern nint SetWindowLongPtrW(nint hwnd, int index, nint value);

    [DllImport("user32.dll")]
    public static extern nint GetWindowLongPtrW(nint hwnd, int index);

    [DllImport("user32.dll")]
    public static extern int DestroyWindow(nint hwnd);

    [DllImport("user32.dll")]
    public static extern int GetClientRect(nint hwnd, RECT* rect);

    [DllImport("user32.dll")]
    public static extern nint SetTimer(nint hwnd, nint id, uint interval, nint callback);

    [DllImport("user32.dll")]
    public static extern int KillTimer(nint hwnd, nint id);

    [DllImport("user32.dll")]
    public static extern nint SetCapture(nint hwnd);

    [DllImport("user32.dll")]
    public static extern int ReleaseCapture();

    [DllImport("user32.dll")]
    public static extern nint GetDC(nint hwnd);

    [DllImport("user32.dll")]
    public static extern int ReleaseDC(nint hwnd, nint dc);

    [DllImport("user32.dll")]
    public static extern nint BeginPaint(nint hwnd, PAINTSTRUCT* paint);

    [DllImport("user32.dll")]
    public static extern int EndPaint(nint hwnd, PAINTSTRUCT* paint);

    [DllImport("user32.dll")]
    public static extern short GetKeyState(int virtualKey);

    [DllImport("user32.dll")]
    public static extern int InvalidateRect(nint hwnd, RECT* rect, int erase);

    [DllImport("gdi32.dll")]
    public static extern int StretchDIBits(
        nint hdc, int xDest, int yDest, int widthDest, int heightDest,
        int xSrc, int ySrc, int widthSrc, int heightSrc,
        void* bits, BITMAPINFO* info, uint usage, uint rop);
}
