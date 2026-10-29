# Vst3Sharp

[English](../README.md) | **简体中文**

用 C# 写 VST3 插件。NativeAOT 直接编译成原生 DLL，DAW 加载的是插件，不是 .NET 运行时；不需要 C++、CMake，也不用手写 vtable。

Windows x64 · .NET 10 · 预发布（S7）

示例覆盖了效果器、乐器、参数与自动化、状态保存、MIDI、Sidechain 和 ImGui 编辑器。FL Studio 和 Bitwig 都能加载，VST3 validator 47/47。还没有 NuGet 包和 `dotnet new` 模板，目前通过源码 + 项目引用使用。

## 构建

```bash
dotnet build Vst3Sharp.slnx
dotnet publish samples/Plugin.Passthrough -c Release -r win-x64
dotnet publish samples/Plugin.SineSynth   -c Release -r win-x64
```

NativeAOT 需要 MSVC linker 和 Windows 10 SDK（VS 2022 Build Tools 的“使用 C++ 的桌面开发”）。

## 离线测试

不需要 DAW。第一条测效果器，`synth` 测乐器，`editor` 渲染一帧并输出 PNG。

```bash
dotnet run --project samples/Host.Manual -c Release -- \
  samples/Plugin.Passthrough/bin/Release/net10.0/win-x64/publish/Passthrough.vst3/Contents/x86_64-win/Passthrough.vst3

dotnet run --project samples/Host.Manual -c Release -- synth \
  samples/Plugin.SineSynth/bin/Release/net10.0/win-x64/publish/SineSynth.vst3/Contents/x86_64-win/SineSynth.vst3

dotnet run --project samples/Host.Manual -c Release -- editor \
  samples/Plugin.Passthrough/bin/Release/net10.0/win-x64/publish/Passthrough.vst3/Contents/x86_64-win/Passthrough.vst3
```

## 一个插件

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

类必须标 `partial`：生成器要把参数绑定写进同一个类型。它还会根据 `Vendor/Name` 派生稳定的类 GUID，并生成 `GetPluginFactory`、`InitDll`、`ExitDll` 三个导出。项目引用 `Vst3.Editor.ImGui` 时自动挂上通用编辑器。

`[VstPlugin]` 接受 `Vendor`、`Version`、`Url`、`Email`、`SubCategories`、`Kind`（`Effect` 或 `Instrument`）。`[Parameter]` 接受 `ShortTitle`、`Unit`、`Min`、`Max`、`Default`、`Steps`、`CanAutomate`。参数必须是实例字段，类型为 `FloatParameter`，可读 `Normalized`（0–1）、`Value` 和 `Linear`。

## 目录

```
src/Vst3.Interop              ABI：TUID、结构体、vtable、结果码
src/Vst3.Plugin               框架：工厂、合并式组件、参数/状态/事件、作者 API
src/Vst3.Plugin.Generator     源生成器：特性 → 参数、类 GUID、模块导出
src/Vst3.Editor.ImGui         IPlugView + ImGui.NET + Skia 软件渲染 + GDI 贴图
samples/Plugin.Passthrough    效果器：Gain、SC Amount、Sidechain
samples/Plugin.SineSynth      乐器：16 音正弦合成器
samples/Host.Manual           离线测试用的控制台宿主
```

## 装进 DAW

把发布出来的 `*.vst3` 目录复制到 `C:\Program Files\Common Files\VST3\`，重新扫描。Passthrough 是效果器；SineSynth 是乐器，响应 MIDI 音符。

## 还没做

- 只支持 Windows x64，音频只处理 32-bit float，没有 64-bit 处理。
- 每个实例各带一份 .NET 运行时，内存比同等的 C++ 插件高。
- GC 不是硬实时。播放期间零分配，但长跑和 UI 压力测试还没做。
- 编辑器用默认点阵字体、白色文本，没有 DPI 缩放、IME 和主题。
- 没有宿主 API（只有测试宿主）、预设格式、NuGet 包和模板。
- Cubase/Nuendo 未测。

## 文档

- [设计文档与路线图](vst3-csharp-plan.md)
- [English README](../README.md)
