using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vst3.Interop;

namespace Host.Manual;

/// <summary>Minimal native IBStream backed by a fixed-size memory buffer.</summary>
internal unsafe sealed class NativeMemoryStream
{
    private const int VtableSlots = 7;
    private const int Capacity = 1024;

    private static nint* s_vtable;

    private int _refCount = 1;
    private int _position;
    private int _length;
    private byte* _buffer;
    private GCHandle _self;
    private void* _header;

    static NativeMemoryStream()
    {
        s_vtable = (nint*)NativeMemory.Alloc(VtableSlots, (nuint)sizeof(nint));
        s_vtable[0] = (nint)(delegate* unmanaged[Stdcall]<void*, Guid*, void**, int>)&QueryInterface;
        s_vtable[1] = (nint)(delegate* unmanaged[Stdcall]<void*, uint>)&AddRef;
        s_vtable[2] = (nint)(delegate* unmanaged[Stdcall]<void*, uint>)&Release;
        s_vtable[3] = (nint)(delegate* unmanaged[Stdcall]<void*, void*, int, int*, int>)&Read;
        s_vtable[4] = (nint)(delegate* unmanaged[Stdcall]<void*, void*, int, int*, int>)&Write;
        s_vtable[5] = (nint)(delegate* unmanaged[Stdcall]<void*, long, int, long*, int>)&Seek;
        s_vtable[6] = (nint)(delegate* unmanaged[Stdcall]<void*, long*, int>)&Tell;
    }

    public static NativeMemoryStream Create()
    {
        var stream = new NativeMemoryStream();
        stream._buffer = (byte*)NativeMemory.Alloc(Capacity);

        nint* header = (nint*)NativeMemory.Alloc(2, (nuint)sizeof(nint));
        header[0] = (nint)s_vtable;
        header[1] = GCHandle.ToIntPtr(stream._self = GCHandle.Alloc(stream));
        stream._header = header;
        return stream;
    }

    public void* Pointer => _header;

    public void Rewind() => _position = 0;

    private static NativeMemoryStream From(void* self) =>
        (NativeMemoryStream)GCHandle.FromIntPtr(((nint*)self)[1]).Target!;

    private uint AddRefCore() => (uint)Interlocked.Increment(ref _refCount);

    private uint ReleaseCore()
    {
        uint count = (uint)Interlocked.Decrement(ref _refCount);
        if (count == 0)
        {
            _self.Free();
            NativeMemory.Free(_buffer);
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
        if (*iid == IIds.FUnknown || *iid == IIds.IBStream)
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
    private static int Read(void* self, void* buffer, int numBytes, int* numBytesRead)
    {
        var stream = From(self);
        int available = stream._length - stream._position;
        if (available < 0)
            available = 0;

        int count = Math.Min(numBytes, available);
        if (count > 0)
            Buffer.MemoryCopy(stream._buffer + stream._position, buffer, numBytes, count);

        stream._position += count;
        if (numBytesRead != null)
            *numBytesRead = count;

        return Result.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int Write(void* self, void* buffer, int numBytes, int* numBytesWritten)
    {
        var stream = From(self);
        int space = Capacity - stream._position;
        int count = Math.Min(numBytes, space);

        if (count > 0)
            Buffer.MemoryCopy(buffer, stream._buffer + stream._position, space, count);

        stream._position += count;
        if (stream._position > stream._length)
            stream._length = stream._position;

        if (numBytesWritten != null)
            *numBytesWritten = count;

        return count == numBytes ? Result.Ok : Result.False;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int Seek(void* self, long position, int mode, long* result)
    {
        var stream = From(self);
        long newPosition = mode switch
        {
            0 => position,
            1 => stream._position + position,
            2 => stream._length + position,
            _ => -1
        };

        if (newPosition < 0 || newPosition > Capacity)
            return Result.InvalidArgument;

        stream._position = (int)newPosition;
        if (result != null)
            *result = newPosition;

        return Result.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int Tell(void* self, long* position)
    {
        if (position == null)
            return Result.InvalidArgument;

        *position = From(self)._position;
        return Result.Ok;
    }
}
