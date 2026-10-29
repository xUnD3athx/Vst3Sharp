# Vst3Sharp

用 C# 编写与宿主 VST3 插件的最小互操作层（Windows x64 / net10.0 / NativeAOT）。

状态：S4 —— 作者 API（参数与 DSP 由插件自己声明/实现）、合并式组件、立体声、MIDI 事件、Sidechain、状态存取、自动化输入、`.vst3` bundle 打包。

## 结构

```
src/Vst3.Interop      ABI 基础：TUID、工厂/总线/处理结构体、vtable 读取、结果码
src/Vst3.Plugin       插件侧：工厂（含 IPluginFactory2）、合并式组件、模块导出
samples/Plugin.Passthrough   示例插件（AOT 共享库 + .vst3 bundle）
samples/Host.Manual          控制台宿主（枚举 + 实例化 + 离线处理）
```

## 快速开始

```bash
dotnet build Vst3Sharp.slnx

dotnet publish samples/Plugin.Passthrough -c Release -r win-x64
dotnet run --project samples/Host.Manual -c Release -- \
  samples/Plugin.Passthrough/bin/Release/net10.0/win-x64/publish/Passthrough.vst3/Contents/x86_64-win/Passthrough.vst3
```

预期输出包含 `audio in=2`、`input[1] ... name="Sidechain"`、`parameterCount -> 2`、五个音频块 `PASS`，以及 `state roundtrip ... PASS`。

## 在 FL Studio 里测试

把整个 `Passthrough.vst3` 目录复制到：

```
C:\Program Files\Common Files\VST3\
```

然后在 FL Studio 里重新扫描（Options → Manage plugins → Verify plugins）。

- **Gain**：插件参数面板里拉到 0 应静音，拉回 1.0 恢复。
- **SC Amount**：默认 0，插件保持直通（即使宿主送入 sidechain 缓冲也不压）。调到 80% 后，把另一条轨 Send 到 `Sidechain`，主信号才会被压低。
- **MIDI**：FL 对效果器的 MIDI 路由支持有限；音符门控已在 `Host.Manual` 里验证（note-on velocity 缩放增益，note-off 恢复）。

## 作者 API

- `PluginDescriptor`：类信息 + 参数表 + `CreateProcessor`
- `AudioProcessor.Process(in ProcessContext)`：作者自己的 DSP
- `ParameterCollection`：归一化参数读写、plain/normalized 转换
- 框架只负责 ABI/vtable、参数查询与自动化写入、事件解析、参数状态序列化，不包含任何业务 DSP

## 尚未实现

编辑器（`IPlugView`）。

## 设计文档

见 [docs/vst3-csharp-plan.md](docs/vst3-csharp-plan.md)。
