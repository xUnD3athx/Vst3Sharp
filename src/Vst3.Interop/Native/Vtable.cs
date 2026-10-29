namespace Vst3.Interop;

/// <summary>Raw access to native COM-style vtables.</summary>
public static unsafe class Vtable
{
    public static nint* Of(void* instance) => *(nint**)instance;

    public static nint Slot(void* instance, int index) => Of(instance)[index];
}
