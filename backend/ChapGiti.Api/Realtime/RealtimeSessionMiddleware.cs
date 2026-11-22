using System.Text.Encodings.Web;
using ChapGiti.Application.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ChapGiti.Api.Realtime;

public sealed class RealtimeSessionMiddleware(RequestDelegate next, IConfiguration configuration, RealtimeAccess access, ILogger<RealtimeSessionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api/realtime")) { await next(context); return; }
        var admin = context.Request.Path.StartsWithSegments("/api/realtime/admin");
        if (!admin && !context.Request.Path.StartsWithSegments("/api/realtime/customer")) { context.Response.StatusCode = 404; return; }
        var origin = context.Request.Headers.Origin.ToString();
        var expected = new Uri(configuration["Site:Url"]!).GetLeftPart(UriPartial.Authority);
        var requireOrigin = context.WebSockets.IsWebSocketRequest || !HttpMethods.IsGet(context.Request.Method);
        if ((requireOrigin || origin != "") && !string.Equals(origin, expected, StringComparison.Ordinal))
        {
            logger.LogWarning("Realtime authorization rejected: origin.");
            context.Response.StatusCode = 403; return;
        }
        try { context.Items[RealtimeAccess.ItemKey] = await access.Authenticate(context, admin); }
        catch (UseCaseException)
        {
            logger.LogWarning("Realtime authorization rejected: {Scope} session.", admin ? "admin" : "customer");
            context.Response.StatusCode = 401; return;
        }
        context.Response.Headers.CacheControl = "no-store, no-transform";
        await next(context);
    }
}

// Adapts an already validated existing session to SignalR's standard authorization pipeline.
// It issues no cookie/token and is not a second login system.
public sealed class RealtimeSessionHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(
        Context.Items[RealtimeAccess.ItemKey] is RealtimeIdentity identity
            ? AuthenticateResult.Success(new AuthenticationTicket(identity.Principal(), Scheme.Name))
            : AuthenticateResult.NoResult());
    protected override Task HandleChallengeAsync(AuthenticationProperties properties) { Response.StatusCode = 401; return Task.CompletedTask; }
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) { Response.StatusCode = 403; return Task.CompletedTask; }
}
