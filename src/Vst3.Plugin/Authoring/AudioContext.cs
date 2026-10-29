using Vst3.Interop;

namespace Vst3;

/// <summary>Safe, allocation-free audio view over one process block.</summary>
public readonly unsafe ref struct AudioContext
{
    private readonly AudioBusBuffers* _inputs;
    private readonly int _inputCount;
    private readonly AudioBusBuffers* _outputs;
    private readonly int _outputCount;
    private readonly MidiEvent* _events;
    private readonly int _eventCount;

    internal AudioContext(
        int sampleCount,
        AudioBusBuffers* inputs,
        int inputCount,
        AudioBusBuffers* outputs,
        int outputCount,
        MidiEvent* events,
        int eventCount)
    {
        SampleCount = sampleCount;
        _inputs = inputs;
        _inputCount = inputCount;
        _outputs = outputs;
        _outputCount = outputCount;
        _events = events;
        _eventCount = eventCount;
    }

    public int SampleCount { get; }

    public AudioBuffer Input(int bus) =>
        _inputs != null && (uint)bus < (uint)_inputCount
            ? new AudioBuffer(&_inputs[bus], SampleCount)
            : default;

    public AudioBuffer Output(int bus) =>
        _outputs != null && (uint)bus < (uint)_outputCount
            ? new AudioBuffer(&_outputs[bus], SampleCount)
            : default;

    public ReadOnlySpan<MidiEvent> Events =>
        _eventCount > 0 ? new ReadOnlySpan<MidiEvent>(_events, _eventCount) : ReadOnlySpan<MidiEvent>.Empty;
}

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

    public ChannelList Channels => new(this);

    public readonly ref struct ChannelList
    {
        private readonly AudioBuffer _buffer;

        internal ChannelList(AudioBuffer buffer) => _buffer = buffer;

        public Enumerator GetEnumerator() => new(_buffer);

        public ref struct Enumerator
        {
            private readonly AudioBuffer _buffer;
            private int _index;

            internal Enumerator(AudioBuffer buffer)
            {
                _buffer = buffer;
                _index = -1;
            }

            public AudioChannel Current => new(_buffer[_index], _index);

            public bool MoveNext()
            {
                _index++;
                return _index < _buffer.ChannelCount;
            }
        }
    }
}

public readonly ref struct AudioChannel
{
    private readonly Span<float> _samples;

    internal AudioChannel(Span<float> samples, int index)
    {
        _samples = samples;
        Index = index;
    }

    public int Index { get; }

    public int Length => _samples.Length;

    public Span<float> Samples => _samples;

    public float this[int index]
    {
        get => _samples[index];
        set => _samples[index] = value;
    }
}
