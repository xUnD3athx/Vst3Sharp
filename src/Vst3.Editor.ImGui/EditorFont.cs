using System.Buffers.Binary;
using ImGuiNET;

namespace Vst3.Editor;

/// <summary>Glyph ranges baked into the ImGui font atlas.</summary>
public enum EditorGlyphRange
{
    Default,
    Latin,
    Greek,
    Cyrillic,
    ChineseSimplified,
    ChineseFull,
    Japanese,
    Korean,
    Thai,
    Vietnamese,
}

/// <summary>
/// Process-wide TrueType/OpenType font selection for plugin editors.
/// Set this before the first editor is opened. By default the fonts bundled in
/// <c>Fonts/</c> next to the plugin module are used; a missing or invalid file falls back
/// to the built-in bitmap font. Relative paths resolve against the plugin module directory.
/// </summary>
public static class EditorFont
{
    /// <summary>Environment overrides used by the offline editor test.</summary>
    private const string FontPathVariable = "VST3SHARP_FONT";

    private const string FontRangeVariable = "VST3SHARP_FONT_RANGE";

    private const string FontFolderName = "Fonts";

    private const string BundledUiFont = "NotoSans-Regular.ttf";

    private const string BundledCjkFont = "NotoSansSC-Regular.ttf";

    public const float DefaultPixelSize = 16.0f;

    /// <summary>Path to a .ttf, .otf or .ttc file, absolute or relative to the plugin module.</summary>
    public static string? FilePath { get; set; }

    /// <summary>Face index inside a .ttc collection; 0 for single-face fonts.</summary>
    public static int FaceIndex { get; set; }

    /// <summary>Font size in pixels. The default panel and editor views scale from this size.</summary>
    public static float PixelSize { get; set; } = DefaultPixelSize;

    public static EditorGlyphRange GlyphRange { get; set; } = EditorGlyphRange.Default;

    /// <summary>
    /// Use the bundled Noto Sans/Noto Sans SC files when <see cref="FilePath"/> is not set.
    /// Disable to fall back to ImGui's built-in bitmap font.
    /// </summary>
    public static bool UseBundledFont { get; set; } = true;

    /// <summary>Selects a font file and resets the other options to the supplied values.</summary>
    public static void UseFile(
        string path,
        float pixelSize = DefaultPixelSize,
        EditorGlyphRange glyphRange = EditorGlyphRange.Default,
        int faceIndex = 0)
    {
        FilePath = path;
        PixelSize = pixelSize;
        GlyphRange = glyphRange;
        FaceIndex = faceIndex;
    }

    /// <summary>Restores the default configuration: bundled fonts, 16 px, default range.</summary>
    public static void Reset()
    {
        FilePath = null;
        FaceIndex = 0;
        PixelSize = DefaultPixelSize;
        GlyphRange = EditorGlyphRange.Default;
        UseBundledFont = true;
    }

    internal static unsafe bool TryAdd(ImFontAtlasPtr fonts)
    {
        EditorGlyphRange range = ResolveGlyphRange();
        float size = NormalizeSize(PixelSize);
        IntPtr ranges = SelectRanges(fonts, range);

        string? configured = ResolveConfiguredPath();
        string? primary = configured ?? (UseBundledFont ? FindBundledFont(BundledUiFont) : null);
        if (primary is null || !LooksLikeFontFile(primary))
            return false;

        if (!AddFont(fonts, primary, size, ranges, Math.Max(0, FaceIndex), merge: false))
            return false;

        // The Latin font has no CJK glyphs: merge the bundled subset for CJK ranges.
        // Explicitly configured fonts may already cover these code points; duplicates are skipped.
        string? cjk = UseBundledFont ? FindBundledFont(BundledCjkFont) : null;
        if (cjk is not null && IsCjkRange(range) && !IsSameFile(primary, cjk))
            AddFont(fonts, cjk, size, ranges, faceIndex: 0, merge: true);

        return true;
    }

