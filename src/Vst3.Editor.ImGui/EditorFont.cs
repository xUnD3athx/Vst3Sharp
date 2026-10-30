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
/// Set this before the first editor is opened; a missing or invalid file falls back
/// to the built-in bitmap font. Relative paths resolve against the plugin module directory.
/// </summary>
public static class EditorFont
{
    /// <summary>Environment overrides used by the offline editor test.</summary>
    private const string FontPathVariable = "VST3SHARP_FONT";

    private const string FontRangeVariable = "VST3SHARP_FONT_RANGE";

    public const float DefaultPixelSize = 16.0f;

    /// <summary>Path to a .ttf, .otf or .ttc file, absolute or relative to the plugin module.</summary>
    public static string? FilePath { get; set; }

    /// <summary>Face index inside a .ttc collection; 0 for single-face fonts.</summary>
    public static int FaceIndex { get; set; }

    /// <summary>Font size in pixels. The default panel and editor views scale from this size.</summary>
    public static float PixelSize { get; set; } = DefaultPixelSize;

    public static EditorGlyphRange GlyphRange { get; set; } = EditorGlyphRange.Default;

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

    /// <summary>Restores the built-in bitmap font.</summary>
    public static void Reset()
    {
        FilePath = null;
        FaceIndex = 0;
        PixelSize = DefaultPixelSize;
        GlyphRange = EditorGlyphRange.Default;
    }

    internal static unsafe bool TryAdd(ImFontAtlasPtr fonts)
    {
        string? path = FilePath;
        if (string.IsNullOrWhiteSpace(path))
            path = Environment.GetEnvironmentVariable(FontPathVariable);

        if (string.IsNullOrWhiteSpace(path))
            return false;

        string? resolved = ResolvePath(path);
        if (resolved is null || !LooksLikeFontFile(resolved))
            return false;

        ImFontConfigPtr config = ImGuiNative.ImFontConfig_ImFontConfig();
        try
        {
            config.FontNo = Math.Max(0, FaceIndex);
            config.OversampleH = 2;
            config.OversampleV = 1;
            config.PixelSnapH = false;

            IntPtr ranges = SelectRanges(fonts, ResolveGlyphRange());
            ImFontPtr font = fonts.AddFontFromFileTTF(resolved, NormalizeSize(PixelSize), config, ranges);
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
