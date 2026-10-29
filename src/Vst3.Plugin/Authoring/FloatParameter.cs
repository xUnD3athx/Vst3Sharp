using Vst3.Plugin;

namespace Vst3;

/// <summary>Typed normalized/plain parameter handle bound by the generated plugin code.</summary>
public sealed class FloatParameter
{
    private ParameterCollection? _owner;
    private int _index;

    internal void Attach(ParameterCollection owner, int index)
    {
        _owner = owner;
        _index = index;
    }

    public double Normalized
    {
        get => _owner?.GetNormalized(_index) ?? 0.0;
        set
        {
            if (_owner != null)
                _owner.SetNormalized(_index, value);
        }
    }

    public double Value
    {
        get => _owner?.ToPlain(_index, Normalized) ?? 0.0;
        set
        {
            if (_owner != null)
                _owner.SetNormalized(_index, _owner.ToNormalized(_index, value));
        }
    }

    public float Linear => (float)Value;
}
