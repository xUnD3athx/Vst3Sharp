# 已知缺口

## 音频处理

- 仅处理 32-bit float；`getLatencySamples` 和 `getTailSamples` 固定返回 0。
- 不支持采样级自动化，自动化按音频块生效。
- 事件仅解析 note on/off，不包含 MIDI CC 等语义。
- `getParamStringByValue` 仅返回数字，单位由宿主显示。

## 线程与内存

- 编辑器回调与音频线程之间未做同步。
- 播放路径不分配，但 GC 不是硬实时；长时间运行与 UI 压力测试尚未完成。
- 每个插件实例包含独立的 .NET 运行时，内存占用高于等价的 C++ 插件。

## 编辑器

- 默认使用随包 Noto Sans（拉丁）与 Noto Sans SC 子集（中文，按 `GlyphRange` 合并）；仍可通过 `EditorFont` 换成任意 TTF/OTF/TTC 或关闭随包字体回到位图字体。文本颜色沿用字体图集原色，无 DPI 缩放与主题。
- 中文字集只覆盖 ImGui `ChineseSimplified` 的 2500 常用字，不含韩文与全部 CJK 扩展字。
- IME 未实现。

## 平台与工具链

- 仅支持 Windows x64。
- 无 NuGet 包与 `dotnet new` 模板，只能通过项目引用使用。
- 无独立托管 API（仅有测试宿主）、无预设格式。
- Cubase / Nuendo 未测试。
