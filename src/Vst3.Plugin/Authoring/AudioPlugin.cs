using System.ComponentModel;
using Vst3.Plugin;

namespace Vst3;

/// <summary>Author-facing plugin base class. Implement <see cref="Process"/> and declare parameters with attributes.</summary>
public abstract class AudioPlugin
{
    internal ParameterCollection ParameterStore { get; private set; } = null!;

    protected ParameterCollection Parameters => ParameterStore;

    protected abstract void Process(AudioContext context);

    public virtual void OnSetup(double sampleRate, int maxBlockSize)
    {
    }

    public virtual void OnActivated(bool active)
    {
    }

    public virtual void OnTerminate()
    {
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public virtual IReadOnlyList<ParameterDescriptor> DeclareParameters() =>
        Array.Empty<ParameterDescriptor>();

    [EditorBrowsable(EditorBrowsableState.Never)]
    public virtual void BindParameters()
    {
    }

    protected void Bind(int index, FloatParameter parameter) => parameter.Attach(ParameterStore, index);

    internal void Initialize(ParameterCollection parameters)
    {
        ParameterStore = parameters;
        BindParameters();
    }

    internal void Run(in AudioContext context) => Process(context);
}
