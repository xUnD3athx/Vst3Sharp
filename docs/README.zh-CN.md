# Vst3Sharp

[English](../README.md) | **简体中文**

Vst3Sharp让你能使用 C# 编写 VST3 插件。你可以使用 NativeAOT 直接编译原生 DLL，避免使用 .NET 运行时。你不需要编写任何 C++、CMake，以及 vtable。

# 环境

Windows x64 · .NET 10

## 构建

```bash
dotnet build Vst3Sharp.slnx
dotnet publish YourPlugin -c Release -r win-x64
```
## 编写范例

```csharp
using Vst3;     //调用Vst3Sharp

[VstPlugin("Gain", Vendor = "Acme", Version = "1.0.0")]
public sealed partial class GainPlugin : AudioPlugin    //创建Vst3插件
{
    [Parameter("Gain", Unit = "dB", Min = -24, Max = 24, Default = 0)]
    private readonly FloatParameter _gain = new();  //创建Parameter

    //音频处理逻辑
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
- 范例中使用到的特性
### VstPlugin
| 字段 | 描述 | 默认值 |
| --- | --- | --- |
| Vendor | 插件开发商 | `"Unknown"` |
| Version | 插件版本 | `"1.0.0"` |
| Url | 插件Url | `""` |
| Email | 联系邮箱 | `""` |
| SubCategories | 插件分类 | Effect 为 `Fx`，Instrument 为 `Instrument\|Synth` |
| Kind | 插件类型 ( Effect 或 Instrument ) | `Effect` |
### Parameter
| 字段 | 描述 | 默认值 |
| --- | --- | --- |
| ShortTitle | 短标题 | `null`（使用名称） |
| Unit | 参数单位 | `""` |
| Min | 参数最小值 | `0` |
| Max | 参数最大值 | `1.0` |
| Default | 参数默认值 | `0` |
| Steps | 参数步进值 | `0`（连续） |
| CanAutomate | 参数是否可自动化 | `true` |
## 文档

- [插件 API 参考](api.md)
