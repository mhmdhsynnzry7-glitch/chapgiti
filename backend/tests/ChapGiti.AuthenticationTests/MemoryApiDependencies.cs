using System.Reflection;
using System.Text.Json.Nodes;
using ChapGiti.Application.Abstractions;
using ChapGiti.Application.Common;

namespace ChapGiti.AuthenticationTests;

// Deliberately contains no SqlStore, connection string, provider client or filesystem storage.
internal sealed class MemoryApiData : IContentDefaults, IContentRepository, IPrivateFileStore, ISmsNotifier, IDatabaseMaintenance
{
    public JsonObject Root { get; } = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Data","defaults.json")))!.AsObject();
    public readonly Dictionary<string,string> Settings=[];
    public bool FailNextSettingSave;
    public bool FailNextOrderList;
    public int SimulatedTrackingCollisions;
    public int OrderListDelayMs;
    public readonly Dictionary<string,byte[]> Assets=[];
    public readonly Dictionary<string,Dictionary<string,object?>> AssetMetadata=[];
    public readonly Dictionary<string,Dictionary<string,object?>> Orders=[];
    public readonly List<Dictionary<string,object?>> Events=[];
    public readonly Dictionary<string,Dictionary<string,object?>> Tickets=[];
    public readonly Dictionary<string,Dictionary<string,object?>> MessagesById=[];
    public readonly Dictionary<string,Dictionary<string,object?>> Files=[];
    public readonly Dictionary<string,Dictionary<string,object?>> Notes=[];
    public readonly Dictionary<string,Dictionary<string,object?>> ArticleRows=[];
    public bool MessageReadsAvailable=true, OrderLinksAvailable=true, OrderNotesAvailable=true, FailNextEvent;
    public bool CatalogAvailable=true,MagazineAvailable=true,FailNextMagazineSave;
    public readonly Dictionary<string,ChapGiti.Application.Catalog.PrintCatalogItem> CatalogItems=MemoryPrintCatalogRepository.Initial();
    public readonly Dictionary<string,ChapGiti.Application.Content.MagazineArticle> MagazineArticles=[];
    public readonly Dictionary<string,string> MagazineAliases=[];
    public readonly Dictionary<string,Dictionary<string,object?>> QuoteRows=[];
    public readonly Dictionary<string,Dictionary<string,object?>> Commands=[];
    public bool QuotesAvailable=true;
    public bool CorrectionsAvailable=true, OrderViewsAvailable=true;
    public readonly Dictionary<string,ChapGiti.Application.Orders.OrderCorrection> CorrectionRows=[];
    public readonly Dictionary<(string Order,string Kind,string Recipient),long> OrderViews=[];
    public bool NotificationsAvailable=true, FailNextNotification;
    public long NotificationSequence;
    public readonly Dictionary<string,(ChapGiti.Application.Notifications.StoredNotification Notice,long Sequence)> NotificationRows=[];
    public FakeStore Store=null!;
    public Action Snapshot()
    {
        var tables=new[]{Orders,Tickets,MessagesById,Files,Notes,ArticleRows,QuoteRows,Commands};
        var copies=tables.Select(table=>table.ToDictionary(pair=>pair.Key,pair=>pair.Value.ToDictionary())).ToArray();
        var events=Events.Select(row=>row.ToDictionary()).ToList();
        var settings=Settings.ToDictionary();
        var notifications=NotificationRows.ToDictionary();
        var corrections=CorrectionRows.ToDictionary();var views=OrderViews.ToDictionary();
        var catalog=CatalogItems.ToDictionary();var magazine=MagazineArticles.ToDictionary();var aliases=MagazineAliases.ToDictionary();
        return ()=>{for(var i=0;i<tables.Length;i++){tables[i].Clear();foreach(var pair in copies[i])tables[i][pair.Key]=pair.Value;}Events.Clear();Events.AddRange(events);Settings.Clear();foreach(var pair in settings)Settings[pair.Key]=pair.Value;NotificationRows.Clear();foreach(var pair in notifications)NotificationRows[pair.Key]=pair.Value;CorrectionRows.Clear();foreach(var pair in corrections)CorrectionRows[pair.Key]=pair.Value;OrderViews.Clear();foreach(var pair in views)OrderViews[pair.Key]=pair.Value;CatalogItems.Clear();foreach(var pair in catalog)CatalogItems[pair.Key]=pair.Value;MagazineArticles.Clear();foreach(var pair in magazine)MagazineArticles[pair.Key]=pair.Value;MagazineAliases.Clear();foreach(var pair in aliases)MagazineAliases[pair.Key]=pair.Value;};
    }
    public JsonNode Copy(string key)=>Root[key]!.DeepClone();
    public Task<Dictionary<string,object?>?> ReadSetting(string key)=>Task.FromResult(Settings.TryGetValue(key,out var value)?new Dictionary<string,object?>{{"value",value}}:null);
    public Task<int> SaveSetting(string key,string json){if(FailNextSettingSave){FailNextSettingSave=false;throw new UseCaseException(ErrorKind.Conflict,"ذخیرهٔ آزمایشی انجام نشد.");}Settings[key]=json;return Task.FromResult(1);}
    public Task<Dictionary<string,object?>?> ReadSeedMarker()=>ReadSetting("content_initialized");
    public Task<List<Dictionary<string,object?>>> Articles(bool all)=>Task.FromResult(ArticleRows.Values.Where(row=>all||Convert.ToBoolean(row["published"])).Select(row=>row.ToDictionary()).ToList());
    public Task<Dictionary<string,object?>?> ReadRates()=>ReadSetting("rates");
    public Task<byte[]> ReadPdf(string key)=>Task.FromResult(Assets.TryGetValue(key,out var bytes)?bytes:throw new UseCaseException(ErrorKind.NotFound,"فایل آزمایشی در دسترس نیست."));
    public Task<byte[]> ReadImage(string id)=>Task.FromResult(Assets[id]);
    public Task WritePdf(string key,byte[] bytes){Assets.Add(key,bytes);return Task.CompletedTask;}
    public Task WriteImage(string id,byte[] bytes){Assets.Add(id,bytes);return Task.CompletedTask;}
    public void DeletePdf(string key)=>Assets.Remove(key);
    public void DeleteImage(string id)=>Assets.Remove(id);
    public SmsAvailability Availability=>new(false,false,false);
    public Task Notify(string orderId,string code,string phone,string status,string eventId)=>Task.CompletedTask;
    public Task Migrate()=>throw new InvalidOperationException("Migrations are forbidden in this test host.");
    public Task Import(string data)=>throw new InvalidOperationException("Import is forbidden in this test host.");
    public Task VerifyAdministrator()=>Task.CompletedTask;
    public Task VerifyStartup()=>Task.CompletedTask;
    public Task Health()=>Task.CompletedTask;
    public T Repository<T>() where T:class
    {
        var proxy=DispatchProxy.Create<T,MemoryRepositoryProxy>();
        ((MemoryRepositoryProxy)(object)proxy).Data=this;
        return proxy;
    }
}

