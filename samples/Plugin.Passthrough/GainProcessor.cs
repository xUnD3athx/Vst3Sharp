using Vst3.Interop;
using Vst3.Plugin;

namespace Plugin.Passthrough;

/// <summary>Example DSP: gain, optional MIDI note gate and optional sidechain ducking.</summary>
internal sealed unsafe class GainProcessor : AudioProcessor
{
    public override void Process(in ProcessContext context)
    {
        var output = context.Output(0);
        if (output.ChannelCount == 0)
            return;

        float gain = (float)(Parameters.GetNormalized(0) * 2.0);
        double sidechainAmount = Parameters.GetNormalized(1);

        double midiGate = 1.0;
        for (int i = 0; i < context.EventCount; i++)
        {
            Event e = context.Events[i];
            if (e.Type == EventTypes.NoteOn)
                midiGate = Math.Clamp(e.NoteOn.Velocity, 0.0f, 1.0f);
            else if (e.Type == EventTypes.NoteOff)
                midiGate = 1.0;
        }

        double duck = 1.0;
        var sidechain = context.Input(1);
        if (sidechainAmount > 0.0 && sidechain.ChannelCount > 0)
        {
            float peak = 0.0f;
            for (int channel = 0; channel < sidechain.ChannelCount; channel++)
            {
                var samples = sidechain[channel];
                for (int i = 0; i < samples.Length; i++)
                {
                    float value = MathF.Abs(samples[i]);
                    if (value > peak)
                        peak = value;
                }
            }

            duck = 1.0 - sidechainAmount * Math.Min(1.0, peak);
        }

        float totalGain = (float)(gain * midiGate * duck);
        var input = context.Input(0);
        int channels = Math.Min(input.ChannelCount, output.ChannelCount);

        for (int channel = 0; channel < channels; channel++)
        {
            var source = input[channel];
            var destination = output[channel];
            for (int i = 0; i < destination.Length; i++)
                destination[i] = source[i] * totalGain;
        }

        for (int channel = channels; channel < output.ChannelCount; channel++)
            output[channel].Clear();
    }
}
