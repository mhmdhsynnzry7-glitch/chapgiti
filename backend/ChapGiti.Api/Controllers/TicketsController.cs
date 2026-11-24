using ChapGiti.Api.Http;
using System.Text.Json.Nodes;
using ChapGiti.Application.Features;
using Microsoft.AspNetCore.Mvc;

namespace ChapGiti.Api.Controllers;

[Route("api")]
public sealed class TicketsController(TicketService tickets) : ApiControllerBase
{
    [HttpGet("tickets")]
    public async Task<IResult> List([FromQuery] string? q="", [FromQuery] string? status="", [FromQuery] int? page=null, [FromQuery] int pageSize=20) => Respond(await tickets.List(false,q??"",status??"",page,pageSize));
    [HttpPost("tickets")]
    [HttpPost("contact")]
    public async Task<IResult> Create(JsonObject body) => Respond(await tickets.Create(body));
    [HttpGet("tickets/{id:guid}")]
    public async Task<IResult> Get(string id) => Respond(await tickets.Get(id));
    [HttpPost("tickets/{id:guid}/read")]
    public async Task<IResult> Read(string id, JsonObject body) => Respond(await tickets.Read(id, body, false));
    [HttpPost("tickets/{id:guid}/messages")]
    public async Task<IResult> Reply(string id, JsonObject body) => Respond(await tickets.Reply(id, body));
    [HttpPost("tickets/{id:guid}/messages/attachments"), RequestSizeLimit(16*1024*1024)]
    public async Task<IResult> Attach(string id) => Respond(await TicketForm.Submit(Request, tickets, id, false));
    [HttpGet("tickets/{id:guid}/attachments/{fileId:guid}")]
    public async Task<IResult> Download(string id, string fileId) => Respond(await tickets.Download(id, fileId, false));
}