public class MemoryRepositoryProxy : DispatchProxy
{
    internal MemoryApiData Data=null!;
    protected override object? Invoke(MethodInfo? method,object?[]? args)
    {
        var a=args!;var name=method!.Name;
        Task<Dictionary<string,object?>?> Row(Dictionary<string,object?>? r)=>Task.FromResult(r);
        Task<List<Dictionary<string,object?>>> Rows(IEnumerable<Dictionary<string,object?>> r)=>Task.FromResult(r.ToList());
        string S(int i)=>Convert.ToString(a[i])??"";
        Dictionary<string,object?>? PublicOrder(Dictionary<string,object?>? order) {
            if(order==null)return null;
            var result=new[]{"id","code","title","service","status","estimate","final_price","created_at","updated_at","version","workflow_version","information_request","information_requires_file","approved_file_id"}.ToDictionary(key=>key,key=>order.GetValueOrDefault(key));
            var latest=Data.QuoteRows.Values.Where(q=>q.Str("order_id")==order.Str("id")).OrderBy(q=>q.Num("revision")).LastOrDefault();
            result["quote_amount"]=latest?.GetValueOrDefault("amount_toman");result["quote_revision"]=latest?.GetValueOrDefault("revision");result["quote_state"]=latest?.GetValueOrDefault("state");return result;
        }
        switch(name)
        {
            case "SupportsQuoteWorkflow":return Task.FromResult(Data.QuotesAvailable);
            case "Quotes":return Rows(Data.QuoteRows.Values.Where(q=>q.Str("order_id")==S(0)).OrderBy(q=>q.Num("revision")).Select(q=>q.ToDictionary()));
            case "LockCommand":return Row(Data.Commands.GetValueOrDefault(S(0)));
            case "InsertCommand":Data.Commands[S(0)]=new(){["id"]=a[0],["order_id"]=a[1],["actor"]=a[2],["kind"]=a[3],["payload_hash"]=a[4],["created_at"]=a[5]};return Task.CompletedTask;
            case "InsertQuote":Data.QuoteRows[S(0)]=new(){["id"]=a[0],["order_id"]=a[1],["revision"]=a[2],["amount_toman"]=a[3],["scope_text"]=a[4],["delivery_terms"]=a[5],["exclusions_text"]=a[6],["created_by_admin"]=a[7],["sent_at"]=a[8],["state"]="sent",["version"]=1};return Task.CompletedTask;
            case "DecideQuote":
                var q=Data.QuoteRows[S(0)];if(q.Str("state")!="sent"||q.Num("version")!=Convert.ToInt64(a[5]))return Task.FromResult(0);
                q["state"]=a[1];q["decision_reason"]=a[2];q["decided_at"]=a[4];q["accepted_at"]=S(1)=="accepted"?a[4]:null;q["accepted_by_customer"]=S(1)=="accepted"?a[3]:null;q["version"]=q.Num("version")+1;return Task.FromResult(1);
            case "SetInformation":Data.Orders[S(0)]["information_request"]=a[1];Data.Orders[S(0)]["information_requires_file"]=a[2];return Task.CompletedTask;
            case "AdoptProposalWorkflow":Data.Orders[S(0)]["workflow_version"]=2;return Task.CompletedTask;
            case "LockSeedMarker":return Data.ReadSeedMarker();
            case "LockSetting":return Data.ReadSetting(S(0));
            case "InsertArticle":
                if(Data.ArticleRows.Values.Any(row=>row.Str("slug")==S(1)))throw new UseCaseException(ErrorKind.Conflict,"نشانی مقاله قبلاً استفاده شده است.");
                Data.ArticleRows[S(0)]=new(){["id"]=a[0],["slug"]=a[1],["title"]=a[2],["excerpt"]=a[3],["content"]=a[4],["category"]=a[5],["published"]=a[6],["seo_title"]=a[7],["seo_description"]=a[8],["created_at"]=a[9],["updated_at"]=a[10]};return Task.FromResult(1);
            case "UpdateArticle":
                if(!Data.ArticleRows.TryGetValue(S(9),out var article))return Task.FromResult(0);
                if(Data.ArticleRows.Values.Any(row=>row.Str("slug")==S(0)&&row.Str("id")!=S(9)))throw new UseCaseException(ErrorKind.Conflict,"نشانی مقاله قبلاً استفاده شده است.");
                foreach(var pair in new[]{("slug",0),("title",1),("excerpt",2),("content",3),("category",4),("published",5),("seo_title",6),("seo_description",7),("updated_at",8)})article[pair.Item1]=a[pair.Item2];return Task.FromResult(1);
            case "InsertOrder":
                Data.Orders[S(0)]=new(){["id"]=a[0],["code"]=a[1],["customer"]=a[2],["phone"]=a[3],["email"]=a[4],["title"]=a[5],["service"]=a[6],["spec"]=a[7],["estimate"]=a[8],["created_at"]=a[9],["updated_at"]=a[9],["customer_id"]=a[10],["status"]="new",["workflow_version"]=2,["version"]=1,["final_price"]=null};
                return Task.FromResult(1);
            case "LockExistingOrder":return Row(Data.Orders.GetValueOrDefault(S(0)));
            case "FindOrder":case "LockOrder":return Row(Data.Orders.GetValueOrDefault(S(0))?.ToDictionary());
            case "SupportsOrderNotes":return Task.FromResult(Data.OrderNotesAvailable);
            case "SupportsOrderLinks":return Task.FromResult(Data.OrderLinksAvailable);
            case "InternalNotes":return Rows(Data.Notes.Values.Where(n=>n.Str("order_id")==S(0)));
            case "LockInternalNote":return Row(Data.Notes.GetValueOrDefault(S(0)));
            case "InsertInternalNote":Data.Notes[S(0)]=new(){["id"]=a[0],["order_id"]=a[1],["note"]=a[2],["actor"]=a[3],["created_at"]=a[4]};return Task.FromResult(1);
            case "TrackOrder":
                var tracked=Data.Orders.Values.FirstOrDefault(r=>r.Str("code")==S(0)&&r.Str("phone")==S(1)&&r.Str("customer_id")==S(2));
                var tracking=PublicOrder(tracked);if(tracking!=null)tracking["spec"]=tracked!["spec"];return Row(tracking);
            case "CustomerOrder":
                if (!Data.Orders.TryGetValue(S(0),out var owned) || owned.Str("customer_id")!=S(1)) return Row(null);
                var detail=PublicOrder(owned)!;detail["spec"]=owned["spec"];return Row(detail);
            case "CustomerOrderFiles":
                return Rows(Data.Orders.TryGetValue(S(0),out var fileOrder)&&fileOrder.Str("customer_id")==S(1)
                    ?Data.Files.Values.Where(f=>f.Str("order_id")==S(0)).Select(f=>new[]{"id","name","size","created_at"}.ToDictionary(k=>k,k=>f.GetValueOrDefault(k)))
                    :[]);
            case "CountCustomerOrders":return Task.FromResult((long)Data.Orders.Values.Count(r=>r.Str("customer_id")==S(0)&&(S(1)==""||S(1).Split(',').Contains(r.Str("status")))));
            case "CustomerOrders":
                if(Data.FailNextOrderList){Data.FailNextOrderList=false;return Task.FromException<List<Dictionary<string,object?>>>(new UseCaseException(ErrorKind.Unavailable,"دریافت سفارش‌ها موقتاً امکان‌پذیر نیست."));}
                var rows=Data.Orders.Values.Where(r=>r.Str("customer_id")==S(0)&&(S(3)==""||S(3).Split(',').Contains(r.Str("status")))).OrderByDescending(r=>r.Str("created_at"),StringComparer.Ordinal)
                    .ThenByDescending(r=>r.Str("id"),StringComparer.Ordinal).Skip(Convert.ToInt32(a[1])).Take(Convert.ToInt32(a[2])).Select(r=>PublicOrder(r)!).ToList();
                async Task<List<Dictionary<string,object?>>> Delayed(){await Task.Delay(Data.OrderListDelayMs);return rows;}
                return Data.OrderListDelayMs>0?Delayed():Task.FromResult(rows);
            case "InsertInitialEvent":
                Data.Events.Add(new(){["id"]=a[0],["order_id"]=a[1],["status"]="new",["note"]="درخواست شما ثبت شد.",["actor"]="customer",["created_at"]=a[2]});return Task.FromResult(1);
            case "InsertEvent":
                if(Data.FailNextEvent){Data.FailNextEvent=false;throw new InvalidOperationException("Isolated event persistence failure.");}
                Data.Events.Add(new(){["id"]=a[0],["order_id"]=a[1],["status"]=a[2],["note"]=a[3],["actor"]=a[4],["created_at"]=a[5]});return Task.FromResult(1);
            case "TrackingEvents":return Rows(Data.Events.Where(r=>r.Str("order_id")==S(0)).OrderBy(r=>r.Str("created_at"),StringComparer.Ordinal)
                .Select(r=>new Dictionary<string,object?> { ["status"]=r["status"], ["note"]=r["note"], ["created_at"]=r["created_at"], ["actor_role"]=r.Str("actor").StartsWith("customer")?"customer":r.Str("actor").Contains("@")?"admin":"system" }));
            case "OrderEvents":return Rows(Data.Events.Where(r=>r.Str("order_id")==S(0)));
            case "FindOwnedOrder":case "LockOwnedOrder":
                return Row(Data.Orders.TryGetValue(S(0),out var order)&&order.Str("code")==S(1)&&order.Str("customer_id")==S(2)?order:null);
            case "UpdateOrder":
                var o=Data.Orders[S(4)];if(o.Num("version")!=Convert.ToInt64(a[5]))return Task.FromResult(0);
                o["status"]=a[0];o["final_price"]=a[1];o["admin_notes"]=a[2];o["updated_at"]=a[3];o["version"]=o.Num("version")+1;return Task.FromResult(1);
            case "CountOrders":return Row(new(){["count"]=FilteredOrders(S(0),S(1)).Count()});
            case "OrderFiles":return Rows(Data.Files.Values.Where(f=>f.Str("order_id")==S(0)));
            case "CountFiles":return Row(new(){["n"]=Data.Files.Values.Count(r=>r.Str("order_id")==S(0))});
            case "InsertFile":Data.Files[S(0)]=new(){["id"]=a[0],["order_id"]=a[1],["object_key"]=a[2],["name"]=a[3],["size"]=a[4],["created_at"]=a[5]};return Task.FromResult(1);
            case "FindFile":return Row(Data.Files.GetValueOrDefault(S(0)));
            case "FindOwnedFile":return Row(Data.Files.TryGetValue(S(0),out var ownedFile)&&Data.Orders.TryGetValue(ownedFile.Str("order_id"),out var privateFileOrder)&&privateFileOrder.Str("customer_id")==S(1)?ownedFile:null);
            case "SearchOrders":return Rows((Convert.ToBoolean(a[5]) ? FilteredOrders(S(0),S(1)).Reverse() : FilteredOrders(S(0),S(1))).Skip(Convert.ToInt32(a[3])).Take(Convert.ToInt32(a[4])));
            case "ExportOrders":return Rows(FilteredOrders("", ""));
            case "RecentOrders":return Rows(FilteredOrders("", "").Take(6));
            case "StatusCounts":return Rows(Data.Orders.Values.GroupBy(o=>o.Str("status")).Select(g=>new Dictionary<string,object?>{["status"]=g.Key,["count"]=g.Count()}));
            case "Summary":return Row(new(){["total"]=Data.Orders.Count,["value"]=Data.Orders.Values.Where(o=>o.Str("status")!="cancelled").Sum(o=>o.GetValueOrDefault("final_price")==null?o.Num("estimate"):o.Num("final_price")),["customers"]=Data.Orders.Values.Select(o=>o.Str("phone")).Distinct().Count()});
            case "UnreadMessages":return Row(new(){["count"]=0});
            case "InsertAsset":Data.AssetMetadata[S(0)]=new(){["mime"]=a[1],["size"]=a[2]};return Task.FromResult(1);
            case "FindAsset":return Row(Data.AssetMetadata.GetValueOrDefault(S(0)));
            case "InsertTicket":Data.Tickets[S(0)]=new(){["id"]=a[0],["customer_id"]=a[1],["subject"]=a[2],["status"]="open",["created_at"]=a[3],["updated_at"]=a[3],["order_id"]=a[4]};return Task.FromResult(1);
            case "InsertInitialMessage":Data.Tickets[S(0)]["message"]=a[1];var mid=S(0);Data.MessagesById[mid]=new(){["id"]=mid,["ticket_id"]=a[0],["actor"]="customer",["message"]=a[1],["created_at"]=a[2]};return Task.FromResult(1);
            case "ServiceInUse": return Task.FromResult(Data.Orders.Values.Any(o=>o.Str("service")==S(0)));
            case "SupportsMessageReads": return Task.FromResult(Data.MessageReadsAvailable);
            case "OrderConversations": return Task.FromResult(Data.Tickets.Values.Where(t=>t.Str("order_id")==S(0)).ToList());
            case "ActiveOrderTicket": return Task.FromResult(Data.Tickets.Values.FirstOrDefault(t => t.Str("customer_id") == S(0) && t.Str("order_id") == S(1) && t.Str("status") != "closed")?.Str("id"));
            case "UnreadSupport":return Task.FromResult(Data.MessagesById.Values.LongCount(m => m.Str("actor")==S(1) && m.GetValueOrDefault("read_at")==null && (a[0]==null || Data.Tickets[m.Str("ticket_id")].Str("customer_id")==S(0))));
            case "MarkMessagesRead":
                var changed = 0;
                if (Data.MessagesById.TryGetValue(S(2), out var lastRead) && lastRead.Str("ticket_id")==S(0) && lastRead.Str("actor")==S(1))
                    foreach (var m in Data.MessagesById.Values.Where(m => m.Str("ticket_id")==S(0) && m.Str("actor")==S(1) && m.GetValueOrDefault("read_at")==null &&
                        (string.CompareOrdinal(m.Str("created_at"),lastRead.Str("created_at"))<0 || m.Str("created_at")==lastRead.Str("created_at") && string.CompareOrdinal(m.Str("id"),S(2))<=0)))
                    { m["read_at"]=ChapGiti.Domain.Common.Input.Now(); changed++; }
                return Task.FromResult(changed);
            case "TrackingCodeInUse":
                if (Data.SimulatedTrackingCollisions>0) {Data.SimulatedTrackingCollisions--;return Task.FromResult(true);}
                return Task.FromResult(Data.Orders.Values.Any(o=>o.Str("code")==S(0)));
            case "LockExistingTicket":return Row(Data.Tickets.GetValueOrDefault(S(0)));
            case "ListAll":return Rows(Data.Tickets.Values);
            case "ListOwned":return Rows(Data.Tickets.Values.Where(r=>r.Str("customer_id")==S(0)));
            case "FindTicket":return Row(Data.Tickets.TryGetValue(S(0),out var t)&&((bool)a[1]!||t.Str("customer_id")==S(2))?WithUnread(TicketRow(t)):null);
            case "SearchTickets":return Rows((Convert.ToBoolean(a[5]) ? FilteredTickets(S(0),S(1),S(2)).Reverse() : FilteredTickets(S(0),S(1),S(2))).Skip(Convert.ToInt32(a[3])).Take(Convert.ToInt32(a[4])).Select(WithUnread));
            case "CountTickets":return Task.FromResult((long)FilteredTickets(S(0),S(1),S(2)).Count());
            case "Messages" when a.Length==1:return Rows(Data.MessagesById.Values.Where(m=>m.Str("ticket_id")==S(0)).OrderBy(m=>m.Str("created_at")));
            case "LockExistingMessage":return Row(Data.MessagesById.GetValueOrDefault(S(0)));
            case "InsertMessage":Data.MessagesById[S(0)]=new(){["id"]=a[0],["ticket_id"]=a[1],["actor"]=a[2],["message"]=a[3],["created_at"]=a[4]};return Task.FromResult(1);
            case "UpdateStatus":case "UpdateAfterReply":Data.Tickets[S(2)]["status"]=a[0];Data.Tickets[S(2)]["updated_at"]=a[1];return Task.FromResult(1);
        }
        var result=method.ReturnType;
        if(result==typeof(Task))return Task.CompletedTask;
        if(result==typeof(Task<int>))return Task.FromResult(1);
        if(result==typeof(Task<List<Dictionary<string,object?>>>))return Rows([]);
        if(result==typeof(Task<Dictionary<string,object?>>))return Row(null);
        throw new InvalidOperationException("Unexpected test repository method: "+name);
    }

