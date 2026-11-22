namespace ChapGiti.Application.Common;

public enum ErrorKind
{
    Invalid, Unauthenticated, Forbidden, NotFound, Conflict, TooLarge, UnsupportedMedia, TooManyRequests, Unavailable, Gone
}
public sealed class UseCaseException(ErrorKind kind, string message, int? retryAfterSeconds = null, string? code = null) : Exception(message)
{
    public ErrorKind Kind { get; } = kind;
    public int? RetryAfterSeconds { get; } = retryAfterSeconds;
    public string? Code { get; } = code;
}
