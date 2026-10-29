using System.Runtime.InteropServices;

namespace Vst3.Interop;

public static class EventTypes
{
    public const ushort NoteOn = 0;
    public const ushort NoteOff = 1;
    public const ushort Data = 2;
    public const ushort PolyPressure = 3;
    public const ushort LegacyMidiCCOut = 65535;
}

[StructLayout(LayoutKind.Sequential)]
public struct NoteOnEvent
{
    public short Channel;
    public short Pitch;
    public float Tuning;
    public float Velocity;
    public int Length;
    public int NoteId;
}

[StructLayout(LayoutKind.Sequential)]
public struct NoteOffEvent
{
    public short Channel;
    public short Pitch;
    public float Velocity;
    public int NoteId;
    public float Tuning;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct DataEvent
{
    public uint Size;
    public uint Type;
    public byte* Bytes;
}

/// <summary>
/// 48-byte event. The union starts at offset 24 (after the 8-byte ppq position,
/// 2-byte flags and 2-byte type plus 4 bytes of padding).
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 48)]
public unsafe struct Event
{
    [FieldOffset(0)] public int BusIndex;
    [FieldOffset(4)] public int SampleOffset;
    [FieldOffset(8)] public double PpqPosition;
    [FieldOffset(16)] public ushort Flags;
    [FieldOffset(18)] public ushort Type;
    [FieldOffset(24)] public NoteOnEvent NoteOn;
    [FieldOffset(24)] public NoteOffEvent NoteOff;
    [FieldOffset(24)] public DataEvent Data;
}
