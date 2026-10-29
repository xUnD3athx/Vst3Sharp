using System.Runtime.InteropServices;

namespace Vst3.Plugin;

/// <summary>
/// Base for a native COM-style object presented through one or more vtable views.
/// Layout per view: [vtable, GCHandle, base pointer].
/// </summary>
internal abstract unsafe class ComObject
{
    private const int HeaderSlots = 3;

    private int _refCount = 1;
    private GCHandle _handle;
    private void* _base;

    protected void Initialize(nint[] vtables)
    {
        int count = vtables.Length;
        nint* block = (nint*)NativeMemory.Alloc((nuint)count, (nuint)(HeaderSlots * sizeof(nint)));
        _handle = GCHandle.Alloc(this);

        for (int i = 0; i < count; i++)
        {
            nint* header = block + i * HeaderSlots;
            header[0] = (nint)vtables[i];
            header[1] = GCHandle.ToIntPtr(_handle);
            header[2] = (nint)block;
        }

        _base = block;
    }

    protected void* View(int index) => (void*)((nint)_base + index * HeaderSlots * sizeof(nint));

    protected static T Self<T>(void* view) where T : ComObject
    {
        nint basePtr = ((nint*)view)[2];
        var handle = GCHandle.FromIntPtr(((nint*)basePtr)[1]);
        return (T)handle.Target!;
    }

    internal uint AddRefCore() => (uint)Interlocked.Increment(ref _refCount);

    internal uint ReleaseCore()
    {
        uint count = (uint)Interlocked.Decrement(ref _refCount);
        if (count == 0)
        {
            _handle.Free();
            NativeMemory.Free(_base);
        }

        return count;
    }

    public abstract bool TryGetView(Guid iid, out void* view);
}
