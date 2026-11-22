using System.Text.Json.Nodes;
using ChapGiti.Api.Contracts;
using ChapGiti.Application.Features;
using Microsoft.AspNetCore.Mvc;

namespace ChapGiti.Api.Controllers;

[Route("api/customer")]
public sealed class CustomerAuthController(AccountService accounts, CustomerOtpService otp, SessionService sessions) : ApiControllerBase
{
    [HttpGet("session")]
    public async Task<IResult> Status() => Respond(await sessions.Status());
    [HttpPost("refresh")]
    public async Task<IResult> Refresh() => Respond(await sessions.Refresh());
    [HttpPost("start")]
    public async Task<IResult> Start(CustomerStartRequest body) => Respond(await accounts.StartCustomer(body.ToBody()));
    [HttpPost("login")]
    public async Task<IResult> Login(CustomerLoginRequest body) => Respond(await accounts.LoginCustomer(body.ToBody()));
    [HttpPost("otp/request")]
    public async Task<IResult> RequestCode(OtpRequest body) => Respond(await otp.Request(body.ToBody()));
    [HttpPost("otp/verify")]
    public async Task<IResult> VerifyCode(VerifyOtpRequest body) => Respond(await otp.VerifyRegistration(body.ToBody()));
    [HttpPost("registration/complete")]
    public async Task<IResult> CompleteRegistration(CompleteRegistrationRequest body) => Respond(await otp.CompleteRegistration(body.ToBody()));
    [HttpPost("phone/verify")]
    public async Task<IResult> VerifyPhone(VerifyOtpRequest body) => Respond(await otp.VerifyPhone(body.ToBody()));
    [HttpPost("password/verify")]
    public async Task<IResult> VerifyRecovery(VerifyOtpRequest body) => Respond(await otp.VerifyReset(body.ToBody()));
    [HttpPost("password/reset")]
    public async Task<IResult> ResetPassword(ResetPasswordRequest body) => Respond(await otp.ResetPassword(body.ToBody()));
    [HttpPost("logout")]
    public async Task<IResult> Logout() => Respond(await accounts.LogoutCustomer());
}
