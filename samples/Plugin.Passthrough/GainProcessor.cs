using Vst3.Interop;
using Vst3.Plugin;

namespace Plugin.Passthrough;

/// <summary>Example DSP: gain, optional MIDI note gate and optional sidechain ducking.</summary>
internal sealed unsafe class GainProcessor : AudioProcessor
{
    public override void Process(in ProcessContext context)
    {
        int samples = context.SampleCount;
        if (samples <= 0)
            return;

        double gain = Parameters.GetNormalized(0) * 2.0;
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
        if (sidechainAmount > 0.0 && context.InputBusCount > 1 && context.Inputs != null &&
            context.Inputs[1].NumChannels > 0)
        {
            float peak = 0.0f;
            for (int channel = 0; channel < context.Inputs[1].NumChannels; channel++)
            {
                float* samplesPtr = context.Inputs[1].ChannelBuffers32[channel];
                for (int i = 0; i < samples; i++)
                {
                    float value = samplesPtr[i];
                    if (value < 0.0f)
                        value = -value;
                    if (value > peak)
                        peak = value;
                }
            }

            if (peak > 1.0f)
                peak = 1.0f;

            duck = 1.0 - sidechainAmount * peak;
        }

        float totalGain = (float)(gain * midiGate * duck);

        if (context.Outputs == null || context.OutputBusCount == 0)
            return;

        int outChannels = context.Outputs[0].NumChannels;
        float** outPointers = context.Outputs[0].ChannelBuffers32;

        if (context.Inputs == null || context.InputBusCount == 0)
        {
            for (int channel = 0; channel < outChannels; channel++)
            {
                float* destination = outPointers[channel];
                for (int i = 0; i < samples; i++)
                    destination[i] = 0.0f;
            }

            return;
        }

        int channels = Math.Min(context.Inputs[0].NumChannels, outChannels);
        float** inPointers = context.Inputs[0].ChannelBuffers32;

        for (int channel = 0; channel < channels; channel++)
        {
            float* source = inPointers[channel];
            float* destination = outPointers[channel];
            for (int i = 0; i < samples; i++)
                destination[i] = source[i] * totalGain;
        }

        for (int channel = channels; channel < outChannels; channel++)
        {
            float* destination = outPointers[channel];
            for (int i = 0; i < samples; i++)
                destination[i] = 0.0f;
        }
    }
}
