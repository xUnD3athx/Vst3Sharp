namespace Vst3.Interop;

public static unsafe class Tuid
{
    public const int Size = 16;

    /// <summary>
    /// Reads a TUID as a GUID. Valid on Windows where VST3 is built COM compatible:
    /// both use little-endian data1..data3 followed by the 8 literal bytes.
    /// </summary>
    public static Guid Read(byte* source) => *(Guid*)source;

    public static void Write(byte* destination, Guid value) => *(Guid*)destination = value;
}
