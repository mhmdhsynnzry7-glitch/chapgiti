namespace ChapGiti.Application.Features;

public sealed class TicketService(
    ITicketRepository repository,
    IPrivateFileStore storage,
    TicketAttachmentPolicy filePolicy,
    IOrderRepository orders,
    IUnitOfWork unitOfWork,
    SessionService sessions,
    NotificationPublisher notifications,
    INotificationRepository recipients,
    ChapGiti.Application.Realtime.CommittedUpdates updates)
{
    private async Task RequireMessaging()
    {
        if (!await repository.SupportsMessageReads())
            throw new UseCaseException(ErrorKind.Unavailable, "ارسال و خواندن پیام تازه پس از فعال‌سازی نسخهٔ جدید پشتیبانی در دسترس است.", code:"SUPPORT_SCHEMA_PENDING");
    }

    public async Task<UseCaseResult> Create(JsonObject body)
    {
        var customer = (await sessions.Customer())!;
        await RequireMessaging();
        await sessions.Limit("ticket-create", 12);
        var subject = Input.Text(body, "subject", 3, 160);
        var message = Input.Text(body, "message", 10, 5000);
        if (Input.Text(body, "website", 0, 100) != "")
            throw new UseCaseException(ErrorKind.Invalid, "درخواست معتبر نیست.");
        var id = body["idempotencyKey"] == null ? Input.NewId() : Input.Id(body);
        var orderId = Input.Text(body,"orderId",0,36);
        var consultation = Input.Flag(body,"consultation");
        if (consultation && orderId != "") throw new UseCaseException(ErrorKind.Invalid,"مشاورهٔ عمومی به سفارش متصل نمی‌شود.");
        if (orderId != "" && !Guid.TryParse(orderId,out _)) throw new UseCaseException(ErrorKind.Invalid,"سفارش انتخاب‌شده معتبر نیست.");
        var resolvedId = id;
        var duplicate = await unitOfWork.Transaction(async () =>
        {
            if (orderId != "") {
                if (await orders.CustomerOrder(orderId,customer!.Str("id")) == null) throw new UseCaseException(ErrorKind.NotFound,"سفارش پیدا نشد.");
                if (!await repository.SupportsOrderLinks()) throw new UseCaseException(ErrorKind.Unavailable,"گفت‌وگوی مرتبط با سفارش هنوز روی این سرور فعال نشده است؛ درخواست عمومی ثبت کنید.",code:"ORDER_SUPPORT_UNAVAILABLE");
            }
            var old = await repository.LockExistingTicket(id);
            if (old != null)
            {
                if (old.Str("customer_id") != customer!.Str("id") || old.Str("subject") != subject || old.Str("message") != message || old.Str("order_id") != orderId)
                    throw new UseCaseException(ErrorKind.Conflict, "شناسه درخواست قبلاً استفاده شده است.");
                return true;
            }
            var replay = await repository.LockExistingMessage(id);
            if (replay != null)
            {
                var parent = await FindTicket(replay.Str("ticket_id"), false, true);
                if (orderId == "" || parent.Str("order_id") != orderId || replay.Str("actor") != "customer" || replay.Str("message") != message)
                    throw new UseCaseException(ErrorKind.Conflict, "شناسهٔ پیام قبلاً استفاده شده است.");
                resolvedId = parent.Str("id");
                return true;
            }
            if (orderId != "" && await repository.ActiveOrderTicket(customer.Str("id"), orderId) is string existing)
            {
                resolvedId = existing;
                var ticket = await FindTicket(existing, false, true);
                var activity = NextActivityTime(ticket.Str("updated_at"));
                await repository.InsertMessage(id, existing, "customer", message, activity);
                await repository.UpdateAfterReply(TicketWorkflow.AfterReply(false), activity, existing);
                updates.Enqueue(ChapGiti.Application.Realtime.RealtimeUpdate.Ticket(existing, id));
                await notifications.Publish(new(ChapGiti.Domain.Notifications.NotificationEvent.TicketMessage, id, existing, customer.Str("id"), ToAdministrators: true));
                return true;
            }
            var now = Input.Now();
            await repository.InsertTicket(id, customer!.Str("id"), subject, now, orderId == "" ? null : orderId);
            await repository.InsertInitialMessage(id, message, now);
            await notifications.Publish(new(consultation ? ChapGiti.Domain.Notifications.NotificationEvent.ConsultationRequested
                : ChapGiti.Domain.Notifications.NotificationEvent.TicketCreated, id, id, customer.Str("id"), ToAdministrators: true));
            return false;
        });
        return UseCaseResult.Json(new
        {
            ok = true,
            id = resolvedId
        }, created: !duplicate);
    }

    public async Task<UseCaseResult> List(bool admin = false, string query = "", string status = "", int? page = null, int pageSize = 20, bool oldestFirst = false)
    {
        if (query.Length>100 || page is <1 or >100000 || pageSize is <1 or >50) throw new UseCaseException(ErrorKind.Invalid,"فیلتر یا صفحه معتبر نیست.");
        if(status!="")TicketWorkflow.ValidateStatus(status);
        if (admin) await sessions.Admin();
        var owner = admin ? null : (await sessions.Customer())!.Str("id");
        query = Input.NormalizeDigits(query.Trim());
        var items = await repository.SearchTickets(owner,query,status,((page??1)-1)*pageSize,page.HasValue?pageSize:admin?200:100, oldestFirst);
        foreach (var item in items) item.Remove(admin ? "unread_customer" : "unread_admin");
        if(!page.HasValue)return UseCaseResult.Json(items);
        return UseCaseResult.Json(new { items,total=await repository.CountTickets(owner,query,status),page,pageSize,readOnly=!await repository.SupportsMessageReads(),orderLinksAvailable=await repository.SupportsOrderLinks() });
    }

    public async Task<UseCaseResult> Read(string id, JsonObject body, bool admin)
    {
        var ticket = await FindTicket(id, admin);
        await RequireMessaging();
        var through = Input.Id(body, "throughId");
        await unitOfWork.Transaction(async () =>
        {
            if (await repository.MarkMessagesRead(id,admin ? "customer" : "admin",through) == 0) return false;
            var identities = admin ? await recipients.AdministratorRecipients() : [ticket.Str("customer_id")];
            foreach (var identity in identities)
                updates.Enqueue(ChapGiti.Application.Realtime.RealtimeUpdate.Notifications(new(admin ? "admin" : "customer",identity),"support-read:"+through));
            return true;
        });
        return UseCaseResult.Json(new { ok = true });
    }
    public async Task<UseCaseResult> Get(string idValue, bool admin = false)
    {
        var ticket = new Dictionary<string,object?>(await FindTicket(idValue, admin));
        ticket.Remove(admin ? "unread_customer" : "unread_admin");
        var linkedFiles=ticket.GetValueOrDefault("order_id") is string orderId && ticket.GetValueOrDefault("order_code") != null
            ? await orders.CustomerOrderFiles(orderId,ticket.Str("customer_id")) : [];
        var messages = await repository.Messages(idValue);
        var attachments = (await repository.TicketFiles(idValue)).ToLookup(f => f.MessageId);
        foreach (var message in messages)
            message["attachments"] = attachments[message.Str("id")].Select(f => new { f.Id, f.Name, f.Mime, f.Size }).ToArray();
        return UseCaseResult.Json(new
        {
            attachmentLimits = new { count = filePolicy.MaxFiles, bytes = filePolicy.MaxBytes },
            ticket,
            orderFiles = linkedFiles,
            readOnly = !await repository.SupportsMessageReads(),
            messages
        });
    }

    public async Task<UseCaseResult> ChangeStatus(string idValue, JsonObject body, Dictionary<string, object?> user)
    {
        var status = Input.Text(body, "status", 1, 30);
        TicketWorkflow.ValidateStatus(status);
        await unitOfWork.Transaction(async () => {
            var current = await FindTicket(idValue,true,true);
            await RequireMessaging();
            if (current.Str("status") == status) return false;
            await repository.UpdateStatus(status,NextActivityTime(current.Str("updated_at")),idValue);
            updates.Enqueue(ChapGiti.Application.Realtime.RealtimeUpdate.Ticket(idValue, Input.NewId()));
            await sessions.Audit(user.Str("email"),"تغییر وضعیت گفت‌وگوی پشتیبانی");
            return true;
        });
        return UseCaseResult.Json(new
        {
            ok = true
        });
    }

    public async Task<UseCaseResult> Download(string id, string fileId, bool admin)
    {
        await FindTicket(id, admin);
        var file = (await repository.TicketFiles(id, fileId: fileId)).SingleOrDefault()
            ?? throw new UseCaseException(ErrorKind.NotFound, "پیوست پیدا نشد.");
        return UseCaseResult.File(await storage.ReadTicketFile(file.Id), file.Mime, file.Name, sandbox:true);
    }

    public async Task AuthorizeReply(string id, bool admin) => await FindTicket(id, admin);

    public async Task<UseCaseResult> Reply(string idValue, JsonObject body, bool admin = false, Dictionary<string, object?>? user = null, IReadOnlyList<IUploadContent>? uploads = null)
    {
        await FindTicket(idValue, admin);
        await RequireMessaging();
        if (!admin)
            await sessions.Limit("ticket-reply", 30);
        var prepared = await filePolicy.Prepare(uploads ?? []);
        if (prepared.Count > 0 && !await repository.SupportsAttachments())
            throw new UseCaseException(ErrorKind.Unavailable,"پیوست پس از به‌روزرسانی پشتیبانی فعال می‌شود.", code:"SUPPORT_SCHEMA_PENDING");
        var message = Input.Text(body, "message", prepared.Count > 0 ? 0 : 2, 5000);
        var id = Input.Id(body);
        var actor = admin ? "admin" : "customer";
        var written = new List<string>();
        bool duplicate;
        try
        {
            duplicate = await unitOfWork.Transaction(async () =>
            {
                var current = await FindTicket(idValue, admin, true);
                var old = await repository.LockExistingMessage(id);
                if (old != null)
                {
                    if (old.Str("ticket_id") != idValue || old.Str("actor") != actor || old.Str("message") != message)
                        throw new UseCaseException(ErrorKind.Conflict, "شناسه پیام قبلاً استفاده شده است.");
                    var prior = (await repository.TicketFiles(idValue, messageId: id)).Select(f=>(f.Name,f.Digest));
                    if (!prior.SequenceEqual(prepared.Select(f=>(f.Name,f.Digest))))
                        throw new UseCaseException(ErrorKind.Conflict,"پیوست‌های این پیام تغییر کرده‌اند.",code:"TICKET_MESSAGE_CONFLICT");
                    return true;
                }
                if (current.Str("status") == "closed")
                    throw new UseCaseException(ErrorKind.Conflict, admin ? "ابتدا تیکت را دوباره باز کنید." : "این تیکت بسته شده است؛ تیکت جدید ثبت کنید.");
                var now = NextActivityTime(current.Str("updated_at"));
                await repository.InsertMessage(id, idValue, actor, message, now);
                foreach (var (upload, position) in prepared.Select((f,i)=>(f,i)))
                {
                    var fileId = Input.NewId();
                    await storage.WriteTicketFile(fileId, upload.Bytes);
                    written.Add(fileId);
                    await repository.AddFile(new(fileId,id,upload.Name,upload.Mime,upload.Bytes.Length,position,upload.Digest));
                }
                if (admin) await sessions.Audit(user!.Str("email"), "پاسخ به تیکت " + idValue);
                updates.Enqueue(ChapGiti.Application.Realtime.RealtimeUpdate.Ticket(idValue, id));
                await repository.UpdateAfterReply(TicketWorkflow.AfterReply(admin, current.Str("status")), now, idValue);
                await notifications.Publish(new(ChapGiti.Domain.Notifications.NotificationEvent.TicketMessage, id, idValue,
                    current.Str("customer_id"), ToAdministrators: !admin));
                return false;
            });
        }
        catch
        {
            foreach (var key in written) storage.DeleteTicketFile(key);
            throw;
        }
        return UseCaseResult.Json(new
        {
            ok = true
        }, created: !duplicate);
    }

    private async Task<Dictionary<string, object?>> FindTicket(string idValue, bool admin, bool locked = false)
    {
        if (admin) await sessions.Admin();
        var owner = admin ? null : await sessions.Customer();
        return await repository.FindTicket(idValue, admin, owner?.Str("id"), locked)
            ?? throw new UseCaseException(ErrorKind.NotFound, "تیکت پیدا نشد.");
    }

    // The ticket row is locked before calling this. SQL stores millisecond precision;
    // two serialized replies in that millisecond must not sort by random message IDs.
    private static string NextActivityTime(string updatedAt)
    {
        var now = DateTimeOffset.UtcNow;
        if (DateTimeOffset.TryParse(updatedAt, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal, out var previous) && now.ToUnixTimeMilliseconds() <= previous.ToUnixTimeMilliseconds())
            now = previous.AddMilliseconds(1);
        return now.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture);
    }
}
