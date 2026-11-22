using System.Text.Json;
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
        try
        {
            await next(context);
        }
        catch (RuleViolation error)
        {
            await Error(context, 400, error.Message);
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
            logger.LogError(error, "Backend request failed");
            await Error(context, 500, "در پردازش درخواست مشکلی پیش آمد. دوباره تلاش کنید.");
        }
    }

    private static async Task Error(HttpContext context, int status, string message, int? retryAfterSeconds = null, string? code = null)
    {
        context.Response.StatusCode = status;
        if (code is not null)
        {
            await context.Response.WriteAsJsonAsync(new { error = message, retryAfterSeconds, code });
            return;
        }
        await context.Response.WriteAsJsonAsync(new
        {
            error = message,
            retryAfterSeconds
        });
    }
}
