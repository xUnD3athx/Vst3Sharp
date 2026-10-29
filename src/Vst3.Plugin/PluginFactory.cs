using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vst3.Interop;

namespace Vst3.Plugin;

/// <summary>
/// Hand-built <c>IPluginFactory</c>. The native object is a two-slot header:
/// slot 0 is the vtable pointer, slot 1 is the GCHandle token of the managed instance.
/// S0 exposes a single class and does not create instances yet.
/// </summary>
internal unsafe sealed class PluginFactory
{
    private const int VtableSlots = 7;
    private const int HeaderSlots = 2;

    private static nint* s_vtable;
    private static PluginDescriptor? s_descriptor;

    private int _refCount = 1;

    static PluginFactory()
    {
        s_vtable = (nint*)NativeMemory.Alloc(VtableSlots, (nuint)sizeof(nint));
        s_vtable[0] = (nint)(delegate* unmanaged[Stdcall]<void*, Guid*, void**, int>)&QueryInterface;
        s_vtable[1] = (nint)(delegate* unmanaged[Stdcall]<void*, uint>)&AddRef;
        s_vtable[2] = (nint)(delegate* unmanaged[Stdcall]<void*, uint>)&Release;
        s_vtable[3] = (nint)(delegate* unmanaged[Stdcall]<void*, PFactoryInfo*, int>)&GetFactoryInfo;
        s_vtable[4] = (nint)(delegate* unmanaged[Stdcall]<void*, int>)&CountClasses;
        s_vtable[5] = (nint)(delegate* unmanaged[Stdcall]<void*, int, PClassInfo*, int>)&GetClassInfo;
        s_vtable[6] = (nint)(delegate* unmanaged[Stdcall]<void*, Guid*, Guid*, void**, int>)&CreateInstance;
    }

    private PluginFactory(PluginDescriptor descriptor)
    {
        s_descriptor = descriptor;
    }

    public static void* Create(PluginDescriptor descriptor)
    {
        var factory = new PluginFactory(descriptor);
        nint* header = (nint*)NativeMemory.Alloc(HeaderSlots, (nuint)sizeof(nint));
        header[0] = (nint)s_vtable;
        header[1] = GCHandle.ToIntPtr(GCHandle.Alloc(factory));
        return header;
    }

    private static PluginFactory From(void* self)
    {
        nint* header = (nint*)self;
        var handle = GCHandle.FromIntPtr(header[1]);
        return (PluginFactory)handle.Target!;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int QueryInterface(void* self, Guid* iid, void** obj)
    {
        if (iid == null || obj == null)
            return Result.InvalidArgument;

        *obj = null;

        if (*iid == IIds.FUnknown || *iid == IIds.IPluginFactory)
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
    private static uint Release(void* self)
    {
        var factory = From(self);
        nint* header = (nint*)self;
        var handle = GCHandle.FromIntPtr(header[1]);

        uint count = (uint)Interlocked.Decrement(ref factory._refCount);
        if (count == 0)
        {
            handle.Free();
            NativeMemory.Free(self);
            return 0;
        }

        return count;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int GetFactoryInfo(void* self, PFactoryInfo* info)
    {
        var descriptor = s_descriptor;
        if (info == null || descriptor == null)
            return Result.InvalidArgument;

        info->SetVendor(descriptor.Vendor);
        info->SetUrl(descriptor.Url);
        info->SetEmail(descriptor.Email);
        info->Flags = PFactoryInfo.FlagUnicode;
        return Result.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int CountClasses(void* self) => s_descriptor is null ? 0 : 1;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int GetClassInfo(void* self, int index, PClassInfo* info)
    {
        var descriptor = s_descriptor;
        if (info == null || descriptor == null || index != 0)
            return Result.InvalidArgument;

        info->Cid = descriptor.ClassId;
        info->Cardinality = PClassInfo.ManyInstances;
        info->SetCategory(descriptor.Category);
        info->SetName(descriptor.Name);
        return Result.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int CreateInstance(void* self, Guid* cid, Guid* iid, void** obj)
    {
        if (obj != null)
            *obj = null;

        return Result.NotImplemented;
    }

    private uint AddRefCore() => (uint)Interlocked.Increment(ref _refCount);
}
