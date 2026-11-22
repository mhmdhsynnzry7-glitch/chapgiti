using ChapGiti.Application.Orders;
using ChapGiti.Domain.Orders;
using ChapGiti.Domain.Printing;

namespace ChapGiti.Application.Features;

public sealed class OrderCorrectionService(IOrderCorrectionRepository corrections, IOrderQuoteRepository quotes,
    OrderQuoteService commands, SessionService sessions, OrderPdfFiles pdfs)
{
    public Task<bool> Available() => corrections.SupportsCorrections();
    public async Task RequireAvailable()
    {
        if (!await Available()) throw new UseCaseException(ErrorKind.Unavailable,
            "گردش اصلاحیه هنوز روی این سرور فعال نشده است؛ migration 008 باید جداگانه بررسی و اجرا شود.", code: "ORDER_CORRECTIONS_UNAVAILABLE");
    }
    public async Task<object[]> History(string orderId) => (await corrections.Corrections(orderId)).Select(c => (object)new {
        c.Id,c.Revision,c.State,c.RequestText,c.RequiresNewPdf,c.RequestedAt,c.Legacy,c.BaseFileId,
        c.SubmissionText,c.SubmittedAt,c.FileId,c.ReviewReason,c.ReviewedAt,c.ApprovedFileId,c.Version
    }).ToArray();

    public async Task<UseCaseResult> ApproveFile(string orderId, JsonObject body)
    {
        var admin = (await sessions.Admin())!;
        var actor = "admin:" + admin.Str("email");
        return await commands.ExecuteCommand(orderId, body, "approve-file", actor, null, async order =>
        {
            OrderCorrectionWorkflow.RequireRequestable(order.Str("status"));
            if ((await corrections.Corrections(orderId)).Count != 0)
                throw Conflict("فایل اصلاحیه باید در بررسی همان نسخه تأیید شود.", "ORDER_APPROVED_FILE_INVALID");
            var fileId = Input.Id(body, "fileId");
            var files = await corrections.Files(orderId);
            var selected = files.SingleOrDefault(f => f.Id == fileId);
            if (fileId != OrderProductionReadiness.PrimaryFileId(order, files))
                throw Conflict("برای تأیید تولید، فایل اصلی این سفارش را انتخاب کنید.", "ORDER_APPROVED_FILE_INVALID");
            if (selected == null) throw Conflict("فایل انتخاب‌شده متعلق به سفارش نیست.", "ORDER_FILE_REQUIRED");
            await pdfs.ValidateStored(selected);
            if (order.Str("approved_file_id") != fileId)
            {
                var spec = JsonNode.Parse(order.Str("spec"))!.AsObject();
                if ((spec["deliveryConditions"] as JsonArray)?.LastOrDefault() is JsonObject previous)
                {
                    previous["supersededAt"] = Input.Now();
                    await corrections.SaveSpecification(orderId, spec.ToJsonString());
                }
            }
            await corrections.SetApprovedFile(orderId, fileId);
            await commands.Move(order, order.Str("status"), Price(order), "فایل نسخهٔ " + selected.FileVersion + " برای چاپ تأیید شد.", actor, Input.Now());
        });
    }
    public async Task<UseCaseResult> Request(string orderId, JsonObject body)
    {
        var admin = (await sessions.Admin())!;
        Only(body, "message", "reason", "requiresFile");
        var instructions = Input.Text(body,"message",2,650);
        if (body["requiresFile"] is not JsonValue flag || !flag.TryGetValue<bool>(out var required) || !required)
            throw new UseCaseException(ErrorKind.Invalid,"برای اصلاح فایل، PDF جایگزین الزامی است.",code:"CORRECTION_PDF_FLAG_REQUIRED");
        var reason = Input.Text(body, "reason", 2, 200);
        var message = reason + "\n" + instructions;
        await RequireAvailable();
        return await commands.ExecuteCommand(orderId,body,"request-correction","admin:"+admin.Str("email"),null,async order => {
            OrderCorrectionWorkflow.RequireRequestable(order.Str("status"));
            if (order.Str("customer_id")=="") throw Conflict("برای درخواست اصلاح، سفارش باید مالک مشخص داشته باشد.","ORDER_OWNER_REQUIRED");
            var files = await corrections.Files(orderId);
            var history = await corrections.Corrections(orderId);
            foreach (var old in history.Where(c=>OrderCorrectionWorkflow.Active(c.State)))
                await Save(old,old with {State="superseded",Version=old.Version+1});
            var baseline = (history.LastOrDefault()?.FileId is string previous ? files.SingleOrDefault(f=>f.Id==previous) : null) ??
                files.SingleOrDefault(f=>f.Id==order.Str("approved_file_id")) ??
                files.SingleOrDefault(f=>f.Id==OrderProductionReadiness.PrimaryFileId(order,files));
            var next = new OrderCorrection(Input.Id(body),orderId,history.Count==0?1:history.Max(c=>c.Revision)+1,
                "requested",message,required,admin.Str("id"),Input.Now(),false,baseline?.Id);
            await corrections.InsertCorrection(next);
            await corrections.SetApprovedFile(orderId,null);
            await quotes.SetInformation(orderId,message,required);
            await commands.SupersedeSentQuotes(orderId);
            await commands.Move(order,"review",Price(order),"درخواست اصلاح نسخهٔ "+next.Revision+": "+message,"admin:"+admin.Str("email"),Input.Now());
        });
    }

    public async Task<UseCaseResult> Submit(string orderId, JsonObject body)
    {
        var customer = (await sessions.Customer())!;
        Only(body,"message","revisionId","revisionVersion","fileId");
        var message = Input.Text(body,"message",0,3000);
        await RequireAvailable();
        return await commands.ExecuteCommand(orderId,body,"submit-correction","customer:"+customer.Str("id"),customer.Str("id"),async order=>{
            OrderCorrectionWorkflow.RequireRequestable(order.Str("status"));
            var latest = await Latest(orderId,body);
            OrderCorrectionWorkflow.RequireSubmission(latest.State);
            var files = await corrections.Files(orderId);
            var selectedId = Input.Text(body,"fileId",0,36);
            var selected = selectedId=="" ? files.LastOrDefault(f=>f.RevisionId==latest.Id) : files.SingleOrDefault(f=>f.Id==selectedId);
            if (selectedId!="" && selected==null) throw Conflict("فایل انتخاب‌شده متعلق به این سفارش نیست.","CORRECTION_FILE_INVALID");
            if (latest.RequiresNewPdf && (selected==null || selected.RevisionId!=latest.Id))
                throw Conflict("برای این اصلاحیه باید یک PDF جدید پس از درخواست چاپخانه بارگذاری کنید؛ فایل قبلی کافی نیست.","CORRECTION_NEW_PDF_REQUIRED");
            var newest = files.LastOrDefault(f => f.RevisionId == latest.Id);
            if (newest != null && selected?.Id != newest.Id)
                throw Conflict("آخرین فایل بارگذاری‌شدهٔ این اصلاحیه را انتخاب کنید.", "CORRECTION_FILE_STALE");
            var effective = selected ?? files.SingleOrDefault(f=>f.Id==latest.BaseFileId);
            await ValidateRequiredFile(order,effective);
            await Save(latest,latest with {State="submitted",SubmissionText=message,SubmittedByCustomer=customer.Str("id"),
                SubmittedAt=Input.Now(),FileId=selected?.Id,Version=latest.Version+1});
            // Keep the outstanding request until an administrator reviews it.
            await commands.Move(order,"review",Price(order),"اصلاحیهٔ نسخهٔ "+latest.Revision+" دریافت شد و منتظر بازبینی چاپخانه است.","customer:"+customer.Str("id"),Input.Now());
        });
    }

    public async Task<UseCaseResult> Review(string orderId, JsonObject body)
    {
        var admin = (await sessions.Admin())!;
        Only(body,"revisionId","revisionVersion","decision","reason");
        var decision = Input.Text(body,"decision",1,12); var reason = Input.Text(body,"reason",0,900);
        await RequireAvailable();
        return await commands.ExecuteCommand(orderId,body,decision=="approve"?"approve-correction":"reject-correction","admin:"+admin.Str("email"),null,async order=>{
            OrderCorrectionWorkflow.RequireRequestable(order.Str("status"));
            var latest = await Latest(orderId,body);
            OrderCorrectionWorkflow.RequireReview(latest.State,decision,reason);
            var approved = decision=="approve";
            var files = await corrections.Files(orderId);
            var selected = files.SingleOrDefault(f=>f.Id==(latest.FileId??latest.BaseFileId));
            if (approved)
            {
                if (latest.RequiresNewPdf && (selected==null || selected.RevisionId!=latest.Id))
                    throw Conflict("فایل جدید این اصلاحیه معتبر نیست.","CORRECTION_NEW_PDF_REQUIRED");
                await ValidateRequiredFile(order,selected);
            }
            await Save(latest,latest with {State=approved?"approved":"rejected",ReviewedByAdmin=admin.Str("id"),
                ReviewReason=reason,ReviewedAt=Input.Now(),ApprovedFileId=approved?selected?.Id:null,Version=latest.Version+1});
            await corrections.SetApprovedFile(orderId,approved?selected?.Id:null);
            var status="review";
            if (approved)
            {
                await quotes.SetInformation(orderId,null,false);
                var quote=(await quotes.Quotes(orderId)).LastOrDefault();
                if(order.Num("workflow_version")>=2 && quote?.Str("state")=="accepted" && quote.Num("amount_toman")==Price(order) && quote.Str("accepted_by_customer")==order.Str("customer_id")) status="confirmed";
            }
            await commands.Move(order,status,Price(order),approved?"اصلاحیهٔ نسخهٔ "+latest.Revision+" تأیید شد.":"رد اصلاحیهٔ نسخهٔ "+latest.Revision+": "+reason,"admin:"+admin.Str("email"),Input.Now());
        });
    }

    private async Task<OrderCorrection> Latest(string id,JsonObject body)
    {
        var latest=(await corrections.Corrections(id)).LastOrDefault() ?? throw Conflict("درخواست اصلاحی وجود ندارد.","CORRECTION_NOT_FOUND");
        if ((body.ContainsKey("revisionId") && Input.Id(body,"revisionId")!=latest.Id) ||
            (body.ContainsKey("revisionVersion") && Input.Number(body,"revisionVersion",1,int.MaxValue)!=latest.Version))
            throw Conflict("نسخهٔ اصلاحیه تغییر کرده است؛ آخرین اطلاعات را دریافت کنید.","CORRECTION_VERSION_CONFLICT");
        return latest;
    }
    private async Task Save(OrderCorrection previous,OrderCorrection next)
    {
        if(!await corrections.SaveCorrection(previous,next)) throw Conflict("اصلاحیه هم‌زمان تغییر کرده است.","CORRECTION_VERSION_CONFLICT");
    }
    private async Task ValidateRequiredFile(Dictionary<string,object?> order,OrderFileRecord? selected)
    {
        if(selected==null)
            throw Conflict("مدرک PDF لازم برای این خدمت در دسترس نیست.","ORDER_FILE_REQUIRED");
        if(selected!=null) await pdfs.ValidateStored(selected);
    }
    private static void Only(JsonObject body,params string[] fields)
    {
        var allowed=fields.Concat(["id","idempotencyKey","expectedVersion","version"]).ToHashSet(StringComparer.Ordinal);
        if(body.Any(pair=>!allowed.Contains(pair.Key))) throw new UseCaseException(ErrorKind.Invalid,"فیلد ناشناخته یا غیرقابل‌تغییر در درخواست اصلاحیه وجود دارد.",code:"CORRECTION_PAYLOAD_INVALID");
    }
    private static long? Price(Dictionary<string,object?> order)=>order.GetValueOrDefault("final_price")==null?null:order.Num("final_price");
    private static UseCaseException Conflict(string message,string code)=>new(ErrorKind.Conflict,message,code:code);
}
