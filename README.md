# Vst3Sharp

用 C# 编写与宿主 VST3 插件的最小互操作层（Windows x64 / net10.0 / NativeAOT）。

状态：S7 —— Effect 与 Instrument、作者 API v2（特性 + 源生成器）、MIDI、Sidechain、状态、自动化、ImGui 编辑器（Skia 软件渲染）、`.vst3` bundle。

## 结构

```
src/Vst3.Interop               ABI 基础：TUID、结构体、vtable、结果码
src/Vst3.Plugin                框架：工厂、合并式组件、参数/状态/自动化/事件、作者 API
src/Vst3.Plugin.Generator      源生成器：特性 → 参数表 + 类 GUID + 模块导出
src/Vst3.Editor.ImGui         编辑器：IPlugView + ImGui.NET + Skia 软件渲染 + GDI 贴图
samples/Plugin.Passthrough     Effect 示例：Gain + SC Amount + Sidechain
samples/Plugin.SineSynth       Instrument 示例：16 音正弦合成器（验证 MIDI）
samples/Host.Manual            控制台宿主：effect / synth / editor 三条离线测试路径
```

## 快速开始

```bash
dotnet build Vst3Sharp.slnx

dotnet publish samples/Plugin.Passthrough -c Release -r win-x64
dotnet publish samples/Plugin.SineSynth -c Release -r win-x64

# Effect：直通 / sidechain / 自动化 / 状态
dotnet run --project samples/Host.Manual -c Release -- \
  samples/Plugin.Passthrough/bin/Release/net10.0/win-x64/publish/Passthrough.vst3/Contents/x86_64-win/Passthrough.vst3

# Instrument：note-on 出声、note-off 释放
dotnet run --project samples/Host.Manual -c Release -- synth \
  samples/Plugin.SineSynth/bin/Release/net10.0/win-x64/publish/SineSynth.vst3/Contents/x86_64-win/SineSynth.vst3

# Editor：attach 到隐藏父窗口、渲染一帧并导出 PNG 快照
dotnet run --project samples/Host.Manual -c Release -- editor \
  samples/Plugin.Passthrough/bin/Release/net10.0/win-x64/publish/Passthrough.vst3/Contents/x86_64-win/Passthrough.vst3
```

## 在 FL Studio 里测试

把两个 `*.vst3` 目录复制到：

```
C:\Program Files\Common Files\VST3\
```

然后在 FL Studio 里重新扫描（Options → Manage plugins → Verify plugins）。

- **Passthrough**（效果器）：打开编辑器会看到 Gain / SC Amount 两个滑杆；Gain 拉到 0 应静音。effect 没有 MIDI 输入。
- **SineSynth**（乐器）：作为乐器加载，弹 MIDI 键盘或画钢琴卷帘，应该听到正弦波；Level 控制音量。

编辑器基于 ImGui + Skia 软件渲染，当前文本统一白色、使用默认像素字体，界面还会继续打磨。

## 尚未实现

自定义字体与文本颜色、DPI 缩放、预设文件格式、编辑器开关 100 次的内存回归。

## 作者 API

一个类 + 两个特性，不写 `unsafe`、`[UnmanagedCallersOnly]`、描述符和 GUID：

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
        foreach (var channel in output.Channels)
        {
            var input = context.Input[0][channel.Index];
            for (int i = 0; i < channel.Length; i++)
                channel[i] = input[i] * _gain.Linear;
        }
    }
}
```

`partial` 是唯一的额外要求：源生成器要在同一个类里补上 `DeclareParameters` / `BindParameters`。它还会根据 `Vendor/Name` 派生稳定的类 GUID，并生成 `GetPluginFactory` / `InitDll` / `ExitDll` 导出；引用了 `Vst3.Editor.ImGui` 时自动接上通用参数编辑器。

框架负责：ABI/vtable、总线布局（Effect / Instrument）、参数查询与自动化、事件解析、参数状态序列化。业务 DSP 全部在插件里。

## 设计文档

见 [docs/vst3-csharp-plan.md](docs/vst3-csharp-plan.md)。
