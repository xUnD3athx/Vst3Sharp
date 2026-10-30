# Plugin API

**English** | [简体中文](api-Zh-CN.md)

## Contents

- [Namespaces](#namespaces)
- [Type overview](#type-overview)
- [Vst3](#vst3)
  - [AudioPlugin](#audioplugin)
  - [AudioContext](#audiocontext)
  - [AudioBuffer / AudioChannel](#audiobuffer--audiochannel)
  - [FloatParameter](#floatparameter)
  - [MidiEvent](#midievent)
  - [Attributes](#attributes)
- [Bus layout](#bus-layout)
- [Parameter behavior](#parameter-behavior)
  - [ParameterCollection](#parametercollection)
- [Generator](#generator)
- [Editor](#editor)
  - [EditorView](#editorview)
  - [EditorFont](#editorfont)
  - [UiCanvas](#uicanvas)
  - [Input](#input)
- [Low-level API](#low-level-api)

## Namespaces

| Namespace | Contents | Use |
| --- | --- | --- |
| `Vst3` | Author API: `AudioPlugin`, `AudioContext`, `FloatParameter`, `MidiEvent`, attributes | Writing plugins |
| `Vst3.Plugin` | Framework internals: `AudioProcessor`, `PluginDescriptor`, `ParameterCollection` | Bypassing the generator, or using the lower-level base class directly |
| `Vst3.Editor` | Editor: `EditorView`, `UiCanvas`, input types (`Vst3.Editor.ImGui` package) | Custom interfaces |
| `Vst3.Interop` | Raw VST3 ABI: structs, vtables, result codes | Implementing VST3 interfaces directly |

## Type overview

| Type | Namespace | Purpose |
| --- | --- | --- |
| `AudioPlugin` | `Vst3` | Plugin base class |
| `AudioContext` | `Vst3` | One audio block |
| `AudioBuffer`, `AudioChannel` | `Vst3` | Bus and channel views |
| `FloatParameter` | `Vst3` | Parameter field handle |
| `MidiEvent`, `MidiEventKind` | `Vst3` | MIDI events |
| `PluginKind` | `Vst3` | `Effect` / `Instrument` |
| `VstPluginAttribute`, `ParameterAttribute` | `Vst3` | Generator input |
| `EditorView` | `Vst3.Editor` | Editor base class |
| `EditorFont`, `EditorGlyphRange` | `Vst3.Editor` | TTF/OTF/TTC font configuration |
| `UiCanvas`, `UiColor` | `Vst3.Editor` | Drawing |
| `UiInput`, `UiPointerEvent`, `UiWheelEvent`, `UiKeyEvent` | `Vst3.Editor` | Input |
| `UiKey`, `UiMouseButton`, `UiModifiers`, `UiPointerAction` | `Vst3.Editor` | Input enums |
| `AudioProcessor`, `ProcessContext` | `Vst3.Plugin` | Lower-level DSP base class |
| `PluginDescriptor`, `ParameterDescriptor` | `Vst3.Plugin` | Hand-built descriptors |
| `ParameterCollection` | `Vst3.Plugin` | Parameter storage |
| `PluginViewContext`, `IPluginViewFactory` | `Vst3.Plugin` | Custom view factories |
| `Vst3Plugin` | `Vst3.Plugin` | Module entry points |

## Vst3

### AudioPlugin

| Member | Signature | Notes |
| --- | --- | --- |
| `Process` | `protected abstract void Process(AudioContext context)` | Abstract; runs on the audio thread |
| `OnSetup` | `virtual void OnSetup(double sampleRate, int maxBlockSize)` | Maps to `setupProcessing` |
| `OnActivated` | `virtual void OnActivated(bool active)` | Maps to `setActive` |
| `OnTerminate` | `virtual void OnTerminate()` | Called before destruction |
| `Parameters` | `protected ParameterCollection Parameters { get; }` | Parameters for this instance |
| `DeclareParameters` | `virtual IReadOnlyList<ParameterDescriptor>` | Implemented by the generator |
| `BindParameters` | `virtual void BindParameters()` | Implemented by the generator |

One VST3 instance corresponds to one plugin object. All callbacks except `Process` run on the host's main thread.

### AudioContext

| Member | Signature | Notes |
| --- | --- | --- |
| `SampleCount` | `int` | Samples in this block |
| `Input` | `AudioBuffer Input(int bus)` | Audio input bus |
| `Output` | `AudioBuffer Output(int bus)` | Audio output bus |
| `Events` | `ReadOnlySpan<MidiEvent>` | Events in this block, retaining the original `SampleOffset` |

A `ref struct`, valid only within the current `Process` call. A missing bus or an out-of-range index returns an empty buffer with `ChannelCount == 0`. The framework does not split audio blocks by event offset; the processing point of each event is decided by `Process`.

### AudioBuffer / AudioChannel

| `AudioBuffer` | Notes |
| --- | --- |
| `ChannelCount` | Number of channels |
| `this[int channel]` | `Span<float>`; out-of-range indexes return an empty span |
| `Channels` | `foreach`-enumerable; elements are `AudioChannel` |

| `AudioChannel` | Notes |
| --- | --- |
| `Index` | Channel index |
| `Length` | Sample count |
| `Samples` | `Span<float>` |
| `this[int i]` | Reads or writes a single sample |

Audio is always 32-bit float.

### FloatParameter

| Member | Signature | Notes |
| --- | --- | --- |
| `Normalized` | `double` | 0–1; the value used by automation and the host |
| `Value` | `double` | Plain value, mapped through `Min`/`Max` |
| `Linear` | `float` | `(float)Value`, for use as a multiplier |

Created when the field is initialized and bound by the generator after construction; reads return 0 before binding.

### MidiEvent

| Member | Signature | Notes |
| --- | --- | --- |
| `Kind` | `MidiEventKind` | `Other` / `NoteOn` / `NoteOff` |
| `SampleOffset` | `int` | Offset within the block |
| `Channel` | `int` | MIDI channel |
| `Pitch` | `int` | 0–127 |
| `Velocity` | `float` | 0–1 |
| `IsNoteOn` / `IsNoteOff` | `bool` | Convenience checks on `Kind` |

Only note on/off are parsed; other events appear as `Other` with `Pitch` and `Velocity` set to 0.

### Attributes

`[VstPlugin(name)]`:

| Field | Notes | Default |
| --- | --- | --- |
| `Vendor` | Vendor | `"Unknown"` |
| `Version` | Version | `"1.0.0"` |
| `Url` | Website | `""` |
| `Email` | Contact email | `""` |
| `SubCategories` | Categories | `Fx` for Effect, `Instrument\|Synth` for Instrument |
| `Kind` | Plugin kind | `PluginKind.Effect` |
| `Editor` | Custom `EditorView` type | `null` (default panel) |

`[Parameter(name)]`:

| Field | Notes | Default |
| --- | --- | --- |
| `ShortTitle` | Short title | `null` (uses the name) |
| `Unit` | Unit; display only | `""` |
| `Min` | Minimum value | `0` |
| `Max` | Maximum value | `1.0` |
| `Default` | Default value | `0` |
| `Steps` | Step count; metadata only | `0` (continuous) |
| `CanAutomate` | Automatable | `true` |

The generator handles `partial`, non-generic, non-nested classes; each assembly exposes one `[VstPlugin]` class; `[Parameter]` fields are instance `FloatParameter`s.

## Bus layout

| `Kind` | Audio input | Audio output | Event input |
| --- | --- | --- | --- |
| `Effect` | 0 Input, 1 Sidechain (stereo, Aux, inactive by default) | 0 Output (stereo) | None |
| `Instrument` | None | 0 Output (stereo) | 0 MIDI In (16 channels) |

Stereo arrangements only.

## Parameter behavior

- Parameter IDs are assigned in field declaration order (0, 1, …). Changing field order after release misaligns the parameters of existing projects.
- `Steps` and `Unit` affect host display only; they do not quantize values.
- Automation applies per audio block: each parameter takes only the last point in its queue, written before `Process`.
- State stores normalized values only (magic + parameter count + one double per parameter). When the parameter count changes, the old state is rejected and `setState` returns false.
- UI and automation write the parameter array directly, without locking.

### ParameterCollection

| Member | Signature |
| --- | --- |
| `Count` | `int` |
| `GetDescriptor` | `ParameterDescriptor GetDescriptor(int index)` |
| `IndexOf` | `int IndexOf(uint id)` |
| `GetNormalized` | `double GetNormalized(int index)` / `(uint id)` |
| `SetNormalized` | `void SetNormalized(int index, double value)` / `(uint id, double)` |
| `ToPlain` | `double ToPlain(int index, double normalized)` |
| `ToNormalized` | `double ToNormalized(int index, double plain)` |

## Generator

The generator produces `DeclareParameters` and `BindParameters` for the `partial` class and emits `<ClassName>.Vst3.g.cs`:

- `Vst3Module` exports `GetPluginFactory`, `InitDll`, and `ExitDll`.
- The class GUID is the first 16 bytes of `SHA-256("Vst3Sharp/" + Vendor + "/" + Name)`; changing `Vendor` or `Name` changes the GUID.
- With a reference to `Vst3.Editor.ImGui`, the default panel is attached automatically; `Editor = typeof(...)` selects the custom editor instead.
- Parameter IDs follow field declaration order.

Diagnostics:

| ID | Cause |
| --- | --- |
| VST3001 | More than one `[VstPlugin]` in the assembly |
| VST3002 | Class does not derive from `AudioPlugin` |
| VST3003 | `[Parameter]` field is not a `FloatParameter` |
| VST3004 | Class is not marked `partial` |
| VST3005 | Generic or nested class |
| VST3006 | `[Parameter]` field is static |
| VST3007 | Type named by `Editor` does not derive from `EditorView` |
| VST3008 | Type named by `Editor` has no public parameterless constructor |

## Editor

Without `Editor`, the default panel shows one row per parameter with a title, slider, and plain value, and supports wheel adjustment.

A custom editor derives from `EditorView` and is specified through `[VstPlugin]`:

```csharp
[VstPlugin("MySynth", Vendor = "Acme", Editor = typeof(MyEditor))]
public sealed partial class MyPlugin : AudioPlugin { ... }

public sealed class MyEditor : EditorView
{
    public override void OnRender(UiCanvas ui)
    {
        ui.Rect(0, 0, ui.Width, ui.Height, new UiColor(24, 24, 28));
        ui.Circle(80, 80, 40, new UiColor(64, 132, 220));
        ui.Text(24, 140, "Hello", new UiColor(230, 230, 236), 18);
    }

    public override void OnPointerDown(in UiPointerEvent e) { /* hit testing */ }
}
```

### EditorView

| Member | Signature |
| --- | --- |
| `Context` | `PluginViewContext` |
| `Input` | `UiInput` |
| `Width` / `Height` | `int` |
| `Parameters` | `protected ParameterCollection` |
| `SetParameter` | `protected void SetParameter(uint id, double normalized)`; notifies the host |
| `BeginEdit` / `EndEdit` | `protected void BeginEdit(uint id)` / `EndEdit(uint id)` |
| `OnAttach` / `OnDetach` | `protected internal virtual` |
| `OnRender` | `virtual void OnRender(UiCanvas canvas)` |
| `OnPointerDown` / `OnPointerUp` / `OnPointerMove` | `virtual void ... (in UiPointerEvent e)` |
| `OnWheel` | `virtual void OnWheel(in UiWheelEvent e)` |
| `OnKey` | `virtual void OnKey(in UiKeyEvent e)` |
| `OnText` | `virtual void OnText(char c)` |
| `OnFocusChanged` | `virtual void OnFocusChanged(bool focused)` |
| `OnResize` | `virtual void OnResize(int width, int height)` |

### EditorFont

The editor uses ImGui's built-in bitmap font by default. To get smoother text or non-Latin glyphs, configure a TTF/OTF/TTC before the first editor opens:

```csharp
using Vst3.Editor;

static GainPlugin()
{
    EditorFont.UseFile(
        @"C:\Windows\Fonts\msyh.ttc",
        pixelSize: 16,
        glyphRange: EditorGlyphRange.ChineseSimplified);
}
```

| Member | Notes |
| --- | --- |
| `UseFile(path, pixelSize = 16, glyphRange = Default, faceIndex = 0)` | Selects a font file and sets size, glyph range, and TTC face index |
| `FilePath` | Font path; relative paths resolve against the plugin module directory |
| `PixelSize` | Base size in pixels; defaults to 16 |
| `GlyphRange` | `EditorGlyphRange`: `Default`, `Latin`, `Greek`, `Cyrillic`, `ChineseSimplified`, `ChineseFull`, `Japanese`, `Korean`, `Thai`, `Vietnamese` |
| `FaceIndex` | Face index inside a TTC collection; 0 for single-face files |
| `Reset()` | Restores the built-in bitmap font |

- A missing file, unsupported extension, or invalid header silently falls back to the bitmap font instead of bringing the host down.
- Prefer `ChineseSimplified` for Chinese UI text (common glyphs, smaller atlas); `ChineseFull` covers more glyphs but grows the atlas significantly.
- Fonts can ship with the plugin: `.ttf` / `.otf` / `.ttc` files in the publish root are copied into the `.vst3` bundle.
- Offline diagnostics: the `VST3SHARP_FONT` environment variable overrides the path, and `VST3SHARP_FONT_RANGE` overrides the glyph range (for example `ChineseSimplified`).

### UiCanvas

Valid only inside `OnRender`; coordinates are view-local physical pixels.

| Method | Signature |
| --- | --- |
| `Width` / `Height` | `float` |
| `Rect` | `Rect(x, y, width, height, UiColor)` |
| `RoundRect` | `RoundRect(x, y, width, height, radius, UiColor)` |
| `RectOutline` | `RectOutline(x, y, width, height, UiColor, thickness = 1)` |
| `Circle` | `Circle(centerX, centerY, radius, UiColor, filled = true)` |
| `Line` | `Line(x1, y1, x2, y2, UiColor, thickness = 1)` |
| `Text` | `Text(x, y, string, UiColor, size = 0)`; `size` 0 uses the default font size |
| `MeasureText` | `Vector2 MeasureText(string, size = 0)` |
| `PushClip` / `PopClip` | `PushClip(x, y, width, height)` / `PopClip()` |

### Input

| `UiPointerEvent` | Notes |
| --- | --- |
| `Action` | `UiPointerAction.Move` / `Down` / `Up` |
| `Button` | The button that changed; `None` for move events |
| `X` / `Y` | View coordinates |
| `Modifiers` | `UiModifiers` flags |
| `Position` | `Vector2` |

`UiWheelEvent`: `X`, `Y`, `Delta` (1 is one notch up), `Modifiers`, `Position`.

`UiKeyEvent`: `Key` (`UiKey`), `VirtualKey`, `IsDown`, `IsRepeat`, `Modifiers`.

`UiInput`: `MouseX`, `MouseY`, `MousePosition`, `Modifiers`, `WheelDelta`, `IsDown(UiMouseButton)`.

The framework keeps mouse capture from press to release, so Move events still arrive outside the window. Move events carry no button state; button state is available through `Input.IsDown`. The framework provides no widget tree; hit testing, layout, and focus management are the plugin author's responsibility. The keyboard maps common keys only; IME is not implemented.

## Low-level API

Without the generator, the plugin constructs `PluginDescriptor` itself and also supplies the module exports, `[UnmanagedCallersOnly]`, and `Vst3Plugin.Configure`.

| `PluginDescriptor` | Default |
| --- | --- |
| `Name` | Required |
| `ClassId` | Required; stable GUID is the plugin's responsibility |
| `Vendor` | `"Unknown"` |
| `Url` / `Email` | `""` |
| `Category` | `"Audio Module Class"` |
| `SubCategories` | `"Fx"` |
| `Version` | `"1.0.0"` |
| `Parameters` | `null` |
| `CreateProcessor` | `null` |
| `Kind` | `Effect` |
| `ViewFactory` | `null` |
| `CreatePlugin` | `null` |

| `ParameterDescriptor` | Default |
| --- | --- |
| `Id` | Required |
| `Title` | Required |
| `ShortTitle` / `Units` | `""` |
| `Min` / `Max` / `Default` | `0` / `1.0` / `0` |
| `StepCount` | `0` |
| `Flags` | `ParameterFlags.CanAutomate` |

`AudioProcessor` is the lower-level DSP base class:

| Member | Signature |
| --- | --- |
| `Parameters` | `ParameterCollection` |
| `SampleRate` | `double` |
| `MaxBlockSize` | `int` |
| `SetupProcessing` | `virtual void SetupProcessing(double sampleRate, int maxBlockSize)` |
| `SetActive` | `virtual void SetActive(bool active)` |
| `Terminate` | `virtual void Terminate()` |
| `Process` | `abstract void Process(in ProcessContext context)` |

`ProcessContext` exposes `AudioBusBuffers*`, `Event*`, `InputBusCount`, `OutputBusCount`, and `EventCount` directly, plus `Input(int)` / `Output(int)` returning `Vst3.Plugin.AudioBuffer`. The `AudioBuffer` in this namespace provides only `ChannelCount` and an indexer; it has no `Channels`.

`IPluginViewFactory.CreateView(PluginViewContext context)` returns an arbitrary `IPlugView*`; window, input, and rendering are handled by the implementation.
