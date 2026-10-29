using Vst3.Interop;

namespace Vst3.Plugin;

public unsafe ref struct ProcessContext
{
    public int SampleCount;
    public int InputBusCount;
    public int OutputBusCount;
    public AudioBusBuffers* Inputs;
    public AudioBusBuffers* Outputs;
    public Event* Events;
    public int EventCount;

    public AudioBuffer Input(int index) =>
        Inputs != null && (uint)index < (uint)InputBusCount
            ? new AudioBuffer(&Inputs[index], SampleCount)
            : default;

    public AudioBuffer Output(int index) =>
        Outputs != null && (uint)index < (uint)OutputBusCount
            ? new AudioBuffer(&Outputs[index], SampleCount)
            : default;
}

/// <summary>Span-based view over one audio bus. Author code rarely needs pointers.</summary>
public readonly unsafe ref struct AudioBuffer
{
    private readonly AudioBusBuffers* _bus;
    private readonly int _sampleCount;

    internal AudioBuffer(AudioBusBuffers* bus, int sampleCount)
    {
        _bus = bus;
        _sampleCount = bus != null && bus->NumChannels > 0 ? sampleCount : 0;
    }

    public int ChannelCount => _sampleCount > 0 ? _bus->NumChannels : 0;

    public Span<float> this[int channel]
    {
        get
        {
            if (_sampleCount <= 0 || (uint)channel >= (uint)_bus->NumChannels)
                return Span<float>.Empty;

            return new Span<float>(_bus->ChannelBuffers32[channel], _sampleCount);
        }
    }
}

/// <summary>Base class for the DSP part of a plugin. Runs on the audio thread.</summary>
public abstract unsafe class AudioProcessor
{
    public ParameterCollection Parameters { get; internal set; } = null!;

    public double SampleRate { get; internal set; }

    public int MaxBlockSize { get; internal set; }

    public virtual void SetupProcessing(double sampleRate, int maxBlockSize)
    {
    }

    public virtual void SetActive(bool active)
    {
    }

    public virtual void Terminate()
    {
    }

    public abstract void Process(in ProcessContext context);
}
