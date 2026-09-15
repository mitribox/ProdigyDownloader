using System.Buffers.Binary;
using System.Text;

namespace ClonerApp.Engine.Filtering;

/// <summary>
/// Lightweight image dimension probe without third-party imaging libraries.
/// Supports JPEG, PNG, GIF, BMP, WebP.
/// </summary>
public static class ImageDimensionReader
{
    public static (int Width, int Height)? TryRead(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            return TryRead(stream);
        }
        catch
        {
            return null;
        }
    }

    public static (int Width, int Height)? TryRead(Stream stream)
    {
        if (!stream.CanSeek)
            return null;

        Span<byte> header = stackalloc byte[32];
        var read = stream.Read(header);
        if (read < 10)
            return null;

        stream.Position = 0;

        if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
        {
            stream.Position = 16;
            Span<byte> wh = stackalloc byte[8];
            if (stream.Read(wh) < 8) return null;
            var width = BinaryPrimitives.ReadInt32BigEndian(wh);
            var height = BinaryPrimitives.ReadInt32BigEndian(wh[4..]);
            return width > 0 && height > 0 ? (width, height) : null;
        }

        if (header[0] == 'G' && header[1] == 'I' && header[2] == 'F')
        {
            var width = BinaryPrimitives.ReadUInt16LittleEndian(header[6..]);
            var height = BinaryPrimitives.ReadUInt16LittleEndian(header[8..]);
            return width > 0 && height > 0 ? (width, height) : null;
        }

        if (header[0] == 'B' && header[1] == 'M')
        {
            stream.Position = 18;
            Span<byte> wh = stackalloc byte[8];
            if (stream.Read(wh) < 8) return null;
            var width = BinaryPrimitives.ReadInt32LittleEndian(wh);
            var height = Math.Abs(BinaryPrimitives.ReadInt32LittleEndian(wh[4..]));
            return width > 0 && height > 0 ? (width, height) : null;
        }

        if (header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F' &&
            header[8] == 'W' && header[9] == 'E' && header[10] == 'B' && header[11] == 'P')
        {
            return TryReadWebP(stream);
        }

        if (header[0] == 0xFF && header[1] == 0xD8)
            return TryReadJpeg(stream);

        return null;
    }

    private static (int Width, int Height)? TryReadWebP(Stream stream)
    {
        stream.Position = 12;
        Span<byte> chunk = stackalloc byte[16];
        if (stream.Read(chunk) < 10) return null;

        var fourCc = Encoding.ASCII.GetString(chunk[..4]);
        if (fourCc is "VP8X")
        {
            var width = 1 + chunk[8] + (chunk[9] << 8) + (chunk[10] << 16);
            var height = 1 + chunk[11] + (chunk[12] << 8) + (chunk[13] << 16);
            return (width, height);
        }

        if (fourCc is "VP8 " && chunk[7] == 0x9D && chunk[8] == 0x01 && chunk[9] == 0x2A)
        {
            stream.Position = 26;
            Span<byte> wh = stackalloc byte[4];
            if (stream.Read(wh) < 4) return null;
            var width = BinaryPrimitives.ReadUInt16LittleEndian(wh) & 0x3FFF;
            var height = BinaryPrimitives.ReadUInt16LittleEndian(wh[2..]) & 0x3FFF;
            return width > 0 && height > 0 ? (width, height) : null;
        }

        if (fourCc is "VP8L")
        {
            stream.Position = 21;
            Span<byte> bits = stackalloc byte[4];
            if (stream.Read(bits) < 4) return null;
            var data = BinaryPrimitives.ReadUInt32LittleEndian(bits);
            var width = (int)((data & 0x3FFF) + 1);
            var height = (int)(((data >> 14) & 0x3FFF) + 1);
            return (width, height);
        }

        return null;
    }

    private static (int Width, int Height)? TryReadJpeg(Stream stream)
    {
        stream.Position = 2;
        var marker = new byte[2];
        var lenBytes = new byte[2];
        var sof = new byte[5];

        while (stream.Read(marker) == 2)
        {
            if (marker[0] != 0xFF) return null;
            var type = marker[1];
            while (type == 0xFF)
            {
                var b = stream.ReadByte();
                if (b < 0) return null;
                type = (byte)b;
            }

            if (type is 0xD8 or 0xD9)
                continue;

            if (stream.Read(lenBytes) < 2) return null;
            var length = BinaryPrimitives.ReadUInt16BigEndian(lenBytes);
            if (length < 2) return null;

            if ((type >= 0xC0 && type <= 0xC3) || (type >= 0xC5 && type <= 0xC7) ||
                (type >= 0xC9 && type <= 0xCB) || (type >= 0xCD && type <= 0xCF))
            {
                if (stream.Read(sof) < 5) return null;
                var height = BinaryPrimitives.ReadUInt16BigEndian(sof.AsSpan(1));
                var width = BinaryPrimitives.ReadUInt16BigEndian(sof.AsSpan(3));
                return width > 0 && height > 0 ? (width, height) : null;
            }

            stream.Position += length - 2;
        }

        return null;
    }
}
