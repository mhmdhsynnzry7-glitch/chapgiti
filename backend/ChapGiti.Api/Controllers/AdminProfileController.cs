using ChapGiti.Api.Contracts;
using ChapGiti.Api.Http;
using ChapGiti.Application.Features;
using Microsoft.AspNetCore.Mvc;

namespace ChapGiti.Api.Controllers;

[Route("api/admin/profile")]
[RequireAdministrator]
public sealed class AdminProfileController(AdministratorService administrators, AccountService accounts) : ApiControllerBase
{
    [HttpGet]
    public async Task<IResult> Me() => Respond(await accounts.GetAdmin());
    [HttpPut("email")]
    public async Task<IResult> Email(AdminEmailRequest body) => Respond(await administrators.ChangeEmail(body.ToBody()));
    [HttpPut("password")]
    public async Task<IResult> Password(AdminPasswordRequest body) => Respond(await administrators.ChangePassword(body.ToBody()));
}

[Route("api/admin/administrators")]
[RequireAdministrator]
public sealed class AdministratorsController(AdministratorService administrators) : ApiControllerBase
{
    [HttpGet]
    public async Task<IResult> List() => Respond(await administrators.List());
    [HttpPost]
    public async Task<IResult> Create(CreateAdministratorRequest body) => Respond(await administrators.Create(body.ToBody()));
}
