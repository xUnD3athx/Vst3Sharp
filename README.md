# Vst3Sharp

**English** | [简体中文](docs/README.zh-CN.md)

Vst3Sharp lets you write VST3 plugins in C#. NativeAOT compiles them straight into native DLLs, so you skip the .NET runtime. You write no C++, CMake, or vtable code.

# Environment

Windows x64 · .NET 10

## Build

```bash
dotnet build Vst3Sharp.slnx
dotnet publish YourPlugin -c Release -r win-x64
```
## Example plugin

```csharp
using Vst3;     // use Vst3Sharp

[VstPlugin("Gain", Vendor = "Acme", Version = "1.0.0")]
public sealed partial class GainPlugin : AudioPlugin    // declare a VST3 plugin
{
    [Parameter("Gain", Unit = "dB", Min = -24, Max = 24, Default = 0)]
    private readonly FloatParameter _gain = new();      // declare a parameter

    // audio processing
    protected override void Process(AudioContext context)
    {
        var output = context.Output(0);
        var input = context.Input(0);

        foreach (var channel in output.Channels)
        {
            var source = input[channel.Index];
            for (int i = 0; i < channel.Length; i++)
                channel[i] = source[i] * _gain.Linear;
        }
    }
}
```
- Attributes used in the example
### VstPlugin
| Field | Description | Default |
| --- | --- | --- |
| Vendor | Plugin vendor | `"Unknown"` |
| Version | Plugin version | `"1.0.0"` |
| Url | Plugin URL | `""` |
| Email | Contact email | `""` |
| SubCategories | Plugin categories | `Fx` for Effect, `Instrument\|Synth` for Instrument |
| Kind | Plugin kind ( Effect or Instrument ) | `Effect` |
### Parameter
| Field | Description | Default |
| --- | --- | --- |
| ShortTitle | Short title | `null` (falls back to the name) |
| Unit | Parameter unit | `""` |
| Min | Minimum value | `0` |
| Max | Maximum value | `1.0` |
| Default | Default value | `0` |
| Steps | Step size | `0` (continuous) |
| CanAutomate | Whether the parameter can be automated | `true` |
## Docs

- [Plugin API reference](docs/api.md)
