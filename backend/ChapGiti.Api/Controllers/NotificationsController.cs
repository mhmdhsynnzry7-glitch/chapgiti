using ChapGiti.Api.Http;
using ChapGiti.Application.Features;
using Microsoft.AspNetCore.Mvc;

namespace ChapGiti.Api.Controllers;

[Route("api/customer/notifications")]
public sealed class CustomerNotificationsController(NotificationService notifications) : ApiControllerBase
{
    [HttpGet]
    public async Task<IResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20) => Respond(await notifications.List(false, page, pageSize));
    [HttpGet("unread-count")]
    public async Task<IResult> Count() => Respond(await notifications.Summary(false));
    [HttpPost("{id:guid}/read")]
    public async Task<IResult> Read(string id) => Respond(await notifications.Read(false, id));
    [HttpPost("read-all")]
    public async Task<IResult> ReadAll() => Respond(await notifications.ReadAll(false));
}

[Route("api/admin/notifications")]
[RequireAdministrator]
public sealed class AdminNotificationsController(NotificationService notifications) : ApiControllerBase
{
    [HttpGet]
    public async Task<IResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20) => Respond(await notifications.List(true, page, pageSize));
    [HttpGet("unread-count")]
    public async Task<IResult> Count() => Respond(await notifications.Summary(true));
    [HttpPost("{id:guid}/read")]
    public async Task<IResult> Read(string id) => Respond(await notifications.Read(true, id));
    [HttpPost("read-all")]
    public async Task<IResult> ReadAll() => Respond(await notifications.ReadAll(true));
}
