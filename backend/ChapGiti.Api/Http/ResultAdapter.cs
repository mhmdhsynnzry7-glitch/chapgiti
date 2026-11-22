using ChapGiti.Application.Common;

namespace ChapGiti.Api.Http;

public static class ResultAdapter
{
    public static IResult ToHttp(this UseCaseResult result, HttpContext context)
    {
        if (result.Sandbox)
            context.Response.Headers.ContentSecurityPolicy = "sandbox";
        if (result.Bytes is not null)
            return Results.File(result.Bytes, result.ContentType, result.DownloadName);
        return Results.Json(result.Data, statusCode: result.Created ? 201 : 200);
    }
}
