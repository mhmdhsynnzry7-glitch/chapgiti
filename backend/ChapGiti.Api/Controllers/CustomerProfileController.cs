using System.Text.Json.Nodes;
using ChapGiti.Api.Contracts;
using ChapGiti.Application.Features;
using Microsoft.AspNetCore.Mvc;

namespace ChapGiti.Api.Controllers;

[Route("api/customer")]
public sealed class CustomerProfileController(AccountService accounts, OrderService orders) : ApiControllerBase
{
    [HttpGet("me")]
    public async Task<IResult> Me() => Respond(await accounts.GetCustomer());
    [HttpPut("profile")]
    public async Task<IResult> Update(ProfileRequest body) => Respond(await accounts.SaveProfile(body.ToBody()));
    [HttpGet("orders")]
    public async Task<IResult> Orders([FromQuery] int? page = null, [FromQuery] int pageSize = 10, [FromQuery] string? group = null, [FromQuery] string attention = "") =>
        Respond(page.HasValue || attention.Length != 0 ? await orders.CustomerOrders(page ?? 1, pageSize, group ?? "",attention) : await accounts.RecentOrders());
    [HttpGet("orders/{id:guid}")]
    public async Task<IResult> Order(string id) => Respond(await orders.CustomerOrder(id));
    [HttpGet("files/{id:guid}")]
    public async Task<IResult> File(string id,[FromQuery] bool preview=false)=>Respond(await orders.Download(id,preview));
}
