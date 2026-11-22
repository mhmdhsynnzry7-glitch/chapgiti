using ChapGiti.Application.Common;
using ChapGiti.Application.Features;
using Microsoft.AspNetCore.Mvc;

namespace ChapGiti.Api.Controllers;

[Route("api")]
public sealed class PublicContentController(ContentService content, PublicAssetService assets) : ApiControllerBase
{
    [HttpGet("public/bootstrap")]
    public async Task<IResult> Bootstrap() => Respond(UseCaseResult.Json(await content.Bootstrap()));
    [HttpGet("public/services")]
    public async Task<IResult> Services() => Respond(UseCaseResult.Json(await content.Services()));
    [HttpGet("public/services/{slug:maxlength(100)}")]
    public async Task<IResult> Service(string slug) => Respond(UseCaseResult.Json(await content.Service(slug)));
    [HttpGet("banner-assets/{id:guid}")]
    public async Task<IResult> Asset(string id)
    {
        var result = await assets.Get(id);
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return Respond(result);
    }
}
