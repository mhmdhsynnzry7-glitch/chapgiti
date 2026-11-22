using ChapGiti.Api.Http;
using ChapGiti.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace ChapGiti.Api.Controllers;

[ApiController]
[RequestSizeLimit(100_000)]
public abstract class ApiControllerBase : ControllerBase
{
    protected IResult Respond(UseCaseResult result) => result.ToHttp(HttpContext);
    protected Dictionary<string, object?> Administrator =>
        (Dictionary<string, object?>)HttpContext.Items[typeof(RequireAdministratorAttribute)]!;
}
