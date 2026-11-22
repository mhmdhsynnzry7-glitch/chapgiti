using System.Security.Cryptography;
using ChapGiti.Application.Notifications;
using ChapGiti.Domain.Orders;

namespace ChapGiti.Application.Features;

public sealed class OrderService(
    IOrderRepository repository,
    IUnitOfWork unitOfWork,
    SessionService sessions,
    PrintCatalogService catalog,
    CutTariffService tariffs,
    IPrivateFileStore files,
    ISmsNotifier sms,
    OrderQuoteService proposals,
    NotificationPublisher notifications,
    OrderSubmissionPolicy submission,
    OrderPdfFiles pdfs,
    OrderCorrectionService corrections,
    IOrderCorrectionRepository correctionRepository,
    IAdminOrderRepository adminOrders,
    OrderViewService views,
    ChapGiti.Application.Orders.IOrderInboxRepository inbox)
{
    public async Task<UseCaseResult> Quote(JsonObject body)
    {
        await sessions.Customer();
        await sessions.Limit("quote", 120);
        var pricing = await catalog.Pricing();
        await catalog.ValidateSelections(body);
        return UseCaseResult.Json(await tariffs.Estimate(pricing, pricing.Validate(body)));
    }

    public async Task<UseCaseResult> Create(JsonObject body, IUploadContent? initialFile = null)
    {
        await sessions.Limit("orders", 12);
        var customer = (await sessions.Customer())!;
        await proposals.RequireAvailable();
        var title = Input.Text(body, "title", 2, 160);
        var service = Input.Text(body, "service", 0, 60, "book-printing");
        var notes = Input.Text(body, "notes", 0, 3000);
        var email = Input.Text(body, "email", 0, 254);
        if (email != "" && !Regex.IsMatch(email, @"^[^\s@]+@[^\s@]+\.[^\s@]+$"))
            throw new UseCaseException(ErrorKind.Invalid, "ایمیل معتبر نیست.");
        if (body["phone"] != null && Input.Phone(body) != customer.Str("phone"))
            throw new UseCaseException(ErrorKind.Invalid, "شماره سفارش باید با شماره واردشده یکسان باشد.");
        var pricing = await catalog.Pricing();
        await catalog.ValidateSelections(Input.Object(body["spec"]));
        var spec = pricing.Validate(body["spec"]);
        spec.Remove("catalogSnapshot");
        spec.Remove("deliveryConditions");
        spec.Remove("quoteSpecifications");
        spec.Remove("primaryFileId");
        if (body["termsAccepted"] is not JsonValue consent || !consent.TryGetValue<bool>(out var accepted) || !accepted)
            throw new UseCaseException(ErrorKind.Invalid, "پذیرش شرایط ثبت سفارش و حریم خصوصی الزامی است.", code: "ORDER_TERMS_REQUIRED");
        var requiredPdf = submission.RequiresPdf(service);
        var prepared = initialFile == null ? null : await pdfs.Prepare(initialFile);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(new JsonObject
        {
            ["title"] = title,
            ["service"] = service,
            ["notes"] = notes,
            ["email"] = email,
            ["spec"] = spec.DeepClone(),
            ["initialFileHash"] = prepared == null ? null : Convert.ToHexString(SHA256.HashData(prepared.Bytes)),
            ["initialFileName"] = prepared?.Name
        }.ToJsonString())));
        var id = Input.Id(body);
        string code = "";
        var eventId = Input.NewId();
        var created = Input.Now();
        long total = 0;
        var written = new List<string>();
        Dictionary<string, object?>? old;
        try
        {
            old = await unitOfWork.Transaction(async () =>
        {
            var existing = await repository.LockExistingOrder(id);
            if (existing != null)
            {
                var oldSpec = JsonNode.Parse(existing.Str("spec")) as JsonObject;
                if (existing.Str("customer_id") != customer.Str("id") || existing.Str("phone") != customer.Str("phone") ||
                    oldSpec?["submissionFingerprint"]?.GetValue<string>() != fingerprint)
                    throw new UseCaseException(ErrorKind.Conflict, "شناسه درخواست قبلاً استفاده شده است.");
                return existing;
            }
            if (requiredPdf && prepared == null)
                throw new UseCaseException(ErrorKind.Invalid,
                "برای ثبت این خدمت ابتدا فایل PDF معتبر را انتخاب کنید.", code: "ORDER_PDF_REQUIRED");
            await catalog.LockForSubmission();
            await catalog.ValidateSelections(spec);
            var currentPricing = await catalog.Pricing();
            spec = currentPricing.Validate(spec);
            var quote = await tariffs.Estimate(currentPricing, spec, includeSnapshot: true);
            total = quote["total"]!.GetValue<long>();
            var saved = (JsonObject)spec.DeepClone();
            saved["catalogSnapshot"] = await catalog.Snapshot(spec);
            saved["customerNotes"] = notes;
            saved["priceBreakdown"] = quote.DeepClone();
            saved["requiresPdf"] = requiredPdf;
            saved["termsAcceptedAt"] = created;
            saved["submissionFingerprint"] = fingerprint;
            for (var attempt=0; attempt<5; attempt++)
            {
                code=TrackingCode.Generate();
                if (!await repository.TrackingCodeInUse(code)) break;
                if (attempt==4) throw new UseCaseException(ErrorKind.Unavailable,"ساخت کد پیگیری انجام نشد. دوباره تلاش کنید.");
            }
            await repository.InsertOrder(id, code, customer.Str("first_name") + " " + customer.Str("last_name"), customer.Str("phone"), email, title, service, saved.ToJsonString(), total, created, customer.Str("id"));
            if (prepared != null)
            {
                var primary = await pdfs.Save(id, prepared, null, written);
                saved["primaryFileId"] = primary.Id;
                await correctionRepository.SaveSpecification(id, saved.ToJsonString());
            }
            await repository.InsertInitialEvent(eventId, id, created);
            await notifications.Publish(new(ChapGiti.Domain.Notifications.NotificationEvent.RequestCreated, eventId, id, customer.Str("id"), ToAdministrators: true));
            return null;
        });
        }
        catch { pdfs.Cleanup(written); throw; }
        if (old != null)
            return UseCaseResult.Json(new
            {
                id = old["id"],
                code = old["code"],
                estimate = old["estimate"]
            });
        await sms.Notify(id, code, customer.Str("phone"), "new", eventId);
        return UseCaseResult.Json(new
        {
            id,
            code,
            estimate = total
        }, created: true);
    }

    public async Task<UseCaseResult> Track(JsonObject body)
    {
        await sessions.Limit("track", 25);
        var customer = (await sessions.Customer())!;
        var code = Input.NormalizeDigits(Input.Text(body, "code", 8, 30)).ToUpperInvariant();
        var phone = customer.Str("phone");
        if (body["phone"] != null && Input.Phone(body) != phone) throw new UseCaseException(ErrorKind.NotFound,"سفارش پیدا نشد.");
        var order = await repository.TrackOrder(code, phone, customer.Str("id")) ?? throw new UseCaseException(ErrorKind.NotFound, "سفارشی با این مشخصات پیدا نشد.");
        await AddCustomerAttention(order, customer.Str("id"));
        return await TrackingResult(order);
    }

    public async Task<UseCaseResult> CustomerOrders(int page, int pageSize, string group = "", string attention = "")
    {
        var customer = (await sessions.Customer())!;
        if (page is < 1 or > 100000 || pageSize is < 1 or > 50)
            throw new UseCaseException(ErrorKind.Invalid, "شماره صفحه یا تعداد سفارش‌ها معتبر نیست.");
        var statuses = OrderGroups.StatusFilter(group);
        var result = await inbox.Page(new(new("customer",customer.Str("id")),statuses,"",attention,(page-1)*pageSize,pageSize));
        return UseCaseResult.Json(new
        {
            items = result.Items,
            total = result.Total,
            page,
            pageSize
        });
    }

    public async Task<UseCaseResult> CustomerOrder(string id)
    {
        var customer = (await sessions.Customer())!;
        // Capture before loading the snapshot: a later concurrent event must not
        // be acknowledged merely because it arrived while details were loading.
        var observedSequence = await views.Latest(id, new NotificationRecipient("customer", customer.Str("id")));
        var order = await repository.CustomerOrder(id, customer.Str("id"))
            ?? throw new UseCaseException(ErrorKind.NotFound, "سفارش مورد نظر پیدا نشد.");
        // Only the owner's saved production specification is public to this account.
        // Historical pricing breakdown and internal/admin fields remain private.
        var saved = JsonNode.Parse(order.Str("spec")) as JsonObject;
        var specification = new JsonObject();
        foreach (var key in new[] { "pages", "quantity", "size", "paper", "color", "binding", "details", "customerNotes", "requiresPdf", "catalogSnapshot" })
            if (saved?[key] != null)
                specification[key] = saved[key]!.DeepClone();
        var finalConditions = OrderProductionReadiness.CurrentConditions(order, (await correctionRepository.Corrections(id)).LastOrDefault());
        if (finalConditions != null)
        {
            finalConditions = (JsonObject)finalConditions.DeepClone();
            foreach (var key in new[]{"issuedBy","acceptedBy","acceptanceId","specificationVersion"})
                finalConditions.Remove(key);
        }
        order.Remove("spec");
        await AddCustomerAttention(order, customer.Str("id"));
        return UseCaseResult.Json(new
        {
            order, finalConditions,
            events = await repository.TrackingEvents(id),
            specification,
            requiresBinding = OrderWorkflow.RequiresBinding(saved ?? new JsonObject()),
            files = (await correctionRepository.Files(id)).Select(file => new { file.Id, file.Name, file.Size, created_at = file.CreatedAt, file.RevisionId, file.FileVersion }),
            quotesAvailable = await proposals.Available(),
            quotes = await proposals.History(id),
            correctionsAvailable = await corrections.Available(),
            corrections = await corrections.History(id),
            approvedFileId = order.GetValueOrDefault("approved_file_id"),
            orderViewsAvailable = await views.Available(),
            viewSequence = observedSequence
        });
    }

    public async Task<UseCaseResult> Download(string id, bool preview = false)
    {
        var customer = (await sessions.Customer())!;
        var file = await repository.FindOwnedFile(id, customer.Str("id"))
            ?? throw new UseCaseException(ErrorKind.NotFound, "فایل پیدا نشد.");
        return UseCaseResult.File(await files.ReadPdf(file.Str("object_key")), "application/pdf", preview ? null : file.Str("name"), sandbox: true);
    }

    private async Task AddCustomerAttention(Dictionary<string,object?> order,string customerId)
    {
        var card=await inbox.Page(new(new("customer",customerId),"","","",0,1,Id:order.Str("id")));
        order["attention"]=card.Items.FirstOrDefault()?.GetValueOrDefault("attention");
    }

    private async Task<UseCaseResult> TrackingResult(Dictionary<string, object?> order)
    {
        var spec = JsonNode.Parse(order.Str("spec")) as JsonObject ?? new JsonObject();
        order.Remove("spec");
        return UseCaseResult.Json(new
        {
            order,
            requiresBinding = OrderWorkflow.RequiresBinding(spec),
            events = await repository.TrackingEvents(order.Str("id"))
        });
    }

    public async Task<UseCaseResult> Upload(string orderId, IUploadContent upload, string? revisionId = null)
    {
        var customer = await sessions.Customer();
        await sessions.Limit("upload", 20);
        if (await repository.FindOwnedOrder(orderId, upload.OrderCode, customer!.Str("id")) == null)
            throw new UseCaseException(ErrorKind.NotFound, "سفارش معتبر نیست.");
        var prepared = await pdfs.Prepare(upload);
        var written = new List<string>();
        ChapGiti.Application.Orders.OrderFileRecord saved;
        try
        {
            saved = await unitOfWork.Transaction(async () =>
            {
                var order = await repository.LockOwnedOrder(orderId, upload.OrderCode, customer!.Str("id")) ?? throw new UseCaseException(ErrorKind.NotFound, "سفارش معتبر نیست.");
                if (!new[] { "new", "review" }.Contains(order.Str("status")))
                    throw new UseCaseException(ErrorKind.Conflict, "ارسال فایل در این مرحله مجاز نیست.");
                var latest = (await correctionRepository.Corrections(orderId)).LastOrDefault();
                if (latest != null && latest.State != "requested")
                    throw new UseCaseException(ErrorKind.Conflict,
                    "فایل جدید فقط برای درخواست اصلاح باز قابل ارسال است؛ نتیجهٔ بازبینی را منتظر بمانید.", code: "CORRECTION_UPLOAD_CLOSED");
                if (revisionId != null && (latest == null || latest.Id != revisionId))
                    throw new UseCaseException(ErrorKind.Conflict,
                    "نسخهٔ اصلاحیه تغییر کرده است؛ اطلاعات سفارش را دوباره دریافت کنید.", code: "CORRECTION_VERSION_CONFLICT");
                if (latest == null && order.Str("information_request") != "")
                    await corrections.RequireAvailable();
                var file = await pdfs.Save(order.Str("id"), prepared, latest?.Id, written);
                await correctionRepository.SetApprovedFile(orderId, null);
                if (await adminOrders.UpdateOrder(order.Str("status"), order.GetValueOrDefault("final_price") == null ? null : order.Num("final_price"),
                    order.Str("admin_notes"), Input.Now(), orderId, order.Num("version")) != 1)
                    throw new UseCaseException(ErrorKind.Conflict, "سفارش هم‌زمان تغییر کرده است.", code: "ORDER_VERSION_CONFLICT");
                return file;
            });
        }
        catch
        {
            pdfs.Cleanup(written);
            throw;
        }
        return UseCaseResult.Json(new
        {
            saved.Id,
            saved.Name,
            saved.Size,
            saved.RevisionId,
            saved.FileVersion
        }, created: true);
    }
}
