namespace ChapGiti.Application.Abstractions;

public interface IOtpSender
{
    bool Available { get; }
    int CodeLength { get; }
    // The provider-generated code stays within the backend and must never be logged.
    Task<string> Send(string phone);
}
