using System.Text.Json.Nodes;
using ChapGiti.Api.Contracts;
using ChapGiti.Api.Http;
using ChapGiti.Application.Features;
using Microsoft.AspNetCore.Mvc;

namespace ChapGiti.Api.Controllers;

[Route("api/admin/users")]
[RequireAdministrator]
public sealed class AdminUsersController(AdminUserService users) : ApiControllerBase
{
    [HttpGet]
    public async Task<IResult> List([FromQuery] string? q = "", [FromQuery] string? role = "",
        [FromQuery] string? verified = "", [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] bool oldestFirst = false) =>
        Respond(await users.List(q ?? "", role ?? "", verified ?? "", page, pageSize, oldestFirst));
    [HttpPost]
    public async Task<IResult> Create(CreateUserRequest body) => Respond(await users.Create(body.ToBody()));
    [HttpPut("{id:guid}/roles/admin")]
    public async Task<IResult> SetRole(string id, SetAdminRoleRequest body) => Respond(await users.SetAdministrator(id, body.Enabled));
}

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record SetAdminRoleRequest([property: System.Text.Json.Serialization.JsonRequired] bool Enabled);
