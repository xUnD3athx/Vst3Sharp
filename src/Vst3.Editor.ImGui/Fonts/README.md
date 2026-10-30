# Bundled editor fonts

These files are the default fonts for `Vst3.Editor.ImGui`. Both are licensed under the
SIL Open Font License 1.1 and may be redistributed with the plugin.

| File | Purpose | Upstream |
| --- | --- | --- |
| `NotoSans-Regular.ttf` | Latin, Greek, Cyrillic, Thai and Vietnamese UI text | [Noto Sans](https://github.com/googlefonts/noto-fonts), `hinted/ttf/NotoSans/NotoSans-Regular.ttf` |
| `NotoSansSC-Regular.ttf` | Simplified Chinese (common 2500 characters + CJK punctuation/kana) | [Noto Sans SC](https://github.com/google/fonts/tree/main/ofl/notosanssc), variable font instantiated at `wght=400` |

Both files are subsets: only the code points that `EditorGlyphRange` can request are kept.
Unicode coverage is therefore limited to the ImGui glyph ranges, and characters outside them
render as `?`.

- `NotoSans-Regular.ttf` covers Latin, Latin Extended, Greek, Cyrillic, Thai, Vietnamese and
  general punctuation.
- `NotoSansSC-Regular.ttf` covers ImGui's `ChineseSimplified` range: 0x0020-0x00FF,
  0x2000-0x206F, 0x3000-0x30FF, 0x31F0-0x31FF, 0xFF00-0xFFEF plus the 2500 common hanzi.
  It does not contain Hangul or the full CJK block.

## Rebuilding the subsets

```bash
pip install fonttools[woff]
# Noto Sans (static hinted TTF, full Latin/Greek/Cyrillic/Thai/Vietnamese)
python -m fontTools.subset NotoSans-Regular.ttf \
  --unicodes-file=latin-unicodes.txt --output-file=NotoSans-Regular.ttf \
  --no-hinting --name-IDs=0,1,2,3,4,5,6,13,14 --layout-features=kern,liga

# Noto Sans SC (variable) -> static 400 weight, then subset
python -m fontTools.varLib.instancer "NotoSansSC[wght].ttf" wght=400 -o NotoSansSC-Regular.ttf
python -m fontTools.subset NotoSansSC-Regular.ttf \
  --unicodes-file=cjk-unicodes.txt --output-file=NotoSansSC-Regular.ttf \
  --no-hinting --name-IDs=0,1,2,3,4,5,6,13,14 --layout-features=kern,liga
```

`cjk-unicodes.txt` is generated from ImGui's `ImFontAtlas::GetGlyphRangesChineseSimplifiedCommon()`
in `imgui_draw.cpp` (base ranges plus the 2500 accumulative hanzi offsets).

## Licensing

- `OFL-NotoSans.txt` — copyright Google LLC, SIL OFL 1.1.
- `OFL-NotoSansSC.txt` — copyright Adobe, SIL OFL 1.1, Reserved Font Name `Source`.

The Reserved Font Name for Noto Sans SC is `Source`, not `Noto`, so the subset keeps the
names `Noto Sans SC` / `NotoSansSC-Regular`. The subsets never use the reserved name.

Do not add system fonts (Segoe UI, Microsoft YaHei, ...) to this directory: they are not
redistributable.
