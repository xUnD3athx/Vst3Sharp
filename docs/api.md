# 插件 API

插件作者用 `Vst3` 命名空间。`Vst3.Plugin` 放底层实现和逃生口，`Vst3.Editor` 是编辑器。`Vst3.Interop` 是原始 ABI，只在绕过整个框架、自己实现 VST3 接口时才需要。

## 生命周期

```
new Plugin() → OnSetup(rate, block) → OnActivated(true) → Process × N → OnActivated(false) → OnTerminate()
```

一个 VST3 实例对应一个插件对象，构造发生在宿主创建组件时。`OnSetup` 对应 `setupProcessing`，`OnActivated` 对应 `setActive`，`OnTerminate` 在销毁前调用。

`Process` 在音频线程执行，不能分配、加锁或做 IO。其余回调和编辑器回调在宿主主线程。

## AudioPlugin

```csharp
protected abstract void Process(AudioContext context);
public virtual void OnSetup(double sampleRate, int maxBlockSize);
public virtual void OnActivated(bool active);
public virtual void OnTerminate();
protected ParameterCollection Parameters { get; }
```

`DeclareParameters` 和 `BindParameters` 由生成器补齐，不要手写。`AudioContext` 是 `ref struct`，只在本次 `Process` 内有效。

## AudioContext

```csharp
int SampleCount { get; }
AudioBuffer Input(int bus);
AudioBuffer Output(int bus);
ReadOnlySpan<MidiEvent> Events { get; }
```

总线布局由 `PluginKind` 固定：

| Kind | 音频输入 | 音频输出 | 事件输入 |
| --- | --- | --- | --- |
| `Effect` | 0 Input、1 Sidechain（均为立体声，Sidechain 是 Aux，默认不激活） | 0 Output | 无 |
| `Instrument` | 无 | 0 Output | 一个 MIDI 输入 |

总线不存在或索引越界时返回空 buffer（`ChannelCount == 0`），不需要判空。

`Events` 保留原始 `SampleOffset`。框架不按偏移切分音频块，事件在块内何时处理由 `Process` 决定；两个 sample 都在块首处理所有事件。

## AudioBuffer / AudioChannel

```csharp
int ChannelCount { get; }
Span<float> this[int channel] { get; }
ChannelList Channels { get; }   // foreach 遍历，元素是 AudioChannel
```

`AudioChannel` 提供 `Index`、`Length`、`Samples`（`Span<float>`）和 `this[int]`。音频固定为 32-bit float。

## MidiEvent

```csharp
MidiEventKind Kind;   // Other / NoteOn / NoteOff
int SampleOffset;
int Channel;
int Pitch;            // 0–127
float Velocity;       // 0–1
bool IsNoteOn, IsNoteOff;
```

只解析 note on/off。其他事件以 `Other` 出现，`Pitch` 和 `Velocity` 为 0。

## FloatParameter

```csharp
double Normalized { get; set; }   // 0–1，自动化和宿主读写的值
double Value { get; set; }        // 工程值，按 Min/Max 映射
float Linear { get; }             // (float)Value
```

字段初始化时创建，生成器在构造后绑定；绑定前读到的是 0。

## 参数

- 参数 ID 就是字段的声明顺序（0、1、…）。发布后调整字段顺序，旧工程和预设会错位。
- `Steps` 只是元数据，不做量化；`Unit` 只用于宿主显示。
- 自动化按块生效：每个参数只取队列里的最后一个点，在 `Process` 之前写入，不是采样级。
- 状态只保存规范化参数值（magic + 参数个数 + 每个 double）。参数个数变化后，旧状态会被拒绝（`setState` 返回 false）。
- UI 和自动化直接写参数数组，没有加锁。

`Parameters` 的类型是 `ParameterCollection`，可以在 DSP 里按索引或 ID 访问：

```csharp
int Count;
ParameterDescriptor GetDescriptor(int index);
int IndexOf(uint id);
double GetNormalized(int index);           // 也有 (uint id) 重载
void SetNormalized(int index, double value);
double ToPlain(int index, double normalized);
double ToNormalized(int index, double plain);
```

## 特性

`[VstPlugin(name)]`：`Vendor`、`Version`、`Url`、`Email`、`SubCategories`、`Kind`。

`[Parameter(name)]`：`ShortTitle`、`Unit`、`Min`、`Max`、`Default`、`Steps`、`CanAutomate`。

类必须标 `partial`；一个程序集只能有一个 `[VstPlugin]` 类；参数字段必须是实例上的 `FloatParameter`。

生成器的诊断：

| ID | 原因 |
| --- | --- |
| VST3001 | 程序集里有多个 `[VstPlugin]` |
| VST3002 | 类没有继承 `AudioPlugin` |
| VST3003 | `[Parameter]` 字段不是 `FloatParameter` |
| VST3004 | 类没有标 `partial` |
| VST3005 | 泛型类或嵌套类 |
| VST3006 | `[Parameter]` 字段是 static |

## 生成的模块代码

`<类名>.Vst3.g.cs` 里包含描述符和 `Vst3Module`：

- 类 GUID 由 `SHA-256("Vst3Sharp/" + Vendor + "/" + Name)` 派生，`Vendor` 或 `Name` 改了 GUID 就变。
- `SubCategories` 未指定时，`Effect` 用 `Fx`，`Instrument` 用 `Instrument|Synth`。
- 模块导出 `GetPluginFactory`、`InitDll`、`ExitDll`。
- 引用了 `Vst3.Editor.ImGui` 时自动设置 `ViewFactory`，否则插件没有编辑器。

## 逃生口：手写 PluginDescriptor

不用生成器时，可以自己构造 `PluginDescriptor`，但模块导出、`[UnmanagedCallersOnly]` 和 `Vst3Plugin.Configure` 都要自己写：

```csharp
var descriptor = new PluginDescriptor(
    Name: "Gain",
    ClassId: guid,                     // 自己保证稳定
    Vendor: "Acme",
    Kind: PluginKind.Effect,
    Parameters: new[] { new ParameterDescriptor(0, "Gain", "Gain", "dB", -24, 24, 0) },
    CreatePlugin: static () => new GainPlugin());
```

`Vst3.Plugin.AudioProcessor` 是更底层的 DSP 基类，`Process(in ProcessContext)` 直接拿到 `AudioBusBuffers*` 和 `Event*`。这个命名空间下的 `AudioBuffer` 只有 `ChannelCount` 和索引器，没有 `Channels` 枚举。

## 编辑器

```csharp
public interface IPluginViewFactory
{
    nint CreateView(PluginViewContext context);   // 返回 IPlugView*
}

public sealed class PluginViewContext
{
    ParameterCollection Parameters;
    Action<uint, double> SetParameter;            // 写参数并通知宿主（performEdit）
    Action<uint> BeginEdit, EndEdit;              // 拖动开始/结束
    int Width = 520, Height = 320;
}
```

自定义编辑器实现 `IPluginViewFactory` 并通过 `PluginDescriptor.ViewFactory` 接入。默认实现是 `Vst3.Editor.ImGuiEditorFactory`，把所有参数画成滑杆。

## 限制

- 只处理 32-bit float，`getLatencySamples` 和 `getTailSamples` 固定返回 0。
- 没有采样级自动化，没有 MIDI CC 等事件语义。
- `getParamStringByValue` 只返回数字，单位由宿主显示。
- 编辑器回调和音频线程之间没有同步。
