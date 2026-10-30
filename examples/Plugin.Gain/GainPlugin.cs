using Vst3;

namespace Plugin.Gain;

[VstPlugin("Gain", Vendor = "UnD3ath")]
public sealed partial class GainPlugin : AudioPlugin
{
    [Parameter("Gain", Unit = "dB", Min = -24, Max = 24, Default = 0)]
    private readonly FloatParameter _gain = new();

    protected override void Process(AudioContext context)
    {
        var input = context.Input(0);
        var output = context.Output(0);

        float gain = MathF.Pow(10.0f, (float)_gain.Value / 20.0f);

        foreach (var channel in output.Channels)
        {
            var source = input[channel.Index];
            for (int i = 0; i < channel.Length; i++)
                channel[i] = source[i] * gain;
        }
    }
}
