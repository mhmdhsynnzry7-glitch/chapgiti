using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ChapGiti.Api.Http;
using ChapGiti.Application.Abstractions;
using ChapGiti.Application.Features;
using ChapGiti.Application.Common;

namespace ChapGiti.Api.Realtime;

// Only the existing access cookie is retained in server memory, never refresh credentials or URLs.
public sealed class RealtimeIdentity(string scope, string owner, string token)
{
    public string Scope { get; } = scope;
    public string Owner { get; } = owner;
    internal string Cookie => (Scope == "admin" ? "giti_admin_access=" : "giti_customer_access=") + token;
    public string Group => Scope + ":" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Scope + "|" + Owner)));
    public ClaimsPrincipal Principal() => new(new ClaimsIdentity([
        new(ClaimTypes.NameIdentifier, Group), new(ClaimTypes.Name, Group), new("chap:scope", Scope)
    ], "ChapRealtime"));
}

public sealed class RealtimeAccess(IServiceScopeFactory scopes, IConfiguration configuration, IHostEnvironment environment)
{
    public const string ItemKey = "ChapGiti.Realtime.Identity";
    public async Task<RealtimeIdentity> Authenticate(HttpContext context, bool admin)
    {
        // Do not hold an ADO.NET connection for the lifetime of an SSE/WebSocket request.
        await using var scope = scopes.CreateAsyncScope();
        var request = new HttpSessionContext(new FixedContext { HttpContext = context }, configuration, environment);
        var sessions = Sessions(scope.ServiceProvider, request);
        var identity = admin ? await sessions.Admin() : await sessions.Customer();
        return new(admin ? "admin" : "customer", identity!.Str("id"),
            request.GetToken(admin)!);
    }

    public async Task<bool> Validate(RealtimeIdentity identity, string? ticketId = null)
    {
        await using var scope = scopes.CreateAsyncScope();
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = identity.Cookie;
        // A fixed accessor avoids changing HttpContextAccessor's shared AsyncLocal in the caller.
        var request = new HttpSessionContext(new FixedContext { HttpContext = context }, configuration, environment);
        var services = scope.ServiceProvider;
        var sessions = Sessions(services, request);
        var admin = identity.Scope == "admin";
        var current = admin ? await sessions.Admin(false) : await sessions.Customer(false);
        if (current == null || current.Str("id") != identity.Owner || (!admin && !SessionService.Ready(current))) return false;
        return ticketId == null || await services.GetRequiredService<ITicketRepository>()
            .FindTicket(ticketId, admin, admin ? null : identity.Owner, false) != null;
    }

    private static SessionService Sessions(IServiceProvider services, IRequestSession request) => new(
        services.GetRequiredService<ISessionRepository>(), services.GetRequiredService<IAccessSessionRepository>(),
        services.GetRequiredService<IUnitOfWork>(), request, services.GetRequiredService<TimeProvider>());

    private sealed class FixedContext : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }
}
