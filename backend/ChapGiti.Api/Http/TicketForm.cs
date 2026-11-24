using ChapGiti.Application.Abstractions;
using ChapGiti.Application.Common;
using ChapGiti.Application.Features;
using System.Text.Json.Nodes;

namespace ChapGiti.Api.Http;

internal static class TicketForm
{
    public static async Task<UseCaseResult> Create(HttpRequest request, TicketService service)
    {
        await service.AuthorizeCreate();
        var (form, files) = await Read(request);
        var body = Message(form);
        body["subject"] = form["subject"].ToString();
        body["orderId"] = form["orderId"].ToString();
        body["consultation"] = form["consultation"].ToString() == "true";
        body["website"] = form["website"].ToString();
        return await service.Create(body, files);
    }

    public static async Task<UseCaseResult> Submit(HttpRequest request, TicketService service, string id, bool admin, Dictionary<string, object?>? actor = null)
    {
        await service.AuthorizeReply(id, admin);
        var (form, files) = await Read(request);
        return await service.Reply(id, Message(form), admin, actor, files);
    }

    private static JsonObject Message(IFormCollection form) => new()
    {
        ["message"] = form["message"].ToString(),
        ["idempotencyKey"] = form["idempotencyKey"].ToString()
    };

    private static async Task<(IFormCollection, List<IUploadContent>)> Read(HttpRequest request)
    {
        if (!request.HasFormContentType)
            throw new UseCaseException(ErrorKind.UnsupportedMedia, "پیوست را در فرم ارسال کنید.");
        var cancellation = request.HttpContext.RequestAborted;
        var form = await request.ReadFormAsync(cancellation);
        if (form.Files.Count == 0)
            throw new UseCaseException(ErrorKind.Invalid, "یک پیوست انتخاب کنید.");
        return (form, form.Files.Select(f => (IUploadContent)new FormUploadContent(f, cancellation)).ToList());
    }
}
