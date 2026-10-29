using Vst3;

namespace Plugin.SineSynth;

[VstPlugin("SineSynth", Vendor = "UnD3ath", Url = "https://example.invalid", Email = "dev@example.invalid", Kind = PluginKind.Instrument)]
public sealed partial class SineSynthPlugin : AudioPlugin
{
    private const int MaxVoices = 16;
    private const int TableSize = 2048;
    private const int TableMask = TableSize - 1;
    private const float AttackSeconds = 0.005f;
    private const float ReleaseSeconds = 0.08f;

    private static readonly float[] SineTable = BuildTable();

    [Parameter("Level", Min = 0.0, Max = 1.0, Default = 0.5)]
    private readonly FloatParameter _level = new();

    private readonly Voice[] _voices = new Voice[MaxVoices];
    private double _sampleRate = 48000.0;
    private int _nextVoice;

    private struct Voice
    {
        public bool Active;
        public int Note;
        public double Phase;
        public double Increment;
        public float Amplitude;
        public float Envelope;
        public float EnvelopeStep;
        public int EnvelopeDirection;
    }

    public override void OnSetup(double sampleRate, int maxBlockSize) => _sampleRate = sampleRate;

    protected override void Process(AudioContext context)
    {
        var output = context.Output(0);
        if (output.ChannelCount == 0)
            return;

        foreach (var e in context.Events)
        {
            if (e.IsNoteOn)
                NoteOn(e.Pitch, e.Velocity);
            else if (e.IsNoteOff)
                NoteOff(e.Pitch);
        }

        var mono = output[0];
        mono.Clear();
        int samples = mono.Length;

        for (int v = 0; v < MaxVoices; v++)
        {
            ref Voice voice = ref _voices[v];
            if (!voice.Active)
                continue;

            double phase = voice.Phase;
            double increment = voice.Increment;
            float envelope = voice.Envelope;
            float step = voice.EnvelopeStep;
            int direction = voice.EnvelopeDirection;
            float amplitude = voice.Amplitude;

            for (int i = 0; i < samples; i++)
            {
                mono[i] += SineTable[(int)phase & TableMask] * envelope * amplitude;

                phase += increment;
                if (phase >= TableSize)
                    phase -= TableSize;

                if (direction > 0)
                {
                    envelope += step;
                    if (envelope >= 1.0f)
                    {
                        envelope = 1.0f;
                        step = 0.0f;
                    }
                }
                else if (direction < 0)
                {
                    envelope += step;
                    if (envelope <= 0.0f)
                    {
                        envelope = 0.0f;
                        step = 0.0f;
                        break;
                    }
                }
            }

            voice.Phase = phase;
            voice.Envelope = envelope;
            voice.EnvelopeStep = step;

            if (direction < 0 && envelope <= 0.0f)
            {
                voice.Active = false;
                voice.EnvelopeDirection = 0;
            }
        }

        float level = (float)_level.Value;
        if (level != 1.0f)
        {
            for (int i = 0; i < samples; i++)
                mono[i] *= level;
        }

        for (int channel = 1; channel < output.ChannelCount; channel++)
            mono.CopyTo(output[channel]);
    }

    private void NoteOn(int pitch, float velocity)
    {
        int index = FindVoice();
        ref Voice voice = ref _voices[index];
        voice.Active = true;
        voice.Note = pitch;
        voice.Phase = 0.0;
        voice.Increment = Frequency(pitch) * TableSize / _sampleRate;
        voice.Amplitude = Math.Clamp(velocity, 0.0f, 1.0f);
        voice.Envelope = 0.0f;
        voice.EnvelopeDirection = 1;
        voice.EnvelopeStep = 1.0f / (AttackSeconds * (float)_sampleRate);
    }

    private void NoteOff(int pitch)
    {
        for (int i = 0; i < MaxVoices; i++)
        {
            ref Voice voice = ref _voices[i];
            if (voice.Active && voice.Note == pitch && voice.EnvelopeDirection > 0)
            {
                voice.EnvelopeDirection = -1;
                voice.EnvelopeStep = -1.0f / (ReleaseSeconds * (float)_sampleRate);
            }
        }
    }

    private int FindVoice()
    {
        for (int i = 0; i < MaxVoices; i++)
        {
            if (!_voices[i].Active)
                return i;
        }

        int index = _nextVoice;
        _nextVoice = (_nextVoice + 1) % MaxVoices;
        return index;
    }

    private static double Frequency(int pitch) => 440.0 * Math.Pow(2.0, (pitch - 69) / 12.0);

    private static float[] BuildTable()
    {
        var table = new float[TableSize];
        for (int i = 0; i < TableSize; i++)
            table[i] = MathF.Sin(2.0f * MathF.PI * i / TableSize);

        return table;
    }
}
