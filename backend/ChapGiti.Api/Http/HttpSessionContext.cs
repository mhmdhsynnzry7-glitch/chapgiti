using ChapGiti.Application.Abstractions;
using ChapGiti.Application.Common;

namespace ChapGiti.Api.Http;

public sealed class HttpSessionContext(IHttpContextAccessor accessor, IConfiguration configuration, IHostEnvironment environment) : IRequestSession
{
    private HttpContext Context => accessor.HttpContext ??
        throw new InvalidOperationException("This operation requires an HTTP request.");

    private Uri Site => new(configuration["Site:Url"]!);
    private bool LocalDevelopment => environment.IsDevelopment() && Site.IsLoopback;
    private bool Secure => !LocalDevelopment || Site.Scheme == "https";
    public void RequireSecureTransport()
    {
        if (Site.Scheme != "https" && !LocalDevelopment)
            throw new UseCaseException(ErrorKind.Unavailable, "برای ورود امن، سایت باید با HTTPS در دسترس باشد.");
    }

    public string? GetToken(bool administrator) =>
        Context.Request.Cookies.TryGetValue(administrator ? "giti_admin_access" : "giti_customer_access", out var token) ? token : null;

    public string? GetRefreshToken(bool administrator = false) =>
        Context.Request.Cookies.TryGetValue(administrator ? "giti_admin_refresh" : "giti_customer_refresh", out var token) ? token : null;
    public void SetRefreshToken(string token, TimeSpan lifetime, bool administrator = false) =>
        Write(administrator ? "giti_admin_refresh" : "giti_customer_refresh", token, lifetime);
    public void ClearLegacyCookies()
    {
        Write("naghsh_admin", "", TimeSpan.Zero);
        Write("giti_customer", "", TimeSpan.Zero);
    }

    public void SetToken(bool administrator, string token, TimeSpan lifetime) =>
        Write(administrator ? "giti_admin_access" : "giti_customer_access", token, lifetime);

    private void Write(string name, string token, TimeSpan lifetime) =>
        Context.Response.Cookies.Append(name, token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = Secure,
            Path = "/",
            MaxAge = lifetime
        });

    public string ClientAddress => configuration.GetValue<bool>("Site:TrustProxy")
        ? Context.Request.Headers["x-naghsh-client-ip"].ToString()
        : Context.Connection.RemoteIpAddress?.ToString() ?? "local";
}
