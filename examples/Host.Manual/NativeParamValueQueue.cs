using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vst3.Interop;

namespace Host.Manual;

internal unsafe sealed class NativeParamValueQueue
{
    private const int VtableSlots = 7;

    private static nint* s_vtable;

    private readonly uint _id;
    private readonly int _offset;
    private readonly double _value;
    private int _refCount = 1;
    private GCHandle _self;
    private void* _header;

    static NativeParamValueQueue()
    {
        s_vtable = (nint*)NativeMemory.Alloc(VtableSlots, (nuint)sizeof(nint));
        s_vtable[0] = (nint)(delegate* unmanaged[Stdcall]<void*, Guid*, void**, int>)&QueryInterface;
        s_vtable[1] = (nint)(delegate* unmanaged[Stdcall]<void*, uint>)&AddRef;
        s_vtable[2] = (nint)(delegate* unmanaged[Stdcall]<void*, uint>)&Release;
        s_vtable[3] = (nint)(delegate* unmanaged[Stdcall]<void*, uint>)&GetParameterId;
        s_vtable[4] = (nint)(delegate* unmanaged[Stdcall]<void*, int>)&GetPointCount;
        s_vtable[5] = (nint)(delegate* unmanaged[Stdcall]<void*, int, int*, double*, int>)&GetPoint;
        s_vtable[6] = (nint)(delegate* unmanaged[Stdcall]<void*, int, double, int*, int>)&AddPoint;
    }

    private NativeParamValueQueue(uint id, int offset, double value)
    {
        _id = id;
        _offset = offset;
        _value = value;
    }

    public static NativeParamValueQueue Create(uint id, int offset, double value)
    {
        var queue = new NativeParamValueQueue(id, offset, value);
        nint* header = (nint*)NativeMemory.Alloc(2, (nuint)sizeof(nint));
        header[0] = (nint)s_vtable;
        header[1] = GCHandle.ToIntPtr(queue._self = GCHandle.Alloc(queue));
        queue._header = header;
        return queue;
    }

    public void* Pointer => _header;

    private static NativeParamValueQueue From(void* self) =>
        (NativeParamValueQueue)GCHandle.FromIntPtr(((nint*)self)[1]).Target!;

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
        if (*iid == IIds.FUnknown || *iid == IIds.IParamValueQueue)
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
    private static uint GetParameterId(void* self) => From(self)._id;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int GetPointCount(void* self) => 1;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int GetPoint(void* self, int index, int* sampleOffset, double* value)
    {
        if (index != 0 || sampleOffset == null || value == null)
            return Result.InvalidArgument;

        var queue = From(self);
        *sampleOffset = queue._offset;
        *value = queue._value;
        return Result.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int AddPoint(void* self, int sampleOffset, double value, int* index) => Result.NotImplemented;
}
