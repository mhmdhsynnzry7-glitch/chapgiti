namespace ChapGiti.Domain.Printing;

public static class PdfFormat
{
    public const int MaximumBytes = 15 * 1024 * 1024;
    public static bool IsPdf(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 32 || bytes.Length > MaximumBytes || !bytes.StartsWith("%PDF-"u8)) return false;
        var version = (bytes[5] == '1' && bytes[6] == '.' && bytes[7] is >= (byte)'0' and <= (byte)'7') ||
            (bytes[5] == '2' && bytes[6] == '.' && bytes[7] == '0');
        if (!version || bytes[8] is not (10 or 13) || bytes.IndexOf("obj"u8) < 0 || bytes.IndexOf("endobj"u8) < 0) return false;
        var end = bytes[Math.Max(0, bytes.Length - 1024)..];
        var marker = end.LastIndexOf("startxref"u8);
        var eof = end.LastIndexOf("%%EOF"u8);
        if (marker < 0 || eof <= marker) return false;
        var offsetText = System.Text.Encoding.ASCII.GetString(end[(marker + 9)..eof]).Trim();
        if (!int.TryParse(offsetText, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var offset) || offset < 9 || offset >= bytes.Length) return false;
        var section = bytes[offset..];
        return section.StartsWith("xref"u8) ||
            (System.Text.RegularExpressions.Regex.IsMatch(System.Text.Encoding.ASCII.GetString(section[..Math.Min(64, section.Length)]), @"^\d+\s+\d+\s+obj\b") && section.IndexOf("/XRef"u8) >= 0);
    }
}
