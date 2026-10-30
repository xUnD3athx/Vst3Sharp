# 插件 API

[English](api.md) | **简体中文**

## 目录

- [命名空间](#命名空间)
- [类型一览](#类型一览)
- [Vst3](#vst3)
  - [AudioPlugin](#audioplugin)
  - [AudioContext](#audiocontext)
  - [AudioBuffer / AudioChannel](#audiobuffer--audiochannel)
  - [FloatParameter](#floatparameter)
  - [MidiEvent](#midievent)
  - [特性](#特性)
- [总线布局](#总线布局)
- [参数行为](#参数行为)
  - [ParameterCollection](#parametercollection)
- [生成器](#生成器)
- [编辑器](#编辑器)
  - [EditorView](#editorview)
  - [UiCanvas](#uicanvas)
  - [输入](#输入)
- [底层接口](#底层接口)
- [限制](#限制)

## 命名空间

| 命名空间 | 内容 | 使用场景 |
| --- | --- | --- |
| `Vst3` | 作者 API：`AudioPlugin`、`AudioContext`、`FloatParameter`、`MidiEvent`、特性 | 编写插件 |
| `Vst3.Plugin` | 框架实现：`AudioProcessor`、`PluginDescriptor`、`ParameterCollection` | 绕过生成器，或直接使用底层基类 |
| `Vst3.Editor` | 编辑器：`EditorView`、`UiCanvas`、输入类型（`Vst3.Editor.ImGui` 包） | 实现自定义界面 |
| `Vst3.Interop` | 原始 VST3 ABI：结构体、vtable、结果码 | 直接实现 VST3 接口 |

## 类型一览

| 类型 | 命名空间 | 用途 |
| --- | --- | --- |
| `AudioPlugin` | `Vst3` | 插件基类 |
| `AudioContext` | `Vst3` | 一个音频块 |
| `AudioBuffer`、`AudioChannel` | `Vst3` | 总线与声道 |
| `FloatParameter` | `Vst3` | 参数字段句柄 |
| `MidiEvent`、`MidiEventKind` | `Vst3` | MIDI 事件 |
| `PluginKind` | `Vst3` | `Effect` / `Instrument` |
| `VstPluginAttribute`、`ParameterAttribute` | `Vst3` | 生成器输入 |
| `EditorView` | `Vst3.Editor` | 编辑器基类 |
| `UiCanvas`、`UiColor` | `Vst3.Editor` | 绘制 |
| `UiInput`、`UiPointerEvent`、`UiWheelEvent`、`UiKeyEvent` | `Vst3.Editor` | 输入 |
| `UiKey`、`UiMouseButton`、`UiModifiers`、`UiPointerAction` | `Vst3.Editor` | 输入枚举 |
| `AudioProcessor`、`ProcessContext` | `Vst3.Plugin` | 底层 DSP 基类 |
| `PluginDescriptor`、`ParameterDescriptor` | `Vst3.Plugin` | 手动构造的描述符 |
| `ParameterCollection` | `Vst3.Plugin` | 参数存储 |
| `PluginViewContext`、`IPluginViewFactory` | `Vst3.Plugin` | 自定义视图工厂 |
| `Vst3Plugin` | `Vst3.Plugin` | 模块入口 |

## Vst3

### AudioPlugin

| 成员 | 签名 | 说明 |
| --- | --- | --- |
| `Process` | `protected abstract void Process(AudioContext context)` | 抽象方法；在音频线程执行 |
| `OnSetup` | `virtual void OnSetup(double sampleRate, int maxBlockSize)` | 对应 `setupProcessing` |
| `OnActivated` | `virtual void OnActivated(bool active)` | 对应 `setActive` |
| `OnTerminate` | `virtual void OnTerminate()` | 销毁前调用 |
| `Parameters` | `protected ParameterCollection Parameters { get; }` | 本实例的参数 |
| `DeclareParameters` | `virtual IReadOnlyList<ParameterDescriptor>` | 由生成器实现 |
| `BindParameters` | `virtual void BindParameters()` | 由生成器实现 |

一个 VST3 实例对应一个插件对象。除 `Process` 外，其余回调均在宿主主线程执行。

### AudioContext

| 成员 | 签名 | 说明 |
| --- | --- | --- |
| `SampleCount` | `int` | 本块采样数 |
| `Input` | `AudioBuffer Input(int bus)` | 音频输入总线 |
| `Output` | `AudioBuffer Output(int bus)` | 音频输出总线 |
| `Events` | `ReadOnlySpan<MidiEvent>` | 本块事件，保留原始 `SampleOffset` |

`ref struct`，仅在本次 `Process` 内有效。总线不存在或索引越界时返回 `ChannelCount == 0` 的空 buffer。框架不按事件偏移切分音频块，事件的处理时机由 `Process` 决定。

### AudioBuffer / AudioChannel

| `AudioBuffer` | 说明 |
| --- | --- |
| `ChannelCount` | 声道数 |
| `this[int channel]` | `Span<float>`，越界返回空 |
| `Channels` | 可 `foreach` 遍历，元素为 `AudioChannel` |

| `AudioChannel` | 说明 |
| --- | --- |
| `Index` | 声道索引 |
| `Length` | 采样数 |
| `Samples` | `Span<float>` |
| `this[int i]` | 读写单个采样 |

音频固定为 32-bit float。

### FloatParameter

| 成员 | 签名 | 说明 |
| --- | --- | --- |
| `Normalized` | `double` | 0–1，供自动化与宿主读写 |
| `Value` | `double` | 工程值，按 `Min`/`Max` 映射 |
| `Linear` | `float` | `(float)Value`，用于乘系数 |

字段初始化时创建，生成器在构造后绑定；绑定前读取结果为 0。

### MidiEvent

| 成员 | 签名 | 说明 |
| --- | --- | --- |
| `Kind` | `MidiEventKind` | `Other` / `NoteOn` / `NoteOff` |
| `SampleOffset` | `int` | 块内偏移 |
| `Channel` | `int` | MIDI 声道 |
| `Pitch` | `int` | 0–127 |
| `Velocity` | `float` | 0–1 |
| `IsNoteOn` / `IsNoteOff` | `bool` | 对 `Kind` 的便捷判断 |

仅解析 note on/off；其余事件为 `Other`，`Pitch` 和 `Velocity` 为 0。

### 特性

`[VstPlugin(name)]`：

| 字段 | 说明 | 默认值 |
| --- | --- | --- |
| `Vendor` | 开发商 | `"Unknown"` |
| `Version` | 版本 | `"1.0.0"` |
| `Url` | 网址 | `""` |
| `Email` | 联系邮箱 | `""` |
| `SubCategories` | 分类 | Effect 为 `Fx`，Instrument 为 `Instrument\|Synth` |
| `Kind` | 插件类型 | `PluginKind.Effect` |
| `Editor` | 自定义 `EditorView` 类型 | `null`（默认面板） |

`[Parameter(name)]`：

| 字段 | 说明 | 默认值 |
| --- | --- | --- |
| `ShortTitle` | 短标题 | `null`（使用名称） |
| `Unit` | 单位，仅显示 | `""` |
| `Min` | 最小值 | `0` |
| `Max` | 最大值 | `1.0` |
| `Default` | 默认值 | `0` |
| `Steps` | 步进数，仅元数据 | `0`（连续） |
| `CanAutomate` | 是否可自动化 | `true` |

生成器处理 `partial`、非泛型、非嵌套类；每个程序集只暴露一个 `[VstPlugin]` 类；`[Parameter]` 字段为实例上的 `FloatParameter`。

## 总线布局

| `Kind` | 音频输入 | 音频输出 | 事件输入 |
| --- | --- | --- | --- |
| `Effect` | 0 Input、1 Sidechain（立体声，Aux，默认不激活） | 0 Output（立体声） | 无 |
| `Instrument` | 无 | 0 Output（立体声） | 0 MIDI In（16 声道） |

仅支持立体声排列。

## 参数行为

- 参数 ID 按字段声明顺序分配（0、1、…）。发布后调整字段顺序将导致旧工程的参数错位。
- `Steps` 与 `Unit` 仅影响宿主显示，不参与量化。
- 自动化按音频块生效：每个参数仅取队列中的最后一个点，并在 `Process` 之前写入。
- 状态仅保存规范化参数值（magic + 参数个数 + 每个参数一个 double）。参数个数变化后，旧状态将被拒绝，`setState` 返回 false。
- UI 与自动化直接写入参数数组，未加锁。

### ParameterCollection

| 成员 | 签名 |
| --- | --- |
| `Count` | `int` |
| `GetDescriptor` | `ParameterDescriptor GetDescriptor(int index)` |
| `IndexOf` | `int IndexOf(uint id)` |
| `GetNormalized` | `double GetNormalized(int index)` / `(uint id)` |
| `SetNormalized` | `void SetNormalized(int index, double value)` / `(uint id, double)` |
| `ToPlain` | `double ToPlain(int index, double normalized)` |
| `ToNormalized` | `double ToNormalized(int index, double plain)` |

## 生成器

生成器为 `partial` 类生成 `DeclareParameters`、`BindParameters`，并输出 `<类名>.Vst3.g.cs`：

- `Vst3Module` 导出 `GetPluginFactory`、`InitDll`、`ExitDll`。
- Class GUID 取 `SHA-256("Vst3Sharp/" + Vendor + "/" + Name)` 的前 16 字节；修改 `Vendor` 或 `Name` 将改变 GUID。
- 引用 `Vst3.Editor.ImGui` 时自动挂载默认面板；指定 `Editor = typeof(...)` 时改用自定义编辑器。
- 参数 ID 按字段声明顺序分配。

诊断：

| ID | 原因 |
| --- | --- |
| VST3001 | 程序集中存在多个 `[VstPlugin]` |
| VST3002 | 类未继承 `AudioPlugin` |
| VST3003 | `[Parameter]` 字段不是 `FloatParameter` |
| VST3004 | 类未标记 `partial` |
| VST3005 | 泛型类或嵌套类 |
| VST3006 | `[Parameter]` 字段是 static |
| VST3007 | `Editor` 指定的类型未继承 `EditorView` |
| VST3008 | `Editor` 指定的类型缺少公共无参构造 |

## 编辑器

未指定 `Editor` 时使用默认面板：每个参数一行，包含标题、滑杆与工程值，并支持滚轮调整。

自定义编辑器继承 `EditorView`，并通过 `[VstPlugin]` 指定：

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

    public override void OnPointerDown(in UiPointerEvent e) { /* 命中测试 */ }
}
```

### EditorView

| 成员 | 签名 |
| --- | --- |
| `Context` | `PluginViewContext` |
| `Input` | `UiInput` |
| `Width` / `Height` | `int` |
| `Parameters` | `protected ParameterCollection` |
| `SetParameter` | `protected void SetParameter(uint id, double normalized)`，同时通知宿主 |
| `BeginEdit` / `EndEdit` | `protected void BeginEdit(uint id)` / `EndEdit(uint id)` |
| `OnAttach` / `OnDetach` | `protected internal virtual` |
| `OnRender` | `virtual void OnRender(UiCanvas canvas)` |
| `OnPointerDown` / `OnPointerUp` / `OnPointerMove` | `virtual void ... (in UiPointerEvent e)` |
| `OnWheel` | `virtual void OnWheel(in UiWheelEvent e)` |
| `OnKey` | `virtual void OnKey(in UiKeyEvent e)` |
| `OnText` | `virtual void OnText(char c)` |
| `OnFocusChanged` | `virtual void OnFocusChanged(bool focused)` |
| `OnResize` | `virtual void OnResize(int width, int height)` |

### UiCanvas

仅在 `OnRender` 内有效，坐标为视区物理像素。

| 方法 | 签名 |
| --- | --- |
| `Width` / `Height` | `float` |
| `Rect` | `Rect(x, y, width, height, UiColor)` |
| `RoundRect` | `RoundRect(x, y, width, height, radius, UiColor)` |
| `RectOutline` | `RectOutline(x, y, width, height, UiColor, thickness = 1)` |
| `Circle` | `Circle(centerX, centerY, radius, UiColor, filled = true)` |
| `Line` | `Line(x1, y1, x2, y2, UiColor, thickness = 1)` |
| `Text` | `Text(x, y, string, UiColor, size = 0)`，`size` 为 0 时使用默认字号 |
| `MeasureText` | `Vector2 MeasureText(string, size = 0)` |
| `PushClip` / `PopClip` | `PushClip(x, y, width, height)` / `PopClip()` |

### 输入

| `UiPointerEvent` | 说明 |
| --- | --- |
| `Action` | `UiPointerAction.Move` / `Down` / `Up` |
| `Button` | 发生变化的按键；Move 事件为 `None` |
| `X` / `Y` | 视区坐标 |
| `Modifiers` | `UiModifiers` 位标志 |
| `Position` | `Vector2` |

`UiWheelEvent`：`X`、`Y`、`Delta`（1 为一格向上）、`Modifiers`、`Position`。

`UiKeyEvent`：`Key`（`UiKey`）、`VirtualKey`、`IsDown`、`IsRepeat`、`Modifiers`。

`UiInput`：`MouseX`、`MouseY`、`MousePosition`、`Modifiers`、`WheelDelta`、`IsDown(UiMouseButton)`。

框架在按下至抬起期间保持鼠标捕获，拖出窗口后仍可接收 Move 事件。Move 事件不包含按键状态，按键状态通过 `Input.IsDown` 查询。框架不提供控件树，命中测试、布局与焦点管理由插件作者负责。键盘仅映射常用键，IME 尚未实现。

## 底层接口

不使用生成器时，`PluginDescriptor` 由插件自行构造，模块导出、`[UnmanagedCallersOnly]` 与 `Vst3Plugin.Configure` 也由插件提供。

| `PluginDescriptor` | 默认值 |
| --- | --- |
| `Name` | 必填 |
| `ClassId` | 必填；GUID 由插件保证稳定 |
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

| `ParameterDescriptor` | 默认值 |
| --- | --- |
| `Id` | 必填 |
| `Title` | 必填 |
| `ShortTitle` / `Units` | `""` |
| `Min` / `Max` / `Default` | `0` / `1.0` / `0` |
| `StepCount` | `0` |
| `Flags` | `ParameterFlags.CanAutomate` |

`AudioProcessor` 是更底层的 DSP 基类：

| 成员 | 签名 |
| --- | --- |
| `Parameters` | `ParameterCollection` |
| `SampleRate` | `double` |
| `MaxBlockSize` | `int` |
| `SetupProcessing` | `virtual void SetupProcessing(double sampleRate, int maxBlockSize)` |
| `SetActive` | `virtual void SetActive(bool active)` |
| `Terminate` | `virtual void Terminate()` |
| `Process` | `abstract void Process(in ProcessContext context)` |

`ProcessContext` 直接暴露 `AudioBusBuffers*`、`Event*`、`InputBusCount`、`OutputBusCount`、`EventCount`，另有 `Input(int)` / `Output(int)` 返回 `Vst3.Plugin.AudioBuffer`。该命名空间下的 `AudioBuffer` 仅提供 `ChannelCount` 和索引器，不提供 `Channels`。

`IPluginViewFactory.CreateView(PluginViewContext context)` 返回任意 `IPlugView*`；该方式下的窗口、输入与渲染由实现方处理。

## 限制

- 仅处理 32-bit float；`getLatencySamples` 和 `getTailSamples` 固定返回 0。
- 不支持采样级自动化；事件仅解析 note on/off，不包含 MIDI CC 等语义。
- `getParamStringByValue` 仅返回数字，单位由宿主显示。
- 编辑器回调与音频线程之间未做同步。
- 仅支持 Windows x64；每个实例包含独立的 .NET 运行时。
