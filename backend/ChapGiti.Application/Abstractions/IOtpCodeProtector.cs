namespace ChapGiti.Application.Abstractions;

public interface IOtpCodeProtector
{
    string Hash(string challengeId, string phone, string purpose, string code);
    bool Matches(string expectedHash, string challengeId, string phone, string purpose, string code);
}
