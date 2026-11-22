using ChapGiti.Application.Orders;
using ChapGiti.Domain.Printing;

namespace ChapGiti.Application.Features;

public sealed record PreparedOrderPdf(string Name, byte[] Bytes);

public sealed class OrderPdfFiles(IOrderRepository orders, IOrderCorrectionRepository corrections, IPrivateFileStore storage)
{
    public async Task<PreparedOrderPdf> Prepare(IUploadContent upload)
    {
        var bytes = await upload.Read(PdfFormat.MaximumBytes);
        if (!string.Equals(upload.ContentType?.Split(';')[0].Trim(),"application/pdf",StringComparison.OrdinalIgnoreCase))
            throw new UseCaseException(ErrorKind.UnsupportedMedia, "فقط فایل PDF مجاز است.", code: "PDF_TYPE_INVALID");
        if (!PdfFormat.IsPdf(bytes)) throw InvalidPdf();
        string name;
        try { name = Uri.UnescapeDataString(upload.EncodedFileName ?? "document.pdf"); }
        catch (UriFormatException) { throw new UseCaseException(ErrorKind.Invalid, "نام فایل معتبر نیست.", code: "PDF_NAME_INVALID"); }
        name = Regex.Replace(name, @"[\\/:\x00-\x1f]", "_");
        if (name.Length is < 5 or > 160 || !name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            throw new UseCaseException(ErrorKind.Invalid, "نام فایل PDF معتبر نیست.", code: "PDF_NAME_INVALID");
        return new(name, bytes);
    }
    public async Task<OrderFileRecord> Save(string orderId, PreparedOrderPdf pdf, string? correctionId, List<string> written)
    {
        var current = await corrections.Files(orderId);
        if (current.Count(file => file.RevisionId == correctionId) >= 5) throw new UseCaseException(ErrorKind.Invalid, "حداکثر ۵ فایل برای هر مرحله مجاز است؛ تاریخچهٔ فایل‌ها حذف نمی‌شود.", code: "ORDER_FILE_LIMIT");
        var id = Input.NewId(); var key = orderId + "/" + id + ".pdf";
        var version = Math.Max(current.Count, current.Select(file => file.FileVersion ?? 0).DefaultIfEmpty().Max()) + 1;
        var file = new OrderFileRecord(id, orderId, key, pdf.Name, pdf.Bytes.Length, Input.Now(), correctionId, version);
        await storage.WritePdf(key, pdf.Bytes); written.Add(key);
        await orders.InsertFile(id, orderId, key, file.Name, file.Size, file.CreatedAt);
        await corrections.LinkFile(orderId, id, correctionId, version);
        return file;
    }
    public void Cleanup(IEnumerable<string> written) { foreach (var key in written) storage.DeletePdf(key); }
    public async Task ValidateStored(OrderFileRecord file)
    {
        try { if (!PdfFormat.IsPdf(await storage.ReadPdf(file.ObjectKey))) throw InvalidPdf(); }
        catch (UseCaseException error) when (error.Kind == ErrorKind.NotFound)
        { throw new UseCaseException(ErrorKind.Conflict, "فایل PDF انتخاب‌شده در دسترس نیست؛ فایل صحیح را ارسال کنید.", code: "ORDER_FILE_UNAVAILABLE"); }
    }
    private static UseCaseException InvalidPdf() => new(ErrorKind.Invalid,
        "محتوای PDF معتبر نیست؛ فایل باید سرآیند، ساختار و پایان معتبر PDF داشته باشد.", code: "PDF_CONTENT_INVALID");
}
