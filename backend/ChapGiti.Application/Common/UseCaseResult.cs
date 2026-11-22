namespace ChapGiti.Application.Common;

/// <summary>Application output. HTTP serialization is implemented by the API adapter.</summary>
public sealed record UseCaseResult(
    object? Data = null,
    bool Created = false,
    byte[]? Bytes = null,
    string? ContentType = null,
    string? DownloadName = null,
    bool Sandbox = false)
{
    public static UseCaseResult Json(object? data, bool created = false) =>
        new(Data: data, Created: created);

    public static UseCaseResult Text(string text, string contentType, string? downloadName = null) =>
        new(Bytes: System.Text.Encoding.UTF8.GetBytes(text), ContentType: contentType, DownloadName: downloadName);

    public static UseCaseResult File(byte[] bytes, string contentType, string? downloadName = null, bool sandbox = false) =>
        new(Bytes: bytes, ContentType: contentType, DownloadName: downloadName, Sandbox: sandbox);
}
