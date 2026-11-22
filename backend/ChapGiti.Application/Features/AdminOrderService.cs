namespace ChapGiti.Application.Features;

public sealed class AdminOrderService(
    IAdminOrderRepository repository,
    IUnitOfWork unitOfWork,
    SessionService sessions,
    IPrivateFileStore files,
    ISmsNotifier sms,
    OrderQuoteService proposals,
    NotificationPublisher notifications,
    OrderProductionReadiness production,
    OrderCorrectionService corrections,
    IOrderCorrectionRepository correctionRepository,
    OrderViewService views,
    ITicketRepository tickets,
    ChapGiti.Application.Orders.IOrderInboxRepository inbox)
{
    public async Task<UseCaseResult> Overview(Dictionary<string, object?> user)
    {
        var counts = await repository.StatusCounts();
        var summary = await repository.Summary();
        var recent = await repository.RecentOrders();
        var trend = await repository.DailyTrend(DateTime.UtcNow.AddDays(-30));
        return UseCaseResult.Json(new
        {
            counts,
            summary,
            recent,
            trend,
            user = new
            {
                name = user.GetValueOrDefault("username") ?? user["email"],
                email = user["email"]
            }
        });
    }

    public async Task<UseCaseResult> List(string search, string status, string pageValue, int pageSize = 20, bool oldestFirst = false, string attention = "")
    {
        if (pageSize is < 1 or > 50) throw new UseCaseException(ErrorKind.Invalid, "تعداد در صفحه معتبر نیست.");
        if (search.Length > 100)
            search = search[..100];
        if (status != "" && !OrderWorkflow.Labels.ContainsKey(status))
            throw new UseCaseException(ErrorKind.Invalid, "وضعیت نامعتبر است.");
        var page = int.TryParse(pageValue, out var number) ? Math.Clamp(number, 1, 100000) : 1;
        var reader = (await sessions.Admin())!;
        var result = await inbox.Page(new(new("admin",reader.Str("id")),status,Input.NormalizeDigits(search.Trim()),attention,(page-1)*pageSize,pageSize,oldestFirst));
        return UseCaseResult.Json(new
        {
            orders = result.Items,
            total = result.Total,
            page
        });
    }

    public async Task<UseCaseResult> Export()
    {
        var orders = await repository.ExportOrders();
        static string Cell(object? value)
        {
            var text = Convert.ToString(value) ?? "";
            if (Regex.IsMatch(text, @"^[=+@\-\t\r]"))
                text = "'" + text;
            return '"' + text.Replace(new string('"', 1), new string('"', 2)) + '"';
        }
        var lines = new List<string> { string.Join(",", new[] { "کد", "مشتری", "همراه", "عنوان", "وضعیت", "برآورد", "قیمت نهایی", "تاریخ" }.Select(Cell)) };
        foreach (var row in orders)
            lines.Add(string.Join(",", new[] { row["code"], row["customer"], row["phone"], row["title"], OrderWorkflow.Labels.GetValueOrDefault(row.Str("status")), row["estimate"], row["final_price"], row["created_at"] }.Select(Cell)));

        return UseCaseResult.Text("\uFEFF" + string.Join("\r\n", lines), "text/csv; charset=utf-8", "orders.csv");
    }

    public async Task<UseCaseResult> Get(string id)
    {
        var admin = (await sessions.Admin())!;
        var observedSequence=await views.Latest(id,new ChapGiti.Application.Notifications.NotificationRecipient("admin",admin.Str("id")));
        var order = await repository.FindOrder(id) ?? throw new UseCaseException(ErrorKind.NotFound, "سفارش پیدا نشد.");
        var finalConditions = OrderProductionReadiness.CurrentConditions(order, (await correctionRepository.Corrections(id)).LastOrDefault());
        var readiness = await production.Assess(order);
        var card = await inbox.Page(new(new("admin",admin.Str("id")),"","","",0,1,Id:id));
        order["attention"] = card.Items.FirstOrDefault()?.GetValueOrDefault("attention");
        order["spec"] = JsonNode.Parse(order.Str("spec"));
        return UseCaseResult.Json(new
        {
            order,
            conversations = await tickets.SupportsOrderLinks() ? await tickets.OrderConversations(id) : [],
            files = (await correctionRepository.Files(id)).Select(file=>new {file.Id,file.Name,file.Size,created_at=file.CreatedAt,file.RevisionId,file.FileVersion}),
            events = await repository.OrderEvents(id),
            sms = await repository.OrderSms(id),
            allowedTransitions = OrderWorkflow.AdministratorActions(order.Str("status"), OrderWorkflow.RequiresBinding((JsonObject)order["spec"]!), order.Num("workflow_version") >= 2),
            quotesAvailable = await proposals.Available(),
            quotes = await proposals.History(id, true),
            notesAvailable = await repository.SupportsOrderNotes(),
            internalNotes = await repository.InternalNotes(id),
            correctionsAvailable = await corrections.Available(),
            corrections = await corrections.History(id),
            finalConditions,
            productionReadiness = readiness,
            orderViewsAvailable = await views.Available(),
            viewSequence = observedSequence
        });
    }

    public Task<UseCaseResult> Open(string id, Dictionary<string, object?> user) => unitOfWork.Transaction(async () =>
    {
        var order = await repository.LockOrder(id) ?? throw new UseCaseException(ErrorKind.NotFound, "سفارش پیدا نشد.");
        if (order.Str("status") == "new")
        {
            var now = Input.Now();
            var changed = await repository.UpdateOrder("review", order.GetValueOrDefault("final_price") == null ? null : order.Num("final_price"), order.Str("admin_notes"), now, id, order.Num("version"));
            if (changed != 1) throw new UseCaseException(ErrorKind.Conflict, "سفارش تغییر کرده است؛ دوباره باز کنید.");
            await repository.InsertEvent(Input.NewId(), id, "review", "بررسی سفارش توسط چاپخانه آغاز شد.", user.Str("email"), now);
            await sessions.Audit(user.Str("email"), "شروع بررسی سفارش " + order.Str("code"));
        }
        return await Get(id);
    });

    public async Task<UseCaseResult> AddNote(string orderId, JsonObject body, Dictionary<string, object?> user)
    {
        var id = Input.Id(body);
        var note = Input.Text(body, "note", 1, 5000);
        if (!await repository.SupportsOrderNotes())
            throw new UseCaseException(ErrorKind.Unavailable, "تاریخچهٔ یادداشت اجرایی هنوز روی این سرور فعال نشده است.", code: "ORDER_NOTES_UNAVAILABLE");
        await unitOfWork.Transaction(async () => {
            if (await repository.LockOrder(orderId) == null) throw new UseCaseException(ErrorKind.NotFound, "سفارش پیدا نشد.");
            var old = await repository.LockInternalNote(id);
            if (old != null) {
                if (old.Str("order_id") != orderId || old.Str("note") != note || old.Str("actor") != user.Str("email"))
                    throw new UseCaseException(ErrorKind.Conflict, "شناسهٔ یادداشت قبلاً استفاده شده است.");
                return false;
            }
            await repository.InsertInternalNote(id, orderId, note, user.Str("email"), Input.Now());
            return true;
        });
        return UseCaseResult.Json(new { ok=true });
    }

    public async Task<UseCaseResult> Update(string id, JsonObject body, Dictionary<string, object?> user)
    {
        if ((await repository.FindOrder(id))?.Num("workflow_version") >= 2) return await proposals.Advance(id, body, user);
        var status = Input.Text(body, "status", 1, 40);
        var note = Input.Text(body, "note", 0, 900);
        var version = Input.Number(body, "version", 1, int.MaxValue);
        var now = Input.Now();
        var eventId = Input.NewId();
        var order = await unitOfWork.Transaction(async () =>
        {
            var current = await repository.LockOrder(id) ?? throw new UseCaseException(ErrorKind.NotFound, "سفارش پیدا نشد.");
            if (current.Num("workflow_version") >= 2) throw new UseCaseException(ErrorKind.Conflict, "گردش درخواست تغییر کرده است؛ صفحه را تازه کنید.");
            if (current.Num("version") != version) throw new UseCaseException(ErrorKind.Conflict, "سفارش تغییر کرده است؛ صفحه را تازه کنید.");
            if (body.ContainsKey("adminNotes") && Input.Text(body,"adminNotes",0,5000) != current.Str("admin_notes"))
                throw new UseCaseException(ErrorKind.Invalid, "یادداشت اجرایی را از بخش یادداشت‌ها ثبت کنید؛ متن قبلی بازنویسی نمی‌شود.");
            long? stored = current.GetValueOrDefault("final_price") == null ? null : current.Num("final_price");
            long? final = body.ContainsKey("finalPrice") ? body["finalPrice"] == null ? null : Input.Number(body,"finalPrice",0,100000000000) : stored;
            if (current.Str("status") is not ("new" or "review") && !(current.Str("status") == "confirmed" && stored == null && status == "printing") && final != stored)
                throw new UseCaseException(ErrorKind.Conflict, "قیمت تأییدشده پس از ورود به تولید قابل تغییر نیست.");
            OrderWorkflow.ValidateChange(current.Str("status"), status, final, OrderWorkflow.RequiresBinding(JsonNode.Parse(current.Str("spec"))!.AsObject()), note, proposalWorkflow: false);
            if(status=="printing" && current.Str("status")!="printing") await production.Ensure(current,final);
            if (status == current.Str("status") && final == stored && note == "") return current;
            var changed = await repository.UpdateOrder(status, final, current.Str("admin_notes"), now, id, version);
            if (changed == 0)
                throw new UseCaseException(ErrorKind.Conflict, "سفارش توسط کاربر دیگری تغییر کرده؛ صفحه را دوباره بارگذاری کنید.");
            var message = status == "cancelled" && status != current.Str("status") ? "رد سفارش: " + note
                : status == "printing" && current.Str("status") is "review" or "confirmed" ? "سفارش تأیید و وارد مرحلهٔ چاپ شد." + (note == "" ? "" : " " + note)
                : note == "" ? OrderWorkflow.Labels[status] : note;
            await repository.InsertEvent(eventId, id, status, message, user.Str("email"), now);
            if (status != current.Str("status"))
                await notifications.Status(status, eventId, id, current.Str("customer_id"));
            await sessions.Audit(user.Str("email"), "ویرایش سفارش " + current.Str("code"));
            return current;
        });
        if (status != order.Str("status"))
            await sms.Notify(id, order.Str("code"), order.Str("phone"), status, eventId);
        return UseCaseResult.Json(new
        {
            ok = true
        });
    }

    public async Task<UseCaseResult> Download(string id,bool preview=false)
    {
        var row = await repository.FindFile(id) ?? throw new UseCaseException(ErrorKind.NotFound, "فایل پیدا نشد.");
        return UseCaseResult.File(await files.ReadPdf(row.Str("object_key")), "application/pdf", preview?null:row.Str("name"), sandbox: true);
    }

    public async Task<UseCaseResult> Customers()
    {
        var rows = await repository.CustomerTotals();
        var profiles = await repository.CustomerProfiles();
        var customers = rows.ToDictionary(x => x.Str("phone"));
        foreach (var profile in profiles)
            if (!customers.ContainsKey(profile.Str("phone")))
                customers[profile.Str("phone")] = new() { { "phone", profile["phone"] }, { "name", profile.Str("first_name") + " " + profile.Str("last_name") }, { "email", "" }, { "orders", 0 }, { "value", 0 }, { "last_order", profile["updated_at"] } };
        return UseCaseResult.Json(customers.Values.OrderByDescending(x => x.Str("last_order")).Take(500));
    }

    public async Task<UseCaseResult> Messages()
    {
        return UseCaseResult.Json(await repository.Messages());
    }

    public async Task<UseCaseResult> Sms()
    {
        return UseCaseResult.Json(await repository.SmsLogs());
    }

    public async Task<UseCaseResult> Audit()
    {
        return UseCaseResult.Json(await repository.AuditLogs());
    }

    public async Task<UseCaseResult> MarkMessageRead(string id)
    {
        await repository.MarkMessageRead(id);
        return UseCaseResult.Json(new
        {
            ok = true
        });
    }
}
