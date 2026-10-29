using ImGuiNET;
using SkiaSharp;

namespace Vst3.Editor;

/// <summary>Renders ImGui draw data into a BGRA bitmap and blits it with GDI.</summary>
internal sealed unsafe class SkiaRenderer : IDisposable
{
    private SKBitmap _bitmap;
    private SKCanvas _canvas;
    private readonly SKPaint _solidPaint = new() { IsAntialias = true };
    private readonly SKPaint _texturedPaint = new() { IsAntialias = true };
    private readonly SKBitmap _fontBitmap;
    private readonly SKImage _fontImage;
    private readonly SKShader _fontShader;
    private readonly int _atlasWidth;
    private readonly int _atlasHeight;
    private readonly float _whiteU;
    private readonly float _whiteV;
    private int _width;
    private int _height;

    public SkiaRenderer(int width, int height, ImFontAtlasPtr fonts)
    {
        _width = width;
        _height = height;
        _bitmap = new SKBitmap(CreateInfo(width, height));
        _canvas = new SKCanvas(_bitmap);

        if (fonts.Fonts.Size == 0)
            fonts.AddFontDefault();

        fonts.GetTexDataAsRGBA32(out IntPtr pixels, out int atlasWidth, out int atlasHeight, out _);
        _atlasWidth = atlasWidth;
        _atlasHeight = atlasHeight;

        var info = new SKImageInfo(atlasWidth, atlasHeight, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        _fontBitmap = new SKBitmap(info);
        long bytes = (long)atlasWidth * atlasHeight * 4;
        Buffer.MemoryCopy((void*)pixels, (void*)_fontBitmap.GetPixels(), bytes, bytes);
        _fontImage = SKImage.FromBitmap(_fontBitmap);
        _fontShader = SKShader.CreateImage(_fontImage, SKShaderTileMode.Clamp, SKShaderTileMode.Clamp);
        _texturedPaint.Shader = _fontShader;

        var white = fonts.TexUvWhitePixel;
        _whiteU = white.X * atlasWidth;
        _whiteV = white.Y * atlasHeight;

        string? atlasDump = Environment.GetEnvironmentVariable("VST3SHARP_DUMP_ATLAS");
        if (!string.IsNullOrEmpty(atlasDump))
        {
            using var image = SKImage.FromBitmap(_fontBitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.Create(atlasDump);
            data.SaveTo(stream);
        }

        fonts.SetTexID((IntPtr)1);
    }

    private static SKImageInfo CreateInfo(int width, int height) =>
        new(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);

    public void Resize(int width, int height)
    {
        if (width == _width && height == _height)
            return;

        _canvas.Dispose();
        _bitmap.Dispose();
        _width = width;
        _height = height;
        _bitmap = new SKBitmap(CreateInfo(width, height));
        _canvas = new SKCanvas(_bitmap);
    }

    public void Render(ImDrawDataPtr drawData, nint hwnd)
    {
        _canvas.Clear(new SKColor(30, 30, 34));

        for (int listIndex = 0; listIndex < drawData.CmdListsCount; listIndex++)
        {
            var cmdList = drawData.CmdLists[listIndex];
            int vertexCount = cmdList.VtxBuffer.Size;
            if (vertexCount == 0)
                continue;

            var sourceVertices = (ImDrawVert*)cmdList.VtxBuffer.Data;
            var positions = new SKPoint[vertexCount];
            var texCoords = new SKPoint[vertexCount];
            var colors = new SKColor[vertexCount];

            for (int i = 0; i < vertexCount; i++)
            {
                var vertex = sourceVertices[i];
                positions[i] = new SKPoint(vertex.pos.X, vertex.pos.Y);
                texCoords[i] = new SKPoint(vertex.uv.X * _atlasWidth, vertex.uv.Y * _atlasHeight);

                uint color = vertex.col;
                colors[i] = new SKColor(
                    (byte)(color & 0xFF),
                    (byte)((color >> 8) & 0xFF),
                    (byte)((color >> 16) & 0xFF),
                    (byte)((color >> 24) & 0xFF));
            }

            var sourceIndices = (ushort*)cmdList.IdxBuffer.Data;

            for (int cmdIndex = 0; cmdIndex < cmdList.CmdBuffer.Size; cmdIndex++)
            {
                var command = cmdList.CmdBuffer[cmdIndex];
                if (command.UserCallback != IntPtr.Zero)
                    continue;

                int elementCount = (int)command.ElemCount;
                if (elementCount <= 0)
                    continue;

                var solidIndices = new ushort[elementCount];
                var texturedIndices = new ushort[elementCount];
                int solidCount = 0;
                int texturedCount = 0;

                var source = sourceIndices + command.IdxOffset;
                for (int i = 0; i + 2 < elementCount; i += 3)
                {
                    ushort i0 = source[i];
                    ushort i1 = source[i + 1];
                    ushort i2 = source[i + 2];

                    if (IsWhite(texCoords[i0]) && IsWhite(texCoords[i1]) && IsWhite(texCoords[i2]))
                    {
                        solidIndices[solidCount++] = i0;
                        solidIndices[solidCount++] = i1;
                        solidIndices[solidCount++] = i2;
                    }
                    else
                    {
                        texturedIndices[texturedCount++] = i0;
                        texturedIndices[texturedCount++] = i1;
                        texturedIndices[texturedCount++] = i2;
                    }
                }

                _canvas.Save();
                _canvas.ClipRect(new SKRect(
                    command.ClipRect.X, command.ClipRect.Y, command.ClipRect.Z, command.ClipRect.W));

                // Skia ignores vertex colors here, so draw solid triangles with a per-triangle paint color.
                for (int i = 0; i < solidCount; i += 3)
                {
                    ushort i0 = solidIndices[i];
                    ushort i1 = solidIndices[i + 1];
                    ushort i2 = solidIndices[i + 2];

                    _solidPaint.Color = colors[i0];
                    var triangle = new ushort[] { i0, i1, i2 };
                    using var vertices = SKVertices.CreateCopy(
                        SKVertexMode.Triangles, positions, texCoords, null, triangle);
                    _canvas.DrawVertices(vertices, SKBlendMode.SrcOver, _solidPaint);
                }

                if (texturedCount > 0)
                {
                    var indices = new ushort[texturedCount];
                    Array.Copy(texturedIndices, indices, texturedCount);
                    using var vertices = SKVertices.CreateCopy(
                        SKVertexMode.Triangles, positions, texCoords, null, indices);
                    _canvas.DrawVertices(vertices, SKBlendMode.SrcOver, _texturedPaint);
                }

                _canvas.Restore();
            }
        }

        Blit(hwnd);
    }

    private bool IsWhite(SKPoint uv) =>
        MathF.Abs(uv.X - _whiteU) < 1.0f && MathF.Abs(uv.Y - _whiteV) < 1.0f;

    private void Blit(nint hwnd)
    {
        nint dc = Win32.GetDC(hwnd);
        if (dc == 0)
            return;

        try
        {
            var info = new Win32.BITMAPINFO();
            info.Header.Size = (uint)sizeof(Win32.BITMAPINFOHEADER);
            info.Header.Width = _width;
            info.Header.Height = -_height;
            info.Header.Planes = 1;
            info.Header.BitCount = 32;

            Win32.StretchDIBits(
                dc, 0, 0, _width, _height, 0, 0, _width, _height,
                (void*)_bitmap.GetPixels(), &info, Win32.DIB_RGB_COLORS, Win32.SRCCOPY);
        }
        finally
        {
            Win32.ReleaseDC(hwnd, dc);
        }
    }

    public void SaveSnapshot(string path)
    {
        using var image = SKImage.FromBitmap(_bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }

    public void Dispose()
    {
        _canvas.Dispose();
        _bitmap.Dispose();
        _solidPaint.Dispose();
        _texturedPaint.Dispose();
        _fontShader.Dispose();
        _fontImage.Dispose();
        _fontBitmap.Dispose();
    }
}
