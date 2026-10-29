# Vst3Sharp

用 C# 编写与宿主 VST3 插件的最小互操作层（Windows x64 / net10.0 / NativeAOT）。

状态：S0（可行性验证）——AOT DLL 导出 `GetPluginFactory`，C# 控制台宿主加载并枚举插件类。

## 结构

```
src/Vst3.Interop      ABI 基础：TUID、工厂结构体、vtable 读取、结果码
src/Vst3.Plugin       插件侧：最小工厂 + 模块导出
samples/Plugin.Passthrough   示例插件（AOT 共享库）
samples/Host.Manual          控制台宿主（加载并枚举）
```

## 快速开始

```bash
dotnet build Vst3Sharp.sln

dotnet publish samples/Plugin.Passthrough -c Release -r win-x64
dotnet run --project samples/Host.Manual -c Release -- \
  samples/Plugin.Passthrough/bin/Release/net10.0/win-x64/publish/Plugin.Passthrough.dll
```

预期输出：工厂 vendor、类数量、类名与类 ID。

## 设计文档

见 [docs/vst3-csharp-plan.md](docs/vst3-csharp-plan.md)。
