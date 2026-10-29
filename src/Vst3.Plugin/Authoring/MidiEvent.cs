using Vst3.Interop;

namespace Vst3;

public enum MidiEventKind
{
    Other,
    NoteOn,
    NoteOff,
}

/// <summary>Managed, allocation-free view over one VST3 event.</summary>
public readonly struct MidiEvent
{
    internal MidiEvent(MidiEventKind kind, int sampleOffset, int channel, int pitch, float velocity)
    {
        Kind = kind;
        SampleOffset = sampleOffset;
        Channel = channel;
        Pitch = pitch;
        Velocity = velocity;
    }

    public MidiEventKind Kind { get; }

    public int SampleOffset { get; }

    public int Channel { get; }

    public int Pitch { get; }

    public float Velocity { get; }

    public bool IsNoteOn => Kind == MidiEventKind.NoteOn;

    public bool IsNoteOff => Kind == MidiEventKind.NoteOff;

    internal static MidiEvent From(in Event e)
    {
        switch (e.Type)
        {
            case EventTypes.NoteOn:
                return new MidiEvent(
                    MidiEventKind.NoteOn, e.SampleOffset, e.NoteOn.Channel, e.NoteOn.Pitch, e.NoteOn.Velocity);

            case EventTypes.NoteOff:
                return new MidiEvent(
                    MidiEventKind.NoteOff, e.SampleOffset, e.NoteOff.Channel, e.NoteOff.Pitch, e.NoteOff.Velocity);

            default:
                return new MidiEvent(MidiEventKind.Other, e.SampleOffset, 0, 0, 0.0f);
        }
    }
}
