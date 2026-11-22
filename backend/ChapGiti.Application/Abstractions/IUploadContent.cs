namespace ChapGiti.Application.Abstractions;

/// <summary>Reads a bounded upload only after the use case has checked ownership.</summary>
public interface IUploadContent
{
    string? ContentType
    {
        get;
    }
    string? EncodedFileName
    {
        get;
    }
    string OrderCode
    {
        get;
    }
    Task<byte[]> Read(int maximumBytes);
}
