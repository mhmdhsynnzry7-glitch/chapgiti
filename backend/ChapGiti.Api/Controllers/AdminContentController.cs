using System.Text.Json.Nodes;
using ChapGiti.Api.Http;
using ChapGiti.Application.Features;
using Microsoft.AspNetCore.Mvc;

namespace ChapGiti.Api.Controllers;

[Route("api/admin")]
[RequireAdministrator]
public sealed class AdminContentController(AdminContentService content) : ApiControllerBase
{
    [HttpGet("settings")]
    public async Task<IResult> Settings() => Respond(await content.Settings());
    [HttpPut("settings")]
    public async Task<IResult> UpdateSettings(JsonObject body) => Respond(await content.UpdateSettings(body, Administrator));
    [HttpGet("banners")]
    public async Task<IResult> Banners() => Respond(await content.Banners(false));
    [HttpPut("banners")]
    public async Task<IResult> SaveBanners(JsonArray body) => Respond(await content.SaveBanners(false, body, Administrator));
    [HttpGet("portfolio")]
    public async Task<IResult> Portfolio() => Respond(await content.Banners(true));
    [HttpPut("portfolio")]
    public async Task<IResult> SavePortfolio(JsonArray body) => Respond(await content.SaveBanners(true, body, Administrator));
    [HttpPost("banner-assets")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IResult> Upload() => Respond(await content.UploadImage(new HttpUploadContent(Request), Administrator));
    [HttpPost("seed")]
    public async Task<IResult> Seed() => Respond(await content.Seed());
}
