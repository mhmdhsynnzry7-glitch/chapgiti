namespace ChapGiti.Application.Abstractions;

public interface IPrivateErrorLog
{
    void Write(string operation, string code, string requestId, int status);
}
