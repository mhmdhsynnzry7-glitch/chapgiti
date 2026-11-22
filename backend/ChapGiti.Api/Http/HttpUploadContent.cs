using ChapGiti.Application.Abstractions;
using ChapGiti.Application.Common;

namespace ChapGiti.Api.Http;

public sealed class HttpUploadContent(HttpRequest request) : IUploadContent
{
    public string? ContentType => request.ContentType;
    public string? EncodedFileName => request.Headers["x-file-name"].FirstOrDefault();
    public string OrderCode => request.Headers["x-order-code"].ToString();
    public Task<byte[]> Read(int maximumBytes) => ReadBody(request, maximumBytes);

    public static async Task<byte[]> ReadBody(HttpRequest request, int maximumBytes)
    {
        if (request.ContentLength > maximumBytes)
            throw new UseCaseException(ErrorKind.TooLarge, "حجم فایل یا درخواست بیش از حد مجاز است.");
        using var output = new MemoryStream();
        var buffer = new byte[65536];
        int read;
        while ((read = await request.Body.ReadAsync(buffer, request.HttpContext.RequestAborted)) > 0)
        {
            if (output.Length + read > maximumBytes)
                throw new UseCaseException(ErrorKind.TooLarge, "حجم فایل یا درخواست بیش از حد مجاز است.");
            await output.WriteAsync(buffer.AsMemory(0, read));
        }
        return output.ToArray();
    }
}
