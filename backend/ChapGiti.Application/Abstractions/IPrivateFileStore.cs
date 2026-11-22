namespace ChapGiti.Application.Abstractions;

public interface IPrivateFileStore
{
    Task<byte[]> ReadPdf(string key);
    Task<byte[]> ReadImage(string id);
    Task WritePdf(string key, byte[] bytes);
    Task WriteImage(string id, byte[] bytes);
    void DeletePdf(string key);
    void DeleteImage(string id);
}
