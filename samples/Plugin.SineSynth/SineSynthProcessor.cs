using Vst3.Interop;
using Vst3.Plugin;

namespace Plugin.SineSynth;

/// <summary>16-voice sine synth used to verify the MIDI event path.</summary>
internal sealed unsafe class SineSynthProcessor : AudioProcessor
{
    private const int MaxVoices = 16;
    private const int TableSize = 2048;
    private const int TableMask = TableSize - 1;
    private const float AttackSeconds = 0.005f;
    private const float ReleaseSeconds = 0.08f;

    private static readonly float[] SineTable = BuildTable();

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

    public override void SetupProcessing(double sampleRate, int maxBlockSize)
    {
        _sampleRate = sampleRate;
    }

    public override void Process(in ProcessContext context)
    {
        var output = context.Output(0);
        if (output.ChannelCount == 0)
            return;

        for (int i = 0; i < context.EventCount; i++)
        {
            Event e = context.Events[i];
            if (e.Type == EventTypes.NoteOn)
                NoteOn(e.NoteOn.Pitch, e.NoteOn.Velocity);
            else if (e.Type == EventTypes.NoteOff)
                NoteOff(e.NoteOff.Pitch);
        }

        int samples = context.SampleCount;
        var mono = output[0];
        mono.Clear();

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

        float level = (float)Parameters.GetNormalized(0);
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
