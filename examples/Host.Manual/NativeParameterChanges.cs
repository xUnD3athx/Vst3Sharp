using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vst3.Interop;

namespace Host.Manual;

internal unsafe sealed class NativeParameterChanges
{
    private const int VtableSlots = 6;

    private static nint* s_vtable;

    private readonly void* _queue;
    private int _refCount = 1;
    private GCHandle _self;
    private void* _header;

    static NativeParameterChanges()
    {
        s_vtable = (nint*)NativeMemory.Alloc(VtableSlots, (nuint)sizeof(nint));
        s_vtable[0] = (nint)(delegate* unmanaged[Stdcall]<void*, Guid*, void**, int>)&QueryInterface;
        s_vtable[1] = (nint)(delegate* unmanaged[Stdcall]<void*, uint>)&AddRef;
        s_vtable[2] = (nint)(delegate* unmanaged[Stdcall]<void*, uint>)&Release;
        s_vtable[3] = (nint)(delegate* unmanaged[Stdcall]<void*, int>)&GetParameterCount;
        s_vtable[4] = (nint)(delegate* unmanaged[Stdcall]<void*, int, void*>)&GetParameterData;
        s_vtable[5] = (nint)(delegate* unmanaged[Stdcall]<void*, uint*, int*, void*>)&AddParameterData;
    }

    private NativeParameterChanges(void* queue)
    {
        _queue = queue;
    }

    public static NativeParameterChanges Create(void* queue)
    {
        var changes = new NativeParameterChanges(queue);
        nint* header = (nint*)NativeMemory.Alloc(2, (nuint)sizeof(nint));
        header[0] = (nint)s_vtable;
        header[1] = GCHandle.ToIntPtr(changes._self = GCHandle.Alloc(changes));
        changes._header = header;
        return changes;
    }

    public void* Pointer => _header;

    private static NativeParameterChanges From(void* self) =>
        (NativeParameterChanges)GCHandle.FromIntPtr(((nint*)self)[1]).Target!;

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

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int QueryInterface(void* self, Guid* iid, void** obj)
    {
        if (iid == null || obj == null)
            return Result.InvalidArgument;

        *obj = null;
        if (*iid == IIds.FUnknown || *iid == IIds.IParameterChanges)
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
    private static int GetParameterCount(void* self) => 1;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static void* GetParameterData(void* self, int index) =>
        index == 0 ? From(self)._queue : null;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static void* AddParameterData(void* self, uint* id, int* index) => null;
}
