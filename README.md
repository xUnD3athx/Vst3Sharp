# Vst3Sharp

**English** | [简体中文](docs/README.zh-CN.md)

Write VST3 plugins in C#. NativeAOT compiles them into native DLLs, so the DAW loads a plugin rather than starting a .NET runtime. No C++, CMake, or vtable code.

Windows x64 · .NET 10 · pre-release (S7)

The samples cover effects, instruments, parameters and automation, state save/restore, MIDI, sidechain, and an ImGui editor. FL Studio and Bitwig load them; the VST3 validator passes 47/47. There is no NuGet package or `dotnet new` template yet, so use the repo through project references.

## Build

```bash
dotnet build Vst3Sharp.slnx
dotnet publish samples/Plugin.Passthrough -c Release -r win-x64
dotnet publish samples/Plugin.SineSynth   -c Release -r win-x64
```

NativeAOT needs the MSVC linker and Windows 10 SDK (Visual Studio 2022 Build Tools, "Desktop development with C++").

## Offline tests

No DAW required. The first command tests an effect, `synth` tests an instrument, `editor` renders one frame and writes a PNG.

```bash
dotnet run --project samples/Host.Manual -c Release -- \
  samples/Plugin.Passthrough/bin/Release/net10.0/win-x64/publish/Passthrough.vst3/Contents/x86_64-win/Passthrough.vst3

dotnet run --project samples/Host.Manual -c Release -- synth \
  samples/Plugin.SineSynth/bin/Release/net10.0/win-x64/publish/SineSynth.vst3/Contents/x86_64-win/SineSynth.vst3

dotnet run --project samples/Host.Manual -c Release -- editor \
  samples/Plugin.Passthrough/bin/Release/net10.0/win-x64/publish/Passthrough.vst3/Contents/x86_64-win/Passthrough.vst3
```

## A plugin

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

The class has to be `partial`: the source generator adds parameter bindings to the same type. It also derives a stable class GUID from `Vendor/Name` and emits `GetPluginFactory`, `InitDll`, and `ExitDll`. If the project references `Vst3.Editor.ImGui`, the generic editor is attached automatically.

`[VstPlugin]` takes `Vendor`, `Version`, `Url`, `Email`, `SubCategories`, and `Kind` (`Effect` or `Instrument`). `[Parameter]` takes `ShortTitle`, `Unit`, `Min`, `Max`, `Default`, `Steps`, and `CanAutomate`. Parameter fields must be instance `FloatParameter`s; they expose `Normalized` (0–1), `Value`, and `Linear`.

## Layout

```
src/Vst3.Interop              ABI: TUID, structs, vtables, result codes
src/Vst3.Plugin               Framework: factory, combined component, parameters/state/events, author API
src/Vst3.Plugin.Generator     Source generator: attributes -> parameters, class GUID, module exports
src/Vst3.Editor.ImGui         IPlugView + ImGui.NET + Skia software rendering + GDI blit
samples/Plugin.Passthrough    Effect: gain, SC amount, sidechain
samples/Plugin.SineSynth      Instrument: 16-voice sine synth
samples/Host.Manual           Console host for the offline tests
```

## Install in a DAW

Copy the published `*.vst3` folders to `C:\Program Files\Common Files\VST3\` and rescan. Passthrough is an effect; SineSynth is an instrument and responds to MIDI notes.

## Not done yet

- Windows x64 only, and audio is 32-bit float only; no 64-bit processing.
- Each instance carries its own .NET runtime, so memory use is higher than an equivalent C++ plugin.
- GC is not hard real time. Playback allocates nothing, but long-run and UI stress tests are pending.
- The editor uses the default bitmap font with white text; no DPI scaling, IME, or theming.
- No hosting API (only the test host), preset format, NuGet package, or templates.
- Cubase/Nuendo untested.

## Docs

- [Design notes and roadmap](docs/vst3-csharp-plan.md) (Chinese)
- [中文说明](docs/README.zh-CN.md)
