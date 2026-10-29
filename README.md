# Vst3Sharp

Write VST3 plugins in pure C#. Plugins are compiled to native DLLs with NativeAOT, so no CLR starts inside the DAW — and no C++, CMake, or vtable code is required.

**English** | [简体中文](docs/README.zh-CN.md)

Platform: Windows x64 · Runtime: .NET 10

> **Status: pre-release (S7).** The plugin path works end to end, but there is no NuGet package or `dotnet new` template yet — use the repo through project references.

## Capabilities

| Capability | Status |
| --- | --- |
| Effect / instrument plugins | Supported |
| Parameters, automation, state | Supported |
| MIDI events, sidechain | Supported |
| Plugin editor (ImGui + Skia software rendering) | Supported (basic) |
| Hosting API (`Vst3.Hosting`) | Not available; only an offline test host |
| Compatibility | FL Studio and Bitwig verified, VST3 validator 47/47; Cubase untested |

## Requirements

- Windows 10/11 x64
- .NET 10 SDK
- MSVC linker and Windows 10 SDK for NativeAOT (install "Desktop development with C++" with Visual Studio 2022 Build Tools)

## Quick start

```bash
# 1. Build
dotnet build Vst3Sharp.slnx

# 2. Publish both sample plugins (NativeAOT)
dotnet publish samples/Plugin.Passthrough -c Release -r win-x64
dotnet publish samples/Plugin.SineSynth   -c Release -r win-x64

# 3. Verify offline (no DAW needed)
# Effect: passthrough / sidechain / automation / state
dotnet run --project samples/Host.Manual -c Release -- \
  samples/Plugin.Passthrough/bin/Release/net10.0/win-x64/publish/Passthrough.vst3/Contents/x86_64-win/Passthrough.vst3

# Instrument: note-on produces audio, note-off releases it
dotnet run --project samples/Host.Manual -c Release -- synth \
  samples/Plugin.SineSynth/bin/Release/net10.0/win-x64/publish/SineSynth.vst3/Contents/x86_64-win/SineSynth.vst3

# Editor: attach to a hidden parent window, render one frame, write a PNG snapshot
dotnet run --project samples/Host.Manual -c Release -- editor \
  samples/Plugin.Passthrough/bin/Release/net10.0/win-x64/publish/Passthrough.vst3/Contents/x86_64-win/Passthrough.vst3
```

The output is a standard `.vst3` bundle:

```
Passthrough.vst3/Contents/x86_64-win/Passthrough.vst3
```

## Writing a plugin

Derive from `AudioPlugin`, declare metadata and parameters with attributes, and implement `Process`:

```csharp
using Vst3;

[VstPlugin("Gain", Vendor = "Acme", Version = "1.0.0")]
public sealed partial class GainPlugin : AudioPlugin
{
    [Parameter("Gain", Unit = "dB", Min = -24, Max = 24, Default = 0)]
    private readonly FloatParameter _gain = new();

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

Author code contains no `unsafe`, no `[UnmanagedCallersOnly]`, no parameter IDs, descriptors, or GUIDs. The class must be `partial`; the source generator adds the parameter bindings inside the same type.

What `[VstPlugin]` generates:

- A stable class GUID derived from `Vendor/Name` via SHA-256
- The `GetPluginFactory` / `InitDll` / `ExitDll` exports
- If `Vst3.Editor.ImGui` is referenced, the generic parameter editor is wired up automatically; otherwise the plugin has no editor and the host falls back to its generic UI

`[VstPlugin]` options: `Vendor`, `Version`, `Url`, `Email`, `SubCategories`, and `Kind` (`Effect` or `Instrument`, default `Effect`).

`[Parameter]` options:

| Property | Description |
| --- | --- |
| `ShortTitle` | Short name, defaults to the title |
| `Unit` | Display unit (e.g. `dB`, `%`) |
| `Min` / `Max` / `Default` | Range and default value |
| `Steps` | Number of discrete steps; 0 means continuous |
| `CanAutomate` | Whether the parameter is automatable, defaults to `true` |

Parameters must be instance fields (private is fine) of type `FloatParameter`, which exposes `Normalized` (0–1), `Value` (plain value), and `Linear` (`float`).

## Repository layout

```
src/Vst3.Interop              ABI layer: TUID, structs, vtables, result codes
src/Vst3.Plugin               Framework: factory, combined component, parameters/state/automation/events, author API
src/Vst3.Plugin.Generator     Source generator: attributes -> parameter table, class GUID, module exports
src/Vst3.Editor.ImGui         Editor: IPlugView + ImGui.NET + Skia software rendering + GDI blit
samples/Plugin.Passthrough    Effect sample: Gain, SC Amount, sidechain
samples/Plugin.SineSynth      Instrument sample: 16-voice sine synth (MIDI verification)
samples/Host.Manual           Console host: offline effect / synth / editor test paths
```

## Loading in a DAW

Copy the published `*.vst3` directories to:

```
C:\Program Files\Common Files\VST3\
```

Then rescan plugins (FL Studio: Options → Manage plugins → Verify plugins).

- **Passthrough** (effect): the editor shows Gain and SC Amount sliders; Gain at zero should mute the signal. Effects have no MIDI input.
- **SineSynth** (instrument): load it as an instrument, play the MIDI keyboard or piano roll, and you should hear a sine wave; Level controls the output volume.

## Known limitations

- Windows x64 only; audio processing is 32-bit float only, VST3 64-bit processing is not implemented
- Each plugin instance carries its own .NET runtime, so memory use is higher than an equivalent C++ plugin
- GC is not hard real time: the strategy is zero allocation during playback; long-run high-load and UI stress tests are still pending
- The editor uses the default bitmap font with white text; no DPI scaling, CJK IME, or theming yet
- Preset file format, `Vst3.Hosting`, NuGet packages, and templates are not available yet
- Cubase / Nuendo untested; FL Studio and Bitwig pass

## Documentation

- Design notes and roadmap: [docs/vst3-csharp-plan.md](docs/vst3-csharp-plan.md) (Chinese)
- Chinese README: [docs/README.zh-CN.md](docs/README.zh-CN.md)
