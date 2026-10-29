using System.Runtime.InteropServices;
using Vst3.Interop;

namespace Host.Manual;

/// <summary>Attaches the editor to a hidden parent window, pumps messages, and saves a PNG snapshot.</summary>
internal static unsafe class EditorTest
{
    private const uint WS_POPUP = 0x80000000;
    private const uint PM_REMOVE = 0x0001;

    public static int Run(string path)
    {
        path = Path.GetFullPath(path);
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"not found: {path}");
            return 2;
        }

        string snapshot = Path.Combine(Path.GetTempPath(), "vst3sharp-editor.png");
        if (File.Exists(snapshot))
            File.Delete(snapshot);
        Environment.SetEnvironmentVariable("VST3SHARP_EDITOR_SNAPSHOT", snapshot);

        nint library = NativeLibrary.Load(path);
        try
        {
            if (!NativeLibrary.TryGetExport(library, "GetPluginFactory", out nint getFactoryPtr))
            {
                Console.Error.WriteLine("GetPluginFactory is not exported");
                return 3;
            }

            void* factory = ((delegate* unmanaged[Stdcall]<void*>)getFactoryPtr)();
            nint* factoryVt = Vtable.Of(factory);
            var releaseFactory = (delegate* unmanaged[Stdcall]<void*, uint>)factoryVt[2];
            var getClassInfo = (delegate* unmanaged[Stdcall]<void*, int, PClassInfo*, int>)factoryVt[5];
            var createInstance = (delegate* unmanaged[Stdcall]<void*, Guid*, Guid*, void**, int>)factoryVt[6];

            PClassInfo classInfo = default;
            getClassInfo(factory, 0, &classInfo);

            Guid classId = classInfo.Cid;
            Guid componentIid = IIds.IComponent;
            void* component = null;
            createInstance(factory, &classId, &componentIid, &component);
            if (component == null)
            {
                Console.Error.WriteLine("createInstance failed");
                releaseFactory(factory);
                return 4;
            }

            nint* componentVt = Vtable.Of(component);
            var queryComponent = (delegate* unmanaged[Stdcall]<void*, Guid*, void**, int>)componentVt[0];
            var releaseComponent = (delegate* unmanaged[Stdcall]<void*, uint>)componentVt[2];
            var initialize = (delegate* unmanaged[Stdcall]<void*, void*, int>)componentVt[3];
            var terminate = (delegate* unmanaged[Stdcall]<void*, int>)componentVt[4];
            initialize(component, null);

            Guid controllerIid = IIds.IEditController;
            void* controller = null;
            queryComponent(component, &controllerIid, &controller);
            if (controller == null)
            {
                Console.Error.WriteLine("no IEditController");
                terminate(component);
                releaseComponent(component);
                releaseFactory(factory);
                return 5;
            }

            nint* controllerVt = Vtable.Of(controller);
            var releaseController = (delegate* unmanaged[Stdcall]<void*, uint>)controllerVt[2];
            var createView = (delegate* unmanaged[Stdcall]<void*, byte*, void*>)controllerVt[17];

            nint namePtr = Marshal.StringToHGlobalAnsi("editor");
            void* view = createView(controller, (byte*)namePtr);
            if (view == null)
            {
                Console.Error.WriteLine("createView returned null");
                releaseController(controller);
                terminate(component);
                releaseComponent(component);
                releaseFactory(factory);
                return 6;
            }

            nint* viewVt = Vtable.Of(view);
            var releaseView = (delegate* unmanaged[Stdcall]<void*, uint>)viewVt[2];
            var isSupported = (delegate* unmanaged[Stdcall]<void*, byte*, int>)viewVt[3];
            var attached = (delegate* unmanaged[Stdcall]<void*, void*, byte*, int>)viewVt[4];
            var removed = (delegate* unmanaged[Stdcall]<void*, int>)viewVt[5];
            var getSize = (delegate* unmanaged[Stdcall]<void*, ViewRect*, int>)viewVt[9];

            ViewRect size = default;
            getSize(view, &size);
            int width = size.Right - size.Left;
            int height = size.Bottom - size.Top;
            Console.WriteLine($"view size: {width}x{height}");

            nint parent = CreateWindowExW(0, (nint)Marshal.StringToHGlobalUni("STATIC"), 0, WS_POPUP,
                0, 0, width, height, 0, 0, GetModuleHandleW(null), null);
            if (parent == 0)
            {
                Console.Error.WriteLine("failed to create parent window");
                releaseView(view);
                releaseController(controller);
                terminate(component);
                releaseComponent(component);
                releaseFactory(factory);
                return 7;
            }

            nint hwndType = Marshal.StringToHGlobalAnsi("HWND");
            int supported = isSupported(view, (byte*)hwndType);
            int attachHr = attached(view, (void*)parent, (byte*)hwndType);
            Console.WriteLine($"isPlatformTypeSupported(HWND)=0x{supported:X8} attached=0x{attachHr:X8}");

            var msg = new MSG();
            for (int i = 0; i < 60; i++)
            {
                while (PeekMessageW(&msg, 0, 0, 0, PM_REMOVE) != 0)
                {
                    TranslateMessage(&msg);
                    DispatchMessageW(&msg);
                }

                Thread.Sleep(16);
            }

            removed(view);
            DestroyWindow(parent);
            releaseView(view);
            releaseController(controller);
            terminate(component);
            releaseComponent(component);
            releaseFactory(factory);

            bool pass = supported == Result.Ok && attachHr == Result.Ok && File.Exists(snapshot);
            Console.WriteLine($"editor snapshot: {snapshot} exists={File.Exists(snapshot)} -> {(pass ? "PASS" : "FAIL")}");
            return pass ? 0 : 8;
        }
        finally
        {
            NativeLibrary.Free(library);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public nint Hwnd;
        public uint Message;
        public nint WParam;
        public nint LParam;
        public uint Time;
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint CreateWindowExW(
        int exStyle, nint className, nint windowName, uint style,
        int x, int y, int width, int height,
        nint parent, nint menu, nint instance, void* param);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandleW(string? moduleName);

    [DllImport("user32.dll")]
    private static extern int DestroyWindow(nint hwnd);

    [DllImport("user32.dll")]
    private static extern int PeekMessageW(MSG* msg, nint hwnd, uint min, uint max, uint remove);

    [DllImport("user32.dll")]
    private static extern int TranslateMessage(MSG* msg);

    [DllImport("user32.dll")]
    private static extern nint DispatchMessageW(MSG* msg);
}