    private Dictionary<string,object?> TicketRow(Dictionary<string,object?> raw)
    {
        var row=raw.ToDictionary();var profile=Data.Store.Profiles.GetValueOrDefault(row.Str("customer_id"));
        row["name"]=profile==null?null:(profile.First+" "+profile.Last).Trim();row["phone"]=profile?.Phone;
        var order=Data.Orders.GetValueOrDefault(row.Str("order_id"));row["order_code"]=order?.GetValueOrDefault("code");row["order_title"]=order?.GetValueOrDefault("title");row["order_status"]=order?.GetValueOrDefault("status");
        var last=Data.MessagesById.Values.Where(m=>m.Str("ticket_id")==row.Str("id")).OrderByDescending(m=>m.Str("created_at")).ThenByDescending(m=>m.Str("id")).FirstOrDefault();
        row["last_message"]=last?.GetValueOrDefault("message");row["last_actor"]=last?.GetValueOrDefault("actor");return row;
    }
    private Dictionary<string,object?> WithUnread(Dictionary<string,object?> ticket)
    {
        var row = new Dictionary<string,object?>(ticket);
        var incoming = Data.MessagesById.Values.Where(m=>m.Str("ticket_id")==ticket.Str("id") && m.GetValueOrDefault("read_at")==null);
        row["unread_admin"] = incoming.Count(m=>m.Str("actor")=="customer");
        row["unread_customer"] = incoming.Count(m=>m.Str("actor")=="admin");
        return row;
    }
    private IEnumerable<Dictionary<string,object?>> FilteredTickets(string owner,string query,string status) => Data.Tickets.Values
        .Where(t=>(owner==""||t.Str("customer_id")==owner)&&(status==""||t.Str("status")==status)).Select(TicketRow)
        .Where(t=>query==""||new[]{"subject","name","phone"}.Any(key=>t.Str(key).Contains(query,StringComparison.OrdinalIgnoreCase)))
        .OrderByDescending(t=>t.Str("updated_at"),StringComparer.Ordinal).ThenByDescending(t=>t.Str("id"),StringComparer.Ordinal);
    private IEnumerable<Dictionary<string,object?>> FilteredOrders(string status,string query) =>
        Data.Orders.Values.Where(o=>(status==""||o.Str("status")==status) &&
            (query==""||new[]{"customer","title","code","phone"}.Any(key=>o.Str(key).Contains(query,StringComparison.OrdinalIgnoreCase))))
            .OrderByDescending(o=>o.Str("created_at"),StringComparer.Ordinal).ThenByDescending(o=>o.Str("id"),StringComparer.Ordinal);
}
