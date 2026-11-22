using ChapGiti.Api.Http;
using ChapGiti.Application.Features;
using Microsoft.AspNetCore.Mvc;
namespace ChapGiti.Api.Controllers;

[Route("api/admin/activity")]
[RequireAdministrator]
public sealed class CustomerActivityController(CustomerActivityService activity) : ApiControllerBase
{
    [HttpGet]
    public async Task<IResult> List([FromQuery]string? q="",[FromQuery]string? type="",[FromQuery]string? from="",[FromQuery]string? until="",[FromQuery]int page=1,[FromQuery]int pageSize=20, [FromQuery] bool oldestFirst = false) =>
        Respond(await activity.List(q??"",type??"",from??"",until??"",page,pageSize, oldestFirst));
}
