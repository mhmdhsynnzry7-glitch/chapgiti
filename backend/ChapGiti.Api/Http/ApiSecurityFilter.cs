using ChapGiti.Application.Common;
using ChapGiti.Application.Abstractions;
using ChapGiti.Application.Features;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ChapGiti.Api.Http;

// Runs before model binding, including for binary uploads.
public sealed class ApiSecurityFilter(IConfiguration configuration) : IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
        {
            var expected = new Uri(configuration["Site:Url"]!).GetLeftPart(UriPartial.Authority);
            if (!string.Equals(request.Headers.Origin, expected, StringComparison.Ordinal))
                throw new UseCaseException(ErrorKind.Forbidden, "مبدأ درخواست معتبر نیست.");
            if ((request.Path.StartsWithSegments("/api/customer", StringComparison.OrdinalIgnoreCase) ||
                 request.Path.StartsWithSegments("/api/auth", StringComparison.OrdinalIgnoreCase)) &&
                !request.Path.Value!.EndsWith("/logout", StringComparison.OrdinalIgnoreCase))
                context.HttpContext.RequestServices.GetRequiredService<IRequestSession>().RequireSecureTransport();
        }
        await next();
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireAdministratorAttribute : Attribute, IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var sessions = context.HttpContext.RequestServices.GetRequiredService<SessionService>();
        context.HttpContext.Items[typeof(RequireAdministratorAttribute)] = await sessions.Admin();
        await next();
    }
}
