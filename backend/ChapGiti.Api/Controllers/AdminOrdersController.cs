using System.Text.Json.Nodes;
using ChapGiti.Api.Http;
using ChapGiti.Application.Features;
using Microsoft.AspNetCore.Mvc;

namespace ChapGiti.Api.Controllers;

[Route("api/admin")]
[RequireAdministrator]
public sealed class AdminOrdersController(AdminOrderService orders) : ApiControllerBase
{
    [HttpGet("overview")]
    public async Task<IResult> Overview() => Respond(await orders.Overview(Administrator));
    [HttpGet("orders")]
    public async Task<IResult> List([FromQuery] string? q, [FromQuery] string? status, [FromQuery] string? page, [FromQuery] int pageSize = 20, [FromQuery] bool oldestFirst = false, [FromQuery] string attention = "") =>
        Respond(await orders.List(q ?? "", status ?? "", page ?? "", pageSize, oldestFirst, attention));
    [HttpGet("orders/{id:guid}")]
    public async Task<IResult> Get(string id) => Respond(await orders.Get(id));
    [HttpPost("orders/{id:guid}/open")]
    public async Task<IResult> Open(string id) => Respond(await orders.Open(id, Administrator));
    [HttpPost("orders/{id:guid}/notes")]
    public async Task<IResult> AddNote(string id, JsonObject body) => Respond(await orders.AddNote(id, body, Administrator));
    [HttpPatch("orders/{id:guid}")]
    public async Task<IResult> Update(string id, JsonObject body) => Respond(await orders.Update(id, body, Administrator));
    [HttpGet("files/{id:guid}")]
    public async Task<IResult> Download(string id,[FromQuery] bool preview=false) => Respond(await orders.Download(id,preview));
    [HttpGet("export")]
    public async Task<IResult> Export() => Respond(await orders.Export());
    [HttpGet("customers")]
    public async Task<IResult> Customers() => Respond(await orders.Customers());
    [HttpGet("messages")]
    public async Task<IResult> Messages() => Respond(await orders.Messages());
    [HttpPatch("messages/{id:guid}")]
    public async Task<IResult> MarkRead(string id) => Respond(await orders.MarkMessageRead(id));
    [HttpGet("sms")]
    public async Task<IResult> Sms() => Respond(await orders.Sms());
    [HttpGet("audit")]
    public async Task<IResult> Audit() => Respond(await orders.Audit());
}
