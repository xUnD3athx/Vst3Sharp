namespace Vst3.Plugin;

/// <summary>Normalized parameter storage for one processor instance. Allocation-free at runtime.</summary>
public sealed class ParameterCollection
{
    private readonly ParameterDescriptor[] _descriptors;
    private readonly double[] _normalized;

    internal ParameterCollection(IReadOnlyList<ParameterDescriptor> descriptors)
    {
        _descriptors = new ParameterDescriptor[descriptors.Count];
        _normalized = new double[descriptors.Count];

        for (int i = 0; i < descriptors.Count; i++)
        {
            _descriptors[i] = descriptors[i];
            _normalized[i] = ToNormalized(i, descriptors[i].Default);
        }
    }

    public int Count => _descriptors.Length;

    public ParameterDescriptor GetDescriptor(int index) => _descriptors[index];

    public int IndexOf(uint id)
    {
        for (int i = 0; i < _descriptors.Length; i++)
        {
            if (_descriptors[i].Id == id)
                return i;
        }

        return -1;
    }

    public double GetNormalized(int index) => _normalized[index];

    public void SetNormalized(int index, double value) => _normalized[index] = Math.Clamp(value, 0.0, 1.0);

    public double GetNormalized(uint id)
    {
        int index = IndexOf(id);
        return index >= 0 ? _normalized[index] : 0.0;
    }

    public void SetNormalized(uint id, double value)
    {
        int index = IndexOf(id);
        if (index >= 0)
            _normalized[index] = Math.Clamp(value, 0.0, 1.0);
    }

    public double ToPlain(int index, double normalized)
    {
        var descriptor = _descriptors[index];
        return descriptor.Min + normalized * (descriptor.Max - descriptor.Min);
    }

    public double ToNormalized(int index, double plain)
    {
        var descriptor = _descriptors[index];
        double range = descriptor.Max - descriptor.Min;
        if (range <= 0.0)
            return 0.0;

        return Math.Clamp((plain - descriptor.Min) / range, 0.0, 1.0);
    }
}
