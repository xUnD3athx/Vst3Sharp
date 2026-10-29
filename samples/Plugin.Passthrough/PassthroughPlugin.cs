using Vst3;
using Vst3.Plugin;

namespace Plugin.Passthrough;

public sealed class PassthroughPlugin : AudioPlugin
{
    private readonly FloatParameter _gain = new();
    private readonly FloatParameter _sidechainAmount = new();

    public override IReadOnlyList<ParameterDescriptor> DeclareParameters() => new ParameterDescriptor[]
    {
        new(0, "Gain", "Gain", "x", Min: 0.0, Max: 2.0, Default: 1.0),
        new(1, "SC Amount", "SC", "%", Min: 0.0, Max: 100.0, Default: 0.0),
    };

    public override void BindParameters()
    {
        Bind(0, _gain);
        Bind(1, _sidechainAmount);
    }

    protected override void Process(AudioContext context)
    {
        var output = context.Output(0);
        if (output.ChannelCount == 0)
            return;

        double gate = 1.0;
        foreach (var e in context.Events)
        {
            if (e.IsNoteOn)
                gate = Math.Clamp(e.Velocity, 0.0f, 1.0f);
            else if (e.IsNoteOff)
                gate = 1.0;
        }

        double duck = 1.0;
        double sidechainAmount = _sidechainAmount.Normalized;
        var sidechain = context.Input(1);
        if (sidechainAmount > 0.0 && sidechain.ChannelCount > 0)
        {
            float peak = 0.0f;
            foreach (var channel in sidechain.Channels)
            {
                for (int i = 0; i < channel.Length; i++)
                {
                    float value = MathF.Abs(channel[i]);
                    if (value > peak)
                        peak = value;
                }
            }

            duck = 1.0 - sidechainAmount * Math.Min(1.0, peak);
        }

        float totalGain = (float)(_gain.Linear * gate * duck);
        var input = context.Input(0);

        foreach (var channel in output.Channels)
        {
            var source = input[channel.Index];
            for (int i = 0; i < channel.Length; i++)
                channel[i] = source[i] * totalGain;
        }
    }
}
