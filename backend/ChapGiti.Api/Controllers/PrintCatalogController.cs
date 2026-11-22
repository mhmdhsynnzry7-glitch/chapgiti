using System.Text.Json.Nodes;
using ChapGiti.Api.Http;
using ChapGiti.Application.Features;
using ChapGiti.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace ChapGiti.Api.Controllers;

[Route("api")]
public sealed class PrintCatalogController(PrintCatalogService catalog, ContentService content) : ApiControllerBase
{
    [RequireAdministrator, HttpGet("admin/quantity-discount")]
    public async Task<IResult> Discount() => Respond(UseCaseResult.Json(await content.Discount()));
    [RequireAdministrator, HttpPut("admin/quantity-discount")]
    public async Task<IResult> Discount(JsonObject body)
    {
        var rule = ContentService.ParseDiscount(body);
        await content.Save("quantityDiscount", new JsonObject { ["active"] = rule.Active, ["threshold"] = rule.Threshold, ["percentage"] = rule.Percentage });
        return Respond(UseCaseResult.Json(rule));
    }
    [HttpGet("public/print-catalog")]
    public async Task<IResult> Public() => Respond(UseCaseResult.Json(new { items = await catalog.Items() }));
    [RequireAdministrator, HttpGet("admin/print-catalog")]
    public async Task<IResult> All() => Respond(UseCaseResult.Json(new { items = await catalog.Items(true) }));
    [RequireAdministrator, HttpPost("admin/print-catalog")]
    public async Task<IResult> Create(JsonObject body) => Respond(await catalog.Save(null, body, Administrator));
    [RequireAdministrator, HttpDelete("admin/print-catalog/{id:guid}")]
    public async Task<IResult> Delete(string id,[FromQuery] int version) => Respond(await catalog.Delete(id,version,Administrator));
    [RequireAdministrator, HttpPut("admin/print-catalog/{id:guid}")]
    public async Task<IResult> Update(string id, JsonObject body) => Respond(await catalog.Save(id, body, Administrator));
}
