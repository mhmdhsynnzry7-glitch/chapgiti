using System.Text.Json.Nodes;
using ChapGiti.Api.Http;
using ChapGiti.Application.Features;
using Microsoft.AspNetCore.Mvc;

namespace ChapGiti.Api.Controllers;

[Route("api/customer/orders/{id:guid}")]
public sealed class CustomerOrderQuotesController(OrderQuoteService quotes, OrderCorrectionService corrections, OrderViewService views) : ApiControllerBase
{
    [HttpPost("delivery-terms/request-amendment")]
    public async Task<IResult> AmendTerms(string id, JsonObject body) => Respond(await quotes.FinalConditions(id, body, false, amend: true));
    [HttpPost("delivery-terms/accept")]
    public async Task<IResult> AcceptTerms(string id, JsonObject body) => Respond(await quotes.FinalConditions(id, body, true));
    [HttpPost("quote/accept")]
    public async Task<IResult> Accept(string id,JsonObject body)=>Respond(await quotes.CustomerDecision(id,"accept",body));
    [HttpPost("quote/request-revision")]
    public async Task<IResult> Revision(string id,JsonObject body)=>Respond(await quotes.CustomerDecision(id,"request-revision",body));
    [HttpPost("quote/decline")]
    public async Task<IResult> Decline(string id,JsonObject body)=>Respond(await quotes.CustomerDecision(id,"decline",body));
    [HttpPost("information")]
    [HttpPost("corrections/submit")]
    public async Task<IResult> Information(string id,JsonObject body)=>Respond(await corrections.Submit(id,body));
    [HttpPost("view")]
    public async Task<IResult> ViewOrder(string id,JsonObject body)=>Respond(await views.Mark(id,false,body));
}

[Route("api/admin/orders/{id:guid}")]
[RequireAdministrator]
public sealed class AdminOrderQuotesController(OrderQuoteService quotes, OrderCorrectionService corrections, OrderViewService views) : ApiControllerBase
{
    [HttpPost("files/approve")]
    public async Task<IResult> ApproveFile(string id, JsonObject body) => Respond(await corrections.ApproveFile(id, body));
    [HttpPost("delivery-terms")]
    public async Task<IResult> SendTerms(string id, JsonObject body) => Respond(await quotes.FinalConditions(id, body, false));
    [HttpPost("quotes")]
    public async Task<IResult> Send(string id,JsonObject body)=>Respond(await quotes.Send(id,body,Administrator));
    [HttpPost("start-printing")]
    public async Task<IResult> Start(string id,JsonObject body)=>Respond(await quotes.StartPrinting(id,body,Administrator));
    [HttpPost("request-information")]
    [HttpPost("corrections")]
    public async Task<IResult> Information(string id,JsonObject body)=>Respond(await corrections.Request(id,body));
    [HttpPost("corrections/review")]
    public async Task<IResult> Review(string id,JsonObject body)=>Respond(await corrections.Review(id,body));
    [HttpPost("view")]
    public async Task<IResult> ViewOrder(string id,JsonObject body)=>Respond(await views.Mark(id,true,body));
}
