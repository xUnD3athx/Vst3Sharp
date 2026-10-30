using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vst3.Interop;

namespace Host.Manual;

/// <summary>Minimal native IEventList used to feed MIDI events into a plugin under test.</summary>
internal unsafe sealed class NativeEventList
{
    private const int VtableSlots = 6;

    private static nint* s_vtable;

    private readonly Event[] _events = new Event[64];
    private int _count;
    private int _refCount = 1;
    private GCHandle _self;
    private void* _header;

    static NativeEventList()
    {
        s_vtable = (nint*)NativeMemory.Alloc(VtableSlots, (nuint)sizeof(nint));
        s_vtable[0] = (nint)(delegate* unmanaged[Stdcall]<void*, Guid*, void**, int>)&QueryInterface;
        s_vtable[1] = (nint)(delegate* unmanaged[Stdcall]<void*, uint>)&AddRef;
        s_vtable[2] = (nint)(delegate* unmanaged[Stdcall]<void*, uint>)&Release;
        s_vtable[3] = (nint)(delegate* unmanaged[Stdcall]<void*, int>)&GetEventCount;
        s_vtable[4] = (nint)(delegate* unmanaged[Stdcall]<void*, int, Event*, int>)&GetEvent;
        s_vtable[5] = (nint)(delegate* unmanaged[Stdcall]<void*, Event*, int>)&AddEvent;
    }

    public static NativeEventList Create()
    {
        var list = new NativeEventList();
        nint* header = (nint*)NativeMemory.Alloc(2, (nuint)sizeof(nint));
        header[0] = (nint)s_vtable;
        header[1] = GCHandle.ToIntPtr(list._self = GCHandle.Alloc(list));
        list._header = header;
        return list;
    }

    public void* Pointer => _header;

    public void Clear() => _count = 0;

    public void Add(in Event e)
    {
        if (_count < _events.Length)
            _events[_count++] = e;
    }

    public void Release()
    {
        ReleaseCore();
    }

    private uint AddRefCore() => (uint)Interlocked.Increment(ref _refCount);

    private uint ReleaseCore()
    {
        uint count = (uint)Interlocked.Decrement(ref _refCount);
        if (count == 0)
        {
            _self.Free();
            NativeMemory.Free(_header);
        }

        return count;
    }

    private static NativeEventList From(void* self) =>
        (NativeEventList)GCHandle.FromIntPtr(((nint*)self)[1]).Target!;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int QueryInterface(void* self, Guid* iid, void** obj)
    {
        if (iid == null || obj == null)
            return Result.InvalidArgument;

        *obj = null;
        if (*iid == IIds.FUnknown || *iid == IIds.IEventList)
        {
            From(self).AddRefCore();
            *obj = self;
            return Result.Ok;
        }

        return Result.NoInterface;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static uint AddRef(void* self) => From(self).AddRefCore();

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static uint Release(void* self) => From(self).ReleaseCore();

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int GetEventCount(void* self) => From(self)._count;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int GetEvent(void* self, int index, Event* e)
    {
        var list = From(self);
        if (e == null || index < 0 || index >= list._count)
            return Result.InvalidArgument;

        *e = list._events[index];
        return Result.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int AddEvent(void* self, Event* e) => Result.NotImplemented;
}
