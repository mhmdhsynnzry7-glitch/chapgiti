using System.Text.Json;
using ChapGiti.Application.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using ChapGiti.Application.Common;
using ChapGiti.Domain.Common;
using Microsoft.Data.SqlClient;

namespace ChapGiti.Api.Http;

public sealed class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task Invoke(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers["X-Request-Id"] = context.TraceIdentifier;
        try
        {
            await next(context);
        }
        catch (RuleViolation error)
        {
            await Error(context, 400, error.Message, code:error.Code);
        }
        catch (UseCaseException error)
        {
            var status = error.Kind switch
            {
                ErrorKind.Invalid => 400,
                ErrorKind.Unauthenticated => 401,
                ErrorKind.Forbidden => 403,
                ErrorKind.NotFound => 404,
                ErrorKind.Conflict => 409,
                ErrorKind.TooLarge => 413,
                ErrorKind.UnsupportedMedia => 415,
                ErrorKind.TooManyRequests => 429,
                ErrorKind.Unavailable => 503,
                ErrorKind.Gone => 410,
                _ => 500
            };
            if (error.RetryAfterSeconds is int retry)
                context.Response.Headers.RetryAfter = retry.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await Error(context, status, error.Message, error.RetryAfterSeconds, error.Code);
        }
        catch (JsonException)
        {
            await Error(context, 400, "ساختار درخواست معتبر نیست.");
        }
        catch (BadHttpRequestException error)
        {
            await Error(context, error.StatusCode, "حجم یا ساختار درخواست معتبر نیست.");
        }
        catch (SqlException error) when (error.Number is 2601 or 2627)
        {
            await Error(context, 409, "این شناسه یا مقدار قبلاً ثبت شده است.");
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
        catch (Exception error)
        {
            logger.LogError("Backend failure {ExceptionType}, SQL {SqlNumber}, request {RequestId}", error.GetType().Name, (error as SqlException)?.Number, context.TraceIdentifier);
            await Error(context, 500, "در پردازش درخواست مشکلی پیش آمد. دوباره تلاش کنید.");
        }
    }

    private static async Task Error(HttpContext context, int status, string message, int? retryAfterSeconds = null, string? code = null)
    {
        context.Response.StatusCode = status;
        code ??= DefaultCode(status);
        if (status >= 500)
        {
            var endpoint = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
            var operation = endpoint is null ? "HttpRequest" : endpoint.ControllerName + "." + endpoint.ActionName;
            context.RequestServices.GetRequiredService<IPrivateErrorLog>().Write(operation, code, context.TraceIdentifier, status);
        }
        await context.Response.WriteAsJsonAsync(new { error = message, code, retryAfterSeconds, requestId = context.TraceIdentifier });
    }

    internal static string DefaultCode(int status) => status switch
    {
        400 => "VALIDATION_INVALID", 401 => "AUTH_UNAUTHORIZED", 403 => "AUTH_FORBIDDEN",
        404 => "RESOURCE_NOT_FOUND", 405 => "METHOD_NOT_ALLOWED", 409 => "RESOURCE_CONFLICT", 410 => "RESOURCE_GONE",
        413 => "REQUEST_TOO_LARGE", 415 => "UNSUPPORTED_MEDIA", 429 => "RATE_LIMITED",
        503 => "SERVICE_UNAVAILABLE", _ => "INTERNAL_SERVER_ERROR"
    };
}
