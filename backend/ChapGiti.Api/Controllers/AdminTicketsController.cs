using System.Text.Json.Nodes;
using ChapGiti.Api.Http;
using ChapGiti.Application.Features;
using Microsoft.AspNetCore.Mvc;

namespace ChapGiti.Api.Controllers;

[Route("api/admin/tickets")]
[RequireAdministrator]
public sealed class AdminTicketsController(TicketService tickets) : ApiControllerBase
{
    [HttpGet]
    public async Task<IResult> List([FromQuery] string? q="", [FromQuery] string? status="", [FromQuery] int? page=null, [FromQuery] int pageSize=20, [FromQuery] bool oldestFirst = false) => Respond(await tickets.List(true,q??"",status??"",page,pageSize, oldestFirst));
    [HttpGet("{id:guid}")]
    public async Task<IResult> Get(string id) => Respond(await tickets.Get(id, true));
    [HttpPatch("{id:guid}")]
    public async Task<IResult> Update(string id, JsonObject body) => Respond(await tickets.ChangeStatus(id, body, Administrator));
    [HttpPost("{id:guid}/read")]
    public async Task<IResult> Read(string id, JsonObject body) => Respond(await tickets.Read(id, body, true));
    [HttpPost("{id:guid}/messages")]
    public async Task<IResult> Reply(string id, JsonObject body) => Respond(await tickets.Reply(id, body, true, Administrator));
}
