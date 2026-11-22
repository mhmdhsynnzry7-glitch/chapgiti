using System.Text.Json.Nodes;
using ChapGiti.Api.Http;
using ChapGiti.Application.Common;
using ChapGiti.Application.Features;
using Microsoft.AspNetCore.Mvc;

namespace ChapGiti.Api.Controllers;

[Route("api")]
public sealed class MagazineController(MagazineService magazine) : ApiControllerBase
{
    [HttpGet("public/articles")]
    public async Task<IResult> Public(string q = "", string category = "", int page = 1, int pageSize = 12) => Respond(UseCaseResult.Json(await magazine.List(false, q, "published", category, page, pageSize)));
    [HttpGet("public/articles/{slug:maxlength(120)}")]
    public async Task<IResult> Detail(string slug) => Respond(await magazine.Detail(slug));
    [HttpGet("public/magazine/sitemap")]
    public async Task<IResult> Sitemap() => Respond(await magazine.Sitemap());
    [RequireAdministrator, HttpGet("admin/articles")]
    public async Task<IResult> List(string q = "", string status = "", string category = "", int page = 1, int pageSize = 20, [FromQuery] bool oldestFirst = false) => Respond(UseCaseResult.Json(await magazine.List(true, q, status, category, page, pageSize, oldestFirst)));
    [RequireAdministrator, HttpGet("admin/articles/{id:maxlength(50)}")]
    public async Task<IResult> Edit(string id) => Respond(await magazine.Edit(id));
    [RequireAdministrator, HttpPost("admin/articles/prepare-drafts")]
    public async Task<IResult> PrepareDrafts() => Respond(await magazine.PrepareDrafts(Administrator));
    [RequireAdministrator, HttpPost("admin/articles")]
    public async Task<IResult> Create(JsonObject body) => Respond(await magazine.Save(null, body, Administrator));
    [RequireAdministrator, HttpPut("admin/articles/{id:maxlength(50)}")]
    public async Task<IResult> Update(string id, JsonObject body) => Respond(await magazine.Save(id, body, Administrator));
}
