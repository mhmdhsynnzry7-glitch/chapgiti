using System.Text.RegularExpressions;

namespace ChapGiti.Infrastructure.Storage;

public sealed class PrivateFileStore : IPrivateFileStore
{
    private readonly string root;

    public PrivateFileStore(IConfiguration configuration)
    {
        var configured = configuration["Storage:Root"];
        if (string.IsNullOrWhiteSpace(configured) || !Path.IsPathFullyQualified(configured))
            throw new InvalidOperationException("Storage:Root must be an absolute private directory.");
        root = Path.GetFullPath(configured);
        Directory.CreateDirectory(root);
    }

    private string PdfPath(string key)
    {
        if (!Regex.IsMatch(key, @"^[a-f0-9-]{36}/[a-f0-9-]{36}\.pdf$"))
            throw new RuleViolation("شناسه فایل معتبر نیست.");
        return Path.Combine(root, "uploads", key.Replace('/', Path.DirectorySeparatorChar));
    }

    private string ImagePath(string id)
    {
        if (!Guid.TryParseExact(id, "D", out _))
            throw new UseCaseException(ErrorKind.NotFound, "تصویر پیدا نشد.");
        return Path.Combine(root, "banner-assets", id);
    }

    private static async Task<byte[]> Read(string path)
    {
        if (!File.Exists(path))
            throw new UseCaseException(ErrorKind.NotFound, "فایل در دسترس نیست.");
        return await File.ReadAllBytesAsync(path);
    }

    private static async Task Write(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // CreateNew must never overwrite an earlier attachment. Only remove a file
        // created by this call when a partial write fails.
        var created = false;
        try
        {
            await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            created = true;
            await stream.WriteAsync(bytes);
        }
        catch
        {
            if(created) File.Delete(path);
            throw;
        }
    }

    public Task<byte[]> ReadPdf(string key) => Read(PdfPath(key));
    public Task<byte[]> ReadImage(string id) => Read(ImagePath(id));
    public Task WritePdf(string key, byte[] bytes) => Write(PdfPath(key), bytes);
    public Task WriteImage(string id, byte[] bytes) => Write(ImagePath(id), bytes);
    public void DeletePdf(string key) => File.Delete(PdfPath(key));
    public void DeleteImage(string id) => File.Delete(ImagePath(id));
}
