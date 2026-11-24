using System.Security.Cryptography;
using ChapGiti.Domain.Notifications;

namespace ChapGiti.Application.Features;

/// <summary>Coordinates owner decisions and production under the existing order lock.</summary>
public sealed class OrderQuoteService(IOrderQuoteRepository quotes, IAdminOrderRepository orders,
    IUnitOfWork unitOfWork, SessionService sessions, NotificationPublisher notifications,
    OrderProductionReadiness production, IOrderCorrectionRepository corrections)
{
    public Task<bool> Available() => quotes.SupportsQuoteWorkflow();
    public async Task RequireAvailable()
    {
        if (!await Available()) throw new UseCaseException(ErrorKind.Unavailable,
            "ثبت درخواست و پیشنهاد قیمت موقتاً در دسترس نیست؛ لطفاً از پشتیبانی پیگیری کنید.", code: "ORDER_QUOTES_UNAVAILABLE");
    }
    public async Task<List<Dictionary<string, object?>>> History(string orderId, bool administrator = false)
    {
        if (!await Available()) return [];
        var rows = await quotes.Quotes(orderId);
        if (administrator) return rows;
        string[] fields = ["id","order_id","revision","amount_toman","scope_text","delivery_terms","exclusions_text","sent_at","state","accepted_at","decided_at","decision_reason","version"];
        return rows.Select(row => fields.ToDictionary(key => key, key => row.GetValueOrDefault(key))).ToList();
    }

    public Task<UseCaseResult> Send(string id, JsonObject body, Dictionary<string, object?> admin)
    {
        var amount = Input.Number(body,"amountToman",0,100_000_000_000);
        var scope = Input.Text(body,"scopeText",2,3000);
        // Legacy delivery text remains readable on historical proposals.
        var clarification = Input.Text(body, "priceExplanation", 0, 500);
        if (clarification.Length > 0)
        {
            scope += "\nتوضیح اختلاف قیمت: " + clarification;
            if (scope.Length > 3000) throw new UseCaseException(ErrorKind.Invalid, "شرح خدمات و توضیحات قیمت طولانی است.");
        }
        var exclusions = Input.Text(body,"exclusionsText",0,2000);
        return ExecuteCommand(id,body,"send","admin:"+admin.Str("email"),null,async order => {
            if (order.Str("customer_id") == "") throw Conflict("درخواست قدیمی به حساب مشتری متصل نیست؛ پیشنهاد قابل تأیید توسط مالک نیست.","QUOTE_OWNER_REQUIRED");
            var history = await quotes.Quotes(id);
            if (history.Any(q => q.Str("state") == "accepted"))
                throw Conflict("قیمت پذیرفته‌شده قابل تغییر نیست.", "ACCEPTED_PRICE_IMMUTABLE");
            QuoteWorkflow.ValidateIssue(order.Str("status"),amount,scope);
            var file = await production.ReviewForAdvance(order, body.ContainsKey("reviewedFileId") ? Input.Id(body, "reviewedFileId") : null, admin.Str("id"));
            foreach (var old in history.Where(q=>q.Str("state")=="sent"))
                await Decide(old,"superseded","",null);
            var revision = history.Count == 0 ? 1 : checked((int)history.Max(q=>q.Num("revision"))+1);
            var now = Input.Now();
            // FK references the immutable administrator key, not the editable profile email.
            await quotes.InsertQuote(Input.Id(body),id,revision,amount,scope,"",exclusions,admin.Str("id"),now);
            var spec = JsonNode.Parse(order.Str("spec"))!.AsObject();
            if (spec["quoteSpecifications"] == null) spec["quoteSpecifications"] = new JsonObject();
            spec["quoteSpecifications"]![Input.Id(body)] = new JsonObject {
                ["fileId"] = file.Id, ["specificationVersion"] = OrderProductionReadiness.SpecificationVersion(spec)
            };
            await corrections.SaveSpecification(id, spec.ToJsonString());
            await quotes.AdoptProposalWorkflow(id);
            await Move(order,"awaiting_customer_approval",StoredPrice(order),"پیشنهاد قیمت نسخهٔ "+revision+" ارسال شد؛ منتظر تصمیم مشتری.","admin:"+admin.Str("email"),now);
        });
    }

    public async Task<UseCaseResult> CustomerDecision(string id,string action,JsonObject body)
    {
        var customer = (await sessions.Customer())!;
        var quoteId = Input.Id(body,"quoteId");
        var quoteVersion = Input.Number(body,"quoteVersion",1,int.MaxValue);
        var reason = Input.Text(body,"reason",0,900);
        var next = QuoteWorkflow.DecisionStatus(action,reason);
        return await ExecuteCommand(id,body,action,"customer:"+customer.Str("id"),customer.Str("id"),async order => {
            var latest = (await quotes.Quotes(id)).LastOrDefault();
            if(order.Str("status")!="awaiting_customer_approval" || latest == null || latest.Str("id")!=quoteId ||
                latest.Str("state")!="sent" || latest.Num("version")!=quoteVersion)
                throw Conflict("این پیشنهاد دیگر قابل تصمیم‌گیری نیست؛ نسخهٔ فعلی را دریافت کنید.","QUOTE_STALE");
            var state = action=="accept"?"accepted":action=="decline"?"declined":"revision_requested";
            if (action == "accept")
                OrderProductionReadiness.RequireQuotedSpecification(JsonNode.Parse(order.Str("spec"))!.AsObject(), quoteId);
            await Decide(latest,state,reason,customer.Str("id"));
            var message = action=="accept"?"مشتری پیشنهاد نسخهٔ "+latest.Num("revision")+" را تأیید کرد. شرایط نهایی به تأیید جداگانه نیاز دارد."
                :action=="decline"?"انصراف مشتری از پیشنهاد: "+reason:"درخواست اصلاح پیشنهاد توسط مشتری: "+reason;
            await Move(order,next,action=="accept"?latest.Num("amount_toman"):StoredPrice(order),message,"customer:"+customer.Str("id"),Input.Now());
        });
    }

    public async Task<UseCaseResult> FinalConditions(string id, JsonObject body, bool accept, bool amend = false)
    {
        var customerDecision = accept || amend;
        var actor = customerDecision ? (await sessions.Customer())! : (await sessions.Admin())!;
        var identity = customerDecision ? "customer:" + actor.Str("id") : "admin:" + actor.Str("email");
        return await ExecuteCommand(id, body, amend ? "amend-terms" : accept ? "accept-terms" : "send-terms", identity, customerDecision ? actor.Str("id") : null, async order =>
        {
            var quote = (await quotes.Quotes(id)).LastOrDefault();
            if ((order.Str("status") != "confirmed" && (customerDecision || order.Str("status") != "review")) || quote?.Str("state") != "accepted" ||
                quote.Str("accepted_by_customer") != order.Str("customer_id") || quote.Num("amount_toman") != StoredPrice(order))
                throw Conflict("ابتدا قیمت باید توسط مشتری پذیرفته شود.", "CUSTOMER_APPROVAL_REQUIRED");
            var file = customerDecision ? await production.RequireApprovedFile(order) : await production.ReviewForAdvance(order,
                body.ContainsKey("reviewedFileId") ? Input.Id(body, "reviewedFileId") : null, actor.Str("id"));
            var correctionId = (await corrections.Corrections(id)).LastOrDefault()?.Id;
            var spec = JsonNode.Parse(order.Str("spec"))!.AsObject();
            OrderProductionReadiness.RequireQuotedSpecification(spec, quote.Str("id"));
            if (body.ContainsKey("amountToman") || body.ContainsKey("shippingCost") || body.ContainsKey("finalPrice"))
                throw Conflict("تغییر قیمت در شرایط تحویل مجاز نیست.", "ACCEPTED_PRICE_IMMUTABLE");
            if (spec["deliveryConditions"] == null) spec["deliveryConditions"] = new JsonArray();
            var history = spec["deliveryConditions"]!.AsArray();
            var now = Input.Now();
            if (customerDecision)
            {
                var latest = history.LastOrDefault() as JsonObject;
                if (latest == null || latest["supersededAt"] != null || latest["id"]?.ToString() != Input.Id(body, "termsId") || latest["fileId"]?.ToString() != file.Id ||
                    latest["correctionId"]?.ToString() != correctionId || latest["quoteId"]?.ToString() != quote.Str("id") || latest["specificationVersion"]?.ToString() != OrderProductionReadiness.SpecificationVersion(spec) || latest["acceptedAt"] != null)
                    throw Conflict("نسخهٔ شرایط تغییر کرده است.", "FINAL_TERMS_STALE");
                if (amend)
                {
                    latest["amendmentReason"] = Input.Text(body, "reason", 2, 900);
                    latest["amendmentBy"] = actor.Str("id");
                    latest["supersededAt"] = now;
                }
                else
                {
                    latest["acceptedAt"] = now;
                    latest["acceptanceId"] = Input.Id(body);
                    latest["acceptedBy"] = actor.Str("id");
                }
            }
            else
            {
                var next = new JsonObject { ["id"] = Input.Id(body), ["revision"] = history.Count + 1,
                    ["delivery"] = Input.Text(body, "delivery", 2, 2000), ["scope"] = Input.Text(body, "scope", 0, 2000),
                    ["method"] = Input.Text(body, "method", 0, 160), ["estimate"] = Input.Text(body, "estimate", 0, 160),
                    ["address"] = Input.Flag(body, "addressNeeded") ? Input.Text(body, "address", 5, 600) : "",
                    ["comment"] = Input.Text(body, "comment", 0, 2000), ["fileId"] = file.Id, ["correctionId"] = correctionId,
                    ["quoteId"] = quote.Str("id"), ["issuedAt"] = now, ["issuedBy"] = actor.Str("id"), ["specificationVersion"] = OrderProductionReadiness.SpecificationVersion(spec) };
                if (history.LastOrDefault() is JsonObject previous && previous["supersededAt"] == null)
                {
                    bool sameBasis = new[] { "fileId", "correctionId", "quoteId", "specificationVersion" }.All(k=>JsonNode.DeepEquals(previous[k],next[k]));
                    if (sameBasis)
                    {
                        if (body["revisionOf"]?.ToString() != previous["id"]?.ToString())
                            throw Conflict("شرایط قبلاً ارسال شده است. برای تغییر، نسخهٔ جاری را اصلاح کنید.", "FINAL_TERMS_STALE");
                        if (new[] {"delivery","scope","method","estimate","address","comment"}.All(k=>JsonNode.DeepEquals(previous[k],next[k])))
                            throw Conflict("شرایط تغییری نکرده است.", "FINAL_TERMS_UNCHANGED");
                    }
                    previous["supersededAt"] = now;
                }
                history.Add(next);
            }
            await corrections.SaveSpecification(id, spec.ToJsonString());
            await Move(order, "confirmed", StoredPrice(order), amend ? "درخواست اصلاح شرایط تحویل: " + Input.Text(body, "reason", 2, 900) : accept ? "مشتری شرایط نهایی چاپ و تحویل را تأیید کرد." : "شرایط نهایی چاپ و تحویل برای تأیید مشتری ارسال شد.", identity, now);
        });
    }

    public Task<UseCaseResult> StartPrinting(string id,JsonObject body,Dictionary<string,object?> admin) =>
        ExecuteCommand(id,body,"start-printing","admin:"+admin.Str("email"),null,async order => {
            if(order.Str("status")!="confirmed") throw Conflict("شروع چاپ فقط پس از تأیید مشتری و ورود به صف چاپ مجاز است.","CUSTOMER_APPROVAL_REQUIRED");
            var history = await quotes.Quotes(id);
            if(order.Num("workflow_version")>=2 && (history.LastOrDefault()?.Str("state")!="accepted" ||
                history.Last().Num("amount_toman")!=StoredPrice(order) || history.Last().Str("accepted_by_customer")!=order.Str("customer_id")))
                throw Conflict("پیشنهاد تأییدشدهٔ مشتری پیدا نشد.","CUSTOMER_APPROVAL_REQUIRED");
            var price = StoredPrice(order);
            if(order.Num("workflow_version")<2 && price==null && body["finalPrice"]!=null)
                price=Input.Number(body,"finalPrice",0,100_000_000_000);
            OrderWorkflow.ValidateChange("confirmed","printing",price,Binding(order));
            await production.Ensure(order,price);
            await Move(order,"printing",price,"شروع واقعی چاپ توسط چاپخانه ثبت شد.","admin:"+admin.Str("email"),Input.Now());
        });

    public Task<UseCaseResult> Advance(string id,JsonObject body,Dictionary<string,object?> admin)
    {
        var status=Input.Text(body,"status",1,40);var note=Input.Text(body,"note",0,900);
        return ExecuteCommand(id,body,"advance","admin:"+admin.Str("email"),null,async order=>{
            if(status=="printing" || order.Str("status")=="awaiting_customer_approval" || status=="awaiting_customer_approval" ||
                status=="confirmed" || status==order.Str("status"))
                throw Conflict("از اقدام اختصاصی پیشنهاد یا شروع چاپ استفاده کنید.","ORDER_ACTION_INVALID");
            var price=StoredPrice(order);
            if(body.ContainsKey("finalPrice") && (body["finalPrice"]==null ? price!=null : Input.Number(body,"finalPrice",0,100_000_000_000)!=price))
                throw Conflict("مبلغ نهایی فقط با تأیید پیشنهاد مشتری ثبت می‌شود.","ACCEPTED_PRICE_IMMUTABLE");
            OrderWorkflow.ValidateChange(order.Str("status"),status,price,Binding(order),note);
            await Move(order,status,price,status=="cancelled"?"رد درخواست توسط چاپخانه: "+note:OrderWorkflow.Labels[status],"admin:"+admin.Str("email"),Input.Now());
        });
    }

    internal async Task<UseCaseResult> ExecuteCommand(string id,JsonObject body,string kind,string actor,string? owner,Func<Dictionary<string,object?>,Task> change)
    {
        await RequireAvailable();
        var key=Input.Id(body);var version=Input.Number(body,body.ContainsKey("expectedVersion")?"expectedVersion":"version",1,int.MaxValue);
        var canonical=new JsonObject(body.OrderBy(pair=>pair.Key,StringComparer.Ordinal).Select(pair=>new KeyValuePair<string,JsonNode?>(pair.Key,pair.Value?.DeepClone())));
        var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToJsonString())));
        var replay=await unitOfWork.Transaction(async()=>{
            var order=await orders.LockOrder(id)??throw new UseCaseException(ErrorKind.NotFound,"درخواست پیدا نشد.");
            if(owner!=null&&order.Str("customer_id")!=owner) throw new UseCaseException(ErrorKind.NotFound,"درخواست پیدا نشد.");
            var prior=await quotes.LockCommand(key);
            if(prior!=null) {
                if(prior.Str("order_id")!=id||prior.Str("actor")!=actor||prior.Str("kind")!=kind||prior.Str("payload_hash")!=hash)
                    throw Conflict("شناسهٔ عملیات قبلاً برای درخواست دیگری استفاده شده است.","ORDER_COMMAND_CONFLICT");
                return true;
            }
            if(order.Num("version")!=version) throw Conflict("درخواست تغییر کرده است؛ صفحه را تازه کنید.","ORDER_VERSION_CONFLICT");
            await change(order);
            await quotes.InsertCommand(key,id,actor,kind,hash,Input.Now());
            await Notify(kind, key, order, body);
            if(actor.StartsWith("admin:",StringComparison.Ordinal)) await sessions.Audit(actor[6..],"عملیات درخواست "+order.Str("code")+": "+kind);
            return false;
        });
        return UseCaseResult.Json(new {ok=true,replayed=replay});
    }
    private Task Notify(string kind, string key, Dictionary<string,object?> order, JsonObject body)
    {
        var id = order.Str("id"); var owner = order.Str("customer_id");
        return kind switch
        {
            "send-terms" => notifications.Publish(new(NotificationEvent.DeliveryTermsIssued, key, id, owner)),
            "amend-terms" => notifications.Publish(new(NotificationEvent.DeliveryTermsAmendmentRequested, key, id, owner, true)),
            "accept-terms" => notifications.Publish(new(NotificationEvent.DeliveryTermsAccepted, key, id, owner, true)),
            "send" => notifications.Publish(new(NotificationEvent.QuoteIssued, key, id, owner)),
            "accept" => notifications.Publish(new(NotificationEvent.QuoteAccepted, key, id, owner, true)),
            "request-revision" => notifications.Publish(new(NotificationEvent.QuoteRevisionRequested, key, id, owner, true)),
            "decline" => notifications.Publish(new(NotificationEvent.QuoteDeclined, key, id, owner, true)),
            "request-information" => notifications.Publish(new(NotificationEvent.InformationRequested, key, id, owner)),
            "provide-information" => notifications.Publish(new(NotificationEvent.InformationProvided, key, id, owner, true)),
            "request-correction" => notifications.Publish(new(NotificationEvent.InformationRequested,key,id,owner)),
            "submit-correction" => notifications.Publish(new(NotificationEvent.InformationProvided,key,id,owner,true)),
            "approve-correction" => notifications.Publish(new(NotificationEvent.CorrectionApproved,key,id,owner)),
            "reject-correction" => notifications.Publish(new(NotificationEvent.CorrectionRejected,key,id,owner)),
            "start-printing" => notifications.Status("printing", key, id, owner),
            "advance" => notifications.Status(Input.Text(body,"status",1,40), key, id, owner),
            _ => Task.CompletedTask
        };
    }
    private async Task Decide(Dictionary<string,object?> quote,string state,string reason,string? customer)
    {
        if(await quotes.DecideQuote(quote.Str("id"),state,reason,customer,Input.Now(),quote.Num("version"))!=1)
            throw Conflict("نسخهٔ پیشنهاد تغییر کرده است.","QUOTE_STALE");
    }
    internal async Task Move(Dictionary<string,object?> order,string status,long? price,string note,string actor,string now)
    {
        if(await orders.UpdateOrder(status,price,order.Str("admin_notes"),now,order.Str("id"),order.Num("version"))!=1)
            throw Conflict("درخواست تغییر کرده است.","ORDER_VERSION_CONFLICT");
        // Events retain the existing administrator-email convention (nvarchar254).
        // The broader namespaced actor is used only by the command receipt.
        var eventActor=actor.StartsWith("admin:",StringComparison.Ordinal)?actor[6..]:actor;
        await orders.InsertEvent(Input.NewId(),order.Str("id"),status,note,eventActor,now);
    }
    private static long? StoredPrice(Dictionary<string,object?> row)=>row.GetValueOrDefault("final_price")==null?null:row.Num("final_price");
    internal async Task SupersedeSentQuotes(string id)
    {
        foreach(var quote in (await quotes.Quotes(id)).Where(q=>q.Str("state")=="sent")) await Decide(quote,"superseded","",null);
    }
    private static bool Binding(Dictionary<string,object?> row)=>OrderWorkflow.RequiresBinding(JsonNode.Parse(row.Str("spec"))!.AsObject());
    private static UseCaseException Conflict(string message,string code)=>new(ErrorKind.Conflict,message,code:code);
}
