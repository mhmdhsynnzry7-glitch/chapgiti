using System.Text.Json.Nodes;
using ChapGiti.Api.Contracts;
using ChapGiti.Api.Http;
using ChapGiti.Application.Features;
using Microsoft.AspNetCore.Mvc;

namespace ChapGiti.Api.Controllers;

[Route("api")]
public sealed class OrdersController(OrderService orders) : ApiControllerBase
{
    [HttpPost("quote")]
    public async Task<IResult> Quote(QuoteRequest body) => Respond(await orders.Quote(body.ToBody()));
    [HttpPost("orders")]
    [Consumes("application/json")]
    public async Task<IResult> Create(JsonObject body) => Respond(await orders.Create(body));
    [HttpPost("orders")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(16 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 16 * 1024 * 1024)]
    public async Task<IResult> CreateWithPdf()
    {
        var form = await Request.ReadFormAsync(Request.HttpContext.RequestAborted);
        if(form.Count!=1 || !form.ContainsKey("payload") || form["payload"].Count!=1 || form.Files.Count!=1 || form.Files[0].Name!="file")
            throw new ChapGiti.Application.Common.UseCaseException(ChapGiti.Application.Common.ErrorKind.Invalid,"اطلاعات سفارش و یک فایل PDF را ارسال کنید.");
        var payload = JsonNode.Parse(form["payload"].ToString()) as JsonObject
            ?? throw new ChapGiti.Application.Common.UseCaseException(ChapGiti.Application.Common.ErrorKind.Invalid,"اطلاعات سفارش معتبر نیست.");
        return Respond(await orders.Create(payload,new FormUploadContent(form.Files[0],Request.HttpContext.RequestAborted)));
    }
    [HttpPost("track")]
    public async Task<IResult> Track(JsonObject body) => Respond(await orders.Track(body));
    [HttpPost("orders/{id:guid}/files")]
    [RequestSizeLimit(16 * 1024 * 1024)]
    public async Task<IResult> Upload(string id,[FromQuery] string? revisionId=null) => Respond(await orders.Upload(id, new HttpUploadContent(Request),revisionId));
}
