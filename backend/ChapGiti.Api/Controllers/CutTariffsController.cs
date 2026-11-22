using System.Text.Json.Nodes;
using ChapGiti.Api.Http;
using ChapGiti.Application.Features;
using Microsoft.AspNetCore.Mvc;
namespace ChapGiti.Api.Controllers;

[RequireAdministrator]
[Route("api/admin/print-tariffs")]
public sealed class CutTariffsController(CutTariffService tariffs) : ApiControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IResult> Get(string id) => Respond(await tariffs.Get(id));

    [HttpPut("{id:guid}")]
    public async Task<IResult> Save(string id, JsonObject body) => Respond(await tariffs.Save(id, body, Administrator));
}
