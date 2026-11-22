using System.Text.Json.Nodes;
using ChapGiti.Api.Http;
using ChapGiti.Application.Features;
using Microsoft.AspNetCore.Mvc;

namespace ChapGiti.Api.Controllers;

[Route("api/admin/services")]
[RequireAdministrator]
public sealed class AdminServicesController(ServiceContentService services) : ApiControllerBase
{
    [HttpGet]
    public async Task<IResult> List() => Respond(await services.List());
    [HttpPost]
    public async Task<IResult> Create(JsonObject body) => Respond(await services.Save(null, body, Administrator));
    [HttpDelete("{id:guid}")]
    public async Task<IResult> Delete(string id,[FromQuery] int version) => Respond(await services.Delete(id,version,Administrator));
    [HttpPut("{id:guid}")]
    public async Task<IResult> Update(string id, JsonObject body) => Respond(await services.Save(id, body, Administrator));
    [HttpPost("reorder")]
    public async Task<IResult> Reorder(JsonObject body) => Respond(await services.Reorder(body, Administrator));
}
