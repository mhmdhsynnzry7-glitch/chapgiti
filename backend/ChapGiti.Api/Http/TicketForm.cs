using ChapGiti.Application.Common;
using ChapGiti.Application.Features;
using System.Text.Json.Nodes;

namespace ChapGiti.Api.Http;

internal static class TicketForm
{
    public static async Task<UseCaseResult> Submit(HttpRequest request, TicketService service, string id, bool admin, Dictionary<string,object?>? actor = null)
    {
        await service.AuthorizeReply(id,admin);
        if (!request.HasFormContentType) throw new UseCaseException(ErrorKind.UnsupportedMedia,"پیوست را در فرم ارسال کنید.");
        var form = await request.ReadFormAsync(request.HttpContext.RequestAborted);
        if (form.Files.Count == 0) throw new UseCaseException(ErrorKind.Invalid,"یک پیوست انتخاب کنید.");
        var body = new JsonObject { ["idempotencyKey"] = form["idempotencyKey"].ToString(), ["message"] = form["message"].ToString() };
        return await service.Reply(id, body, admin, actor, form.Files.Select(f=>(ChapGiti.Application.Abstractions.IUploadContent)new FormUploadContent(f,request.HttpContext.RequestAborted)).ToList());
    }
}
