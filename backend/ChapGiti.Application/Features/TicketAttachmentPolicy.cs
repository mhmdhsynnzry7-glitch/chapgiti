using ChapGiti.Domain.Tickets;

namespace ChapGiti.Application.Features;

public sealed record TicketAttachmentPolicy(int MaxFiles = 3, int MaxBytes = 4*1024*1024)
{
    public static TicketAttachmentPolicy Default { get; } = new();
    internal async Task<List<TicketUpload>> Prepare(IReadOnlyList<IUploadContent> uploads)
    {
        if (uploads.Count > MaxFiles)
            throw new UseCaseException(ErrorKind.Invalid,"تعداد پیوست‌های انتخاب‌شده بیش از حد مجاز است.", code:"TICKET_ATTACHMENT_COUNT");
        var files = new List<TicketUpload>();
        foreach (var upload in uploads)
        {
            var name = Uri.UnescapeDataString(upload.EncodedFileName ?? "").Trim();
            if(name.Length is 0 or >160 || name.Any(c => char.IsControl(c) || System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)==System.Globalization.UnicodeCategory.Format && c!='\u200c' || "<>:/\\|\"?*".Contains(c)))
                throw new UseCaseException(ErrorKind.Invalid,"نام پیوست معتبر نیست.",code:"TICKET_ATTACHMENT_NAME");
            byte[] bytes;
            try { bytes = await upload.Read(MaxBytes); }
            catch (UseCaseException e) when(e.Kind==ErrorKind.TooLarge) { throw new UseCaseException(ErrorKind.TooLarge,"حجم پیوست بیش از حد مجاز است.",code:"TICKET_ATTACHMENT_TOO_LARGE"); }
            var mime=TicketFileFormat.Mime(bytes, Path.GetExtension(name).ToLowerInvariant()) ??
                throw new UseCaseException(ErrorKind.Invalid,"محتوا یا نوع پیوست معتبر نیست.", code:"TICKET_ATTACHMENT_INVALID_TYPE");
            files.Add(new(name,mime,bytes, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))));
        }
        return files;
    }
}
internal sealed record TicketUpload(string Name, string Mime, byte[] Bytes, string Digest);
