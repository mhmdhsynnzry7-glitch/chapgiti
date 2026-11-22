using ChapGiti.Application.Abstractions;
using ChapGiti.Application.Common;

namespace ChapGiti.Api.Http;

internal sealed class FormUploadContent(IFormFile file,CancellationToken cancellation) : IUploadContent
{
    public string? ContentType=>file.ContentType;
    public string? EncodedFileName=>Uri.EscapeDataString(file.FileName);
    public string OrderCode=>"";
    public async Task<byte[]> Read(int maximumBytes)
    {
        if(file.Length>maximumBytes) throw new UseCaseException(ErrorKind.TooLarge,"حداکثر حجم هر PDF برابر با ۱۵ مگابایت است.");
        await using var source=file.OpenReadStream();
        using var target=new MemoryStream();
        var buffer=new byte[65536]; int read;
        while((read=await source.ReadAsync(buffer,cancellation))>0)
        {
            if(target.Length+read>maximumBytes) throw new UseCaseException(ErrorKind.TooLarge,"حجم PDF بیش از حد مجاز است.");
            await target.WriteAsync(buffer.AsMemory(0,read),cancellation);
        }
        return target.ToArray();
    }
}
