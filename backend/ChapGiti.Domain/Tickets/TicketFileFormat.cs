using System.Buffers.Binary;
using ChapGiti.Domain.Common;
using ChapGiti.Domain.Printing;

namespace ChapGiti.Domain.Tickets;

public static class TicketFileFormat
{
    public static string? Mime(byte[] bytes, string extension)
    {
        var span = bytes.AsSpan();
        if (extension == ".pdf") return PdfFormat.IsPdf(span) ? "application/pdf" : null;
        return UploadRules.ImageMime(bytes) switch
        {
            "image/png" when extension == ".png" && Png(span) => "image/png",
            "image/jpeg" when extension is ".jpg" or ".jpeg" && Jpeg(span) => "image/jpeg",
            "image/webp" when extension == ".webp" && Webp(span) => "image/webp",
            _ => null
        };
    }

    private static bool Png(ReadOnlySpan<byte> file)
    {
        if (file.Length < 45 || !file.Slice(12,4).SequenceEqual("IHDR"u8) ||
            BinaryPrimitives.ReadUInt32BigEndian(file[8..]) != 13 ||
            BinaryPrimitives.ReadUInt32BigEndian(file[16..]) == 0 || BinaryPrimitives.ReadUInt32BigEndian(file[20..]) == 0) return false;
        var data = false;
        for (var offset = 8; offset <= file.Length-12;)
        {
            var size = BinaryPrimitives.ReadUInt32BigEndian(file[offset..]);
            if (size > file.Length-offset-12) return false;
            var type = file.Slice(offset+4,4);
            data |= type.SequenceEqual("IDAT"u8) && size > 0;
            offset += 12+(int)size;
            if (type.SequenceEqual("IEND"u8)) return data && size == 0 && offset == file.Length;
        }
        return false;
    }

    private static bool Jpeg(ReadOnlySpan<byte> file)
    {
        if (file.Length < 24 || file[^2] != 255 || file[^1] != 217) return false;
        var frame = false;
        for (var offset=2; offset+4 < file.Length;)
        {
            if (file[offset++] != 255) return false;
            while (offset < file.Length && file[offset] == 255) offset++;
            if (offset+3 > file.Length) return false;
            var marker = file[offset++];
            var size = BinaryPrimitives.ReadUInt16BigEndian(file[offset..]);
            if (size < 2 || size > file.Length-offset) return false;
            if (marker is 0xc0 or 0xc1 or 0xc2) frame = size >= 8 && BinaryPrimitives.ReadUInt16BigEndian(file[(offset+3)..]) > 0 && BinaryPrimitives.ReadUInt16BigEndian(file[(offset+5)..]) > 0;
            if (marker == 0xda) return frame && size >= 6 && offset+size < file.Length-2;
            offset += size;
        }
        return false;
    }

    private static bool Webp(ReadOnlySpan<byte> file)
    {
        if (file.Length < 26 || BinaryPrimitives.ReadUInt32LittleEndian(file[4..]) != file.Length-8) return false;
        var image = false;
        for (var offset=12; offset < file.Length;)
        {
            if (offset+8 > file.Length) return false;
            var length=BinaryPrimitives.ReadUInt32LittleEndian(file[(offset+4)..]);
            if (length > file.Length-offset-8) return false;
            var chunk=file.Slice(offset,4);
            var payload=file.Slice(offset+8,(int)length);
            image |= chunk.SequenceEqual("VP8L"u8) && length>=5 && payload[0]==0x2f ||
                     chunk.SequenceEqual("VP8 "u8) && length>=10 && payload.Slice(3,3).SequenceEqual(new byte[]{0x9d,1,0x2a});
            offset+=8+(int)length+((int)length%2);
            if (offset > file.Length) return false;
        }
        return image;
    }
}
