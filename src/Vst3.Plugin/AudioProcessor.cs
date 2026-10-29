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
