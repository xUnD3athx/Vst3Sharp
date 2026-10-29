# Vst3Sharp

[English](../README.md) | **简体中文**

用纯 C# 编写 VST3 插件的 SDK。插件经 NativeAOT 直接编译为原生 DLL，DAW 加载时不启动 CLR，也不需要 C++、CMake 或 vtable 代码。

平台：Windows x64 · 运行时：.NET 10

> **状态：预发布（S7）。** 功能已可完整使用，但尚未发布 NuGet 包与 `dotnet new` 模板，目前通过源码 + 项目引用接入。

## 能力概览

| 能力 | 状态 |
| --- | --- |
| Effect / Instrument 插件 | 支持 |
| 参数、自动化、状态保存 | 支持 |
| MIDI 事件、Sidechain | 支持 |
| 插件编辑器（ImGui + Skia 软件渲染） | 支持（基础版） |
| 宿主 API（`Vst3.Hosting`） | 未提供，目前只有离线测试宿主 |
| 兼容性 | FL Studio、Bitwig 实测通过，VST3 validator 47/47；Cubase 未测 |

## 环境要求

- Windows 10/11 x64
- .NET 10 SDK
- NativeAOT 链接需要 MSVC linker 与 Windows 10 SDK（安装 Visual Studio 2022 Build Tools 的"使用 C++ 的桌面开发"即可）

## 快速开始

```bash
# 1. 构建
dotnet build Vst3Sharp.slnx

# 2. 发布两个示例插件（NativeAOT）
dotnet publish samples/Plugin.Passthrough -c Release -r win-x64
dotnet publish samples/Plugin.SineSynth   -c Release -r win-x64

# 3. 离线验证（无需 DAW）
# Effect：直通 / Sidechain / 自动化 / 状态
dotnet run --project samples/Host.Manual -c Release -- \
  samples/Plugin.Passthrough/bin/Release/net10.0/win-x64/publish/Passthrough.vst3/Contents/x86_64-win/Passthrough.vst3

# Instrument：note-on 出声、note-off 释放
dotnet run --project samples/Host.Manual -c Release -- synth \
  samples/Plugin.SineSynth/bin/Release/net10.0/win-x64/publish/SineSynth.vst3/Contents/x86_64-win/SineSynth.vst3

# Editor：attach 到隐藏父窗口，渲染一帧并输出 PNG 快照
dotnet run --project samples/Host.Manual -c Release -- editor \
  samples/Plugin.Passthrough/bin/Release/net10.0/win-x64/publish/Passthrough.vst3/Contents/x86_64-win/Passthrough.vst3
```

发布产物是标准 `.vst3` bundle：

```
Passthrough.vst3/Contents/x86_64-win/Passthrough.vst3
```

## 编写插件

继承 `AudioPlugin`，用特性声明元数据与参数，实现 `Process` 即可：

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

作者代码中不需要 `unsafe`、`[UnmanagedCallersOnly]`、参数 ID、描述符或 GUID。类必须标记 `partial`：源生成器在同一类型内补齐参数绑定。

`[VstPlugin]` 生成的模块行为：

- 根据 `Vendor/Name` 的 SHA-256 派生稳定类 GUID
- 生成 `GetPluginFactory` / `InitDll` / `ExitDll` 导出
- 引用 `Vst3.Editor.ImGui` 时自动挂载通用参数编辑器；未引用则无编辑器，由宿主回退到通用 UI

`[VstPlugin]` 可选属性：`Vendor`、`Version`、`Url`、`Email`、`SubCategories`、`Kind`（`Effect` / `Instrument`，默认 `Effect`）。

`[Parameter]` 可选属性：

| 属性 | 说明 |
| --- | --- |
| `ShortTitle` | 短名称，默认同标题 |
| `Unit` | 单位（如 `dB`、`%`） |
| `Min` / `Max` / `Default` | 参数范围与默认值 |
| `Steps` | 离散步数，0 表示连续 |
| `CanAutomate` | 是否可自动化，默认 `true` |

参数字段应为实例字段（`private` 即可），类型为 `FloatParameter`，提供 `Normalized`（0–1）、`Value`（工程值）和 `Linear`（`float`）。

## 项目结构

```
src/Vst3.Interop              ABI 基础：TUID、结构体、vtable、结果码
src/Vst3.Plugin               框架：工厂、合并式组件、参数/状态/自动化/事件、作者 API
src/Vst3.Plugin.Generator     源生成器：特性 → 参数表、类 GUID、模块导出
src/Vst3.Editor.ImGui         编辑器：IPlugView + ImGui.NET + Skia 软件渲染 + GDI 贴图
samples/Plugin.Passthrough    Effect 示例：Gain、SC Amount、Sidechain
samples/Plugin.SineSynth      Instrument 示例：16 音正弦合成器（验证 MIDI）
samples/Host.Manual           控制台宿主：effect / synth / editor 三条离线测试路径
```

## 在 DAW 中加载

将发布目录下的 `*.vst3` 目录复制到：

```
C:\Program Files\Common Files\VST3\
```

然后重新扫描插件（FL Studio：Options → Manage plugins → Verify plugins）。

- **Passthrough**（效果器）：编辑器包含 Gain / SC Amount 两个滑杆；Gain 归零应静音。Effect 没有 MIDI 输入。
- **SineSynth**（乐器）：作为乐器加载，弹 MIDI 键盘或画钢琴卷帘应听到正弦波；Level 控制音量。

## 已知限制

- 仅支持 Windows x64；音频处理仅 32-bit float，未实现 VST3 64-bit 处理
- 每个插件实例各带一份 .NET 运行时，内存占用高于同等 C++ 插件
- GC 非硬实时：策略是播放期间零分配，长时间高负载与 UI 压力测试尚未完成
- 编辑器当前使用默认像素字体、白色文本，尚无 DPI 缩放、中文 IME 与主题
- 预设文件格式、`Vst3.Hosting` 宿主 API、NuGet 包与模板尚未提供
- Cubase / Nuendo 尚未实测；FL Studio 与 Bitwig 已通过

## 文档

- 设计决策与路线图：[docs/vst3-csharp-plan.md](vst3-csharp-plan.md)
