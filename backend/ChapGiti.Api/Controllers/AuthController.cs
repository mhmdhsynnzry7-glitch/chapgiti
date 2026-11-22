using System.Text.Json.Nodes;
using ChapGiti.Api.Contracts;
using ChapGiti.Application.Features;
using Microsoft.AspNetCore.Mvc;

namespace ChapGiti.Api.Controllers;

[Route("api/auth")]
public sealed class AuthController(AccountService accounts, SessionService sessions) : ApiControllerBase
{
    [HttpGet("me")]
    public async Task<IResult> Me() => Respond(await accounts.GetAdmin());
    [HttpPost("login")]
    public async Task<IResult> Login(AdminLoginRequest body) => Respond(await accounts.LoginAdmin(body.ToBody()));
    [HttpGet("session")]
    public async Task<IResult> Status() => Respond(await sessions.Status(true));
    [HttpPost("logout")]
    public async Task<IResult> Logout() => Respond(await accounts.LogoutAdmin());
    [HttpPost("refresh")]
    public async Task<IResult> Refresh() => Respond(await sessions.Refresh(true));
}
