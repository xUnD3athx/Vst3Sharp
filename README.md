# Vst3Sharp

用 C# 编写与宿主 VST3 插件的最小互操作层（Windows x64 / net10.0 / NativeAOT）。

状态：S5 —— Effect 与 Instrument 两类插件、通用作者 API、MIDI 事件、Sidechain、状态存取、自动化输入、`.vst3` bundle 打包。

## 结构

```
src/Vst3.Interop               ABI 基础：TUID、结构体、vtable、结果码
src/Vst3.Plugin                框架：工厂、合并式组件、参数/状态/自动化/事件、作者 API
samples/Plugin.Passthrough     Effect 示例：Gain + SC Amount + Sidechain
samples/Plugin.SineSynth       Instrument 示例：16 音正弦合成器（验证 MIDI）
samples/Host.Manual            控制台宿主：effect 与 synth 两条离线测试路径
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
```

## 在 FL Studio 里测试

把两个 `*.vst3` 目录复制到：

```
C:\Program Files\Common Files\VST3\
```

然后在 FL Studio 里重新扫描（Options → Manage plugins → Verify plugins）。

- **Passthrough**（效果器）：Gain 拉到 0 应静音；SC Amount 默认 0（直通），调到 80% 并把另一条轨 Send 到 `Sidechain` 才会压低。effect 没有 MIDI 输入。
- **SineSynth**（乐器）：在通道机架里作为乐器加载，弹 MIDI 键盘或画钢琴卷帘，应该听到正弦波；Level 控制音量。

## 作者 API

```csharp
static readonly PluginDescriptor Descriptor = new(
    Name: "SineSynth", ClassId: ..., Kind: PluginKind.Instrument,
    Parameters: new ParameterDescriptor[] { new(0, "Level", "Level", "", 0, 1, 0.5) },
    CreateProcessor: static () => new SineSynthProcessor());

internal sealed class SineSynthProcessor : AudioProcessor
{
    public override void Process(in ProcessContext ctx)
    {
        var output = ctx.Output(0);
        // 读 ctx.Events / ctx.SampleCount，写 output[ch]
    }
}
```

框架负责：ABI/vtable、总线布局（Effect / Instrument）、参数查询与自动化、事件解析、参数状态序列化。业务 DSP 全部在插件里。

## 尚未实现

编辑器（`IPlugView`）。

## 设计文档

见 [docs/vst3-csharp-plan.md](docs/vst3-csharp-plan.md)。
