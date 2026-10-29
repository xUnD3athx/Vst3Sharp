namespace Vst3.Plugin;

public enum PluginKind
{
    /// <summary>Audio effect: audio in (main + sidechain) -> audio out.</summary>
    Effect,

    /// <summary>Instrument: MIDI event in -> audio out, no audio input.</summary>
    Instrument,
}