    private static unsafe bool AddFont(
        ImFontAtlasPtr fonts,
        string path,
        float size,
        IntPtr ranges,
        int faceIndex,
        bool merge)
    {
        ImFontConfigPtr config = ImGuiNative.ImFontConfig_ImFontConfig();
        try
        {
            config.FontNo = faceIndex;
            config.OversampleH = 2;
            config.OversampleV = 1;
            config.PixelSnapH = false;
            config.MergeMode = merge;

            ImFontPtr font = fonts.AddFontFromFileTTF(path, size, config, ranges);
            return font.NativePtr != null;
        }
        catch
        {
            return false;
        }
        finally
        {
            ImGuiNative.ImFontConfig_destroy(config);
        }
    }

    private static bool IsCjkRange(EditorGlyphRange range) => range is
        EditorGlyphRange.ChineseSimplified or
        EditorGlyphRange.ChineseFull or
        EditorGlyphRange.Japanese or
        EditorGlyphRange.Korean;

    private static bool IsSameFile(string left, string right)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static EditorGlyphRange ResolveGlyphRange()
    {
        string? name = Environment.GetEnvironmentVariable(FontRangeVariable);
        return !string.IsNullOrWhiteSpace(name) &&
               Enum.TryParse(name, ignoreCase: true, out EditorGlyphRange range)
            ? range
            : GlyphRange;
    }

    private static float NormalizeSize(float size) =>
        size >= 8.0f && size <= 128.0f ? size : DefaultPixelSize;

    private static string? ResolveConfiguredPath()
    {
        string? path = FilePath;
        if (string.IsNullOrWhiteSpace(path))
            path = Environment.GetEnvironmentVariable(FontPathVariable);

        return string.IsNullOrWhiteSpace(path) ? null : ResolvePath(path);
    }

    private static string? FindBundledFont(string fileName)
    {
        string? directory = NativeLibraryBootstrap.ModuleDirectory;
        if (directory is null)
            return null;

        string path = Path.Combine(directory, FontFolderName, fileName);
        return LooksLikeFontFile(path) ? path : null;
    }

    private static string? ResolvePath(string path)
    {
        if (Path.IsPathRooted(path))
            return path;

        string? directory = NativeLibraryBootstrap.ModuleDirectory;
        return directory is null ? null : Path.Combine(directory, path);
    }

    /// <summary>
    /// stb_truetype asserts on malformed input, which would take the host down with it.
    /// Check the container header and let unsupported files fall back to the bitmap font.
    /// </summary>
    private static unsafe bool LooksLikeFontFile(string path)
    {
        string extension = Path.GetExtension(path);
        if (!extension.Equals(".ttf", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".otf", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".ttc", StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            using FileStream stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[4];
            if (stream.Read(header) != header.Length)
                return false;

            uint tag = BinaryPrimitives.ReadUInt32BigEndian(header);
            return tag is 0x00010000    // TrueType outlines
                or 0x4F54544F           // "OTTO", PostScript outlines
                or 0x74727565           // "true"
                or 0x74746366;          // "ttcf", collection
        }
        catch
        {
            return false;
        }
    }

    private static IntPtr SelectRanges(ImFontAtlasPtr fonts, EditorGlyphRange range) => range switch
    {
        EditorGlyphRange.Latin => fonts.GetGlyphRangesDefault(),
        EditorGlyphRange.Greek => fonts.GetGlyphRangesGreek(),
        EditorGlyphRange.Cyrillic => fonts.GetGlyphRangesCyrillic(),
        EditorGlyphRange.ChineseSimplified => fonts.GetGlyphRangesChineseSimplifiedCommon(),
        EditorGlyphRange.ChineseFull => fonts.GetGlyphRangesChineseFull(),
        EditorGlyphRange.Japanese => fonts.GetGlyphRangesJapanese(),
        EditorGlyphRange.Korean => fonts.GetGlyphRangesKorean(),
        EditorGlyphRange.Thai => fonts.GetGlyphRangesThai(),
        EditorGlyphRange.Vietnamese => fonts.GetGlyphRangesVietnamese(),
        _ => fonts.GetGlyphRangesDefault(),
    };
}
