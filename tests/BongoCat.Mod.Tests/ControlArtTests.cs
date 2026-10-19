using System.Buffers.Binary;
using System.IO.Compression;
using BongoCatMod;

internal static class ControlArtTests
{
    internal static void Run(Action<bool, string> check)
    {
        var consistent = true;
        var smoothed = true;
        foreach (var icon in Enum.GetValues<ControlIcon>())
        foreach (var size in new[] { (26, 24), (28, 28) })
        {
            var pixels = ControlButtonArt.Render(icon, size.Item1, size.Item2);
            var width = size.Item1 * ControlButtonArt.Resolution;
            var height = size.Item2 * ControlButtonArt.Resolution;
            consistent &= pixels.Length == width * height * 4 && pixels[3] == 0 && pixels[^1] == 0;
            consistent &= pixels[((height / 2) * width + width / 2) * 4 + 3] == 255;
            smoothed &= Enumerable.Range(0, width * height).Any(i => pixels[i * 4 + 3] is > 0 and < 255);
        }
        check(consistent, "Control buttons have transparent round corners and an opaque center at both display sizes");
        check(smoothed, "Every control icon has antialiased edge coverage");
        var add = ControlButtonArt.Render(ControlIcon.Add, 28, 28);
        var remove = ControlButtonArt.Render(ControlIcon.Remove, 28, 28);
        var next = ControlButtonArt.Render(ControlIcon.Next, 28, 28);
        var previous = ControlButtonArt.Render(ControlIcon.Previous, 28, 28);
        var mirrored = true;
        var stroke = false;
        var sizePx = 28 * ControlButtonArt.Resolution;
        for (var y = 0; y < sizePx; y++)
        for (var x = 0; x < sizePx; x++)
        {
            var offset = (y * sizePx + x) * 4;
            var opposite = (y * sizePx + sizePx - 1 - x) * 4;
            for (var channel = 0; channel < 4; channel++)
                mirrored &= next[offset + channel] == previous[opposite + channel];
            stroke |= add[offset] > remove[offset];
        }
        check(mirrored, "Previous and next arrow artwork is exactly mirrored");
        check(stroke, "Add artwork includes a vertical stroke absent from remove artwork");
        var frame = ControlButtonArt.RenderSelection(87, 88);
        var frameWidth = 87 * ControlButtonArt.Resolution;
        var frameHeight = 88 * ControlButtonArt.Resolution;
        check(frame[3] == 0 && frame[((frameHeight / 2) * frameWidth + frameWidth / 2) * 4 + 3] == 0 &&
            frame[((frameHeight / 2) * frameWidth + 6) * 4 + 3] == 255,
            "Selection frame has a rounded contour and keeps the cosmetic preview transparent");
    }

    internal static void ExportPreview(string path)
    {
        var icons = Enum.GetValues<ControlIcon>();
        var width = icons.Length * 128 + 16;
        const int height = 224;
        var canvas = new byte[width * height * 4];
        for (var i = 0; i < width * height; i++)
        {
            canvas[i * 4] = 47;
            canvas[i * 4 + 1] = 55;
            canvas[i * 4 + 2] = 60;
            canvas[i * 4 + 3] = 255;
        }
        for (var i = 0; i < icons.Length; i++)
        {
            var art = ControlButtonArt.Render(icons[i], 28, 28);
            var sourceWidth = 28 * ControlButtonArt.Resolution;
            Blit(art, sourceWidth, 24 + i * 128, 16, sourceWidth);
            Blit(art, sourceWidth, 66 + i * 128, 164, 28);
        }
        WritePng(path, width, height, canvas);

        void Blit(byte[] source, int sourceWidth, int left, int top, int targetSize)
        {
            for (var y = 0; y < targetSize; y++)
            for (var x = 0; x < targetSize; x++)
            {
                var offset = ((top + y) * width + left + x) * 4;
                for (var channel = 0; channel < 3; channel++)
                {
                    double sum = 0;
                    var samples = sourceWidth / targetSize;
                    for (var sy = 0; sy < samples; sy++)
                    for (var sx = 0; sx < samples; sx++)
                    {
                        var pixel = ((y * samples + sy) * sourceWidth + x * samples + sx) * 4;
                        var alpha = source[pixel + 3] / 255d;
                        sum += source[pixel + channel] * alpha + canvas[offset + channel] * (1 - alpha);
                    }
                    canvas[offset + channel] = (byte)Math.Round(sum / (samples * samples));
                }
            }
        }
    }

    private static void WritePng(string path, int width, int height, byte[] pixels)
    {
        using var output = File.Create(path);
        output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8;
        header[9] = 6;
        Chunk("IHDR", header);
        using var data = new MemoryStream();
        using (var compressed = new ZLibStream(data, CompressionLevel.Optimal, true))
            for (var y = 0; y < height; y++)
            {
                compressed.WriteByte(0);
                compressed.Write(pixels, y * width * 4, width * 4);
            }
        Chunk("IDAT", data.ToArray());
        Chunk("IEND", Array.Empty<byte>());

        void Chunk(string type, byte[] bytes)
        {
            var tag = System.Text.Encoding.ASCII.GetBytes(type);
            var value = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(value, bytes.Length);
            output.Write(value);
            output.Write(tag);
            output.Write(bytes);
            uint crc = uint.MaxValue;
            foreach (var b in tag.Concat(bytes))
            {
                crc ^= b;
                for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0);
            }
            BinaryPrimitives.WriteUInt32BigEndian(value, ~crc);
            output.Write(value);
        }
    }
}
