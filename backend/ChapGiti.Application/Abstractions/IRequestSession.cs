namespace ChapGiti.Application.Abstractions;

/// <summary>Session transport supplied by the hosting adapter; tokens never enter business commands.</summary>
public interface IRequestSession
{
    string? GetToken(bool administrator);
    void SetToken(bool administrator, string token, TimeSpan lifetime);
    string? GetRefreshToken(bool administrator = false);
    void SetRefreshToken(string token, TimeSpan lifetime, bool administrator = false);
    void ClearLegacyCookies();
    void RequireSecureTransport();
    string ClientAddress
    {
        get;
    }
}
