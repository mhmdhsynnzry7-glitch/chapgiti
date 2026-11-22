using ChapGiti.Application.Abstractions;
using ChapGiti.Application.Common;
namespace ChapGiti.AuthenticationTests;

internal sealed class MemoryActivityRepository(MemoryApiData data,FakeStore store) : ICustomerActivityRepository
{
    public Task<(List<Dictionary<string,object?>> Items,long Total)> List(string query,string type,DateTime? from,DateTime? until,int page,int pageSize, bool oldestFirst = false)
    {
        var rows=new List<Dictionary<string,object?>>();
        void Add(string id,string kind,string? customer,string at,string? orderId=null,string? ticketId=null,string? subject=null,string? code=null,string? status=null,string? actor=null) {
            var profile=customer==null?null:store.Profiles.GetValueOrDefault(customer);
            var name=profile==null?"اطلاعات مشتری موجود نیست":(profile.First+" "+profile.Last).Trim();
            var attributedActor=kind!="order_status"||actor=="customer"||(customer!=null&&actor=="customer:"+customer) ? name : actor==AuthFixture.AdminEmail?"admin":string.IsNullOrEmpty(actor)?null:actor;
            rows.Add(new(){["id"]=id,["type"]=kind,["occurred_at"]=at,["customer_name"]=name,["phone"]=profile?.Phone,["order_id"]=orderId,["ticket_id"]=ticketId,["subject"]=subject,["order_code"]=code,["status"]=status,["actor"]=attributedActor});
        }
        foreach(var s in store.Access.Values.Where(s=>s.CustomerId!=null&&store.SessionCreatedAt.ContainsKey(s.Id)))Add(s.Id,"login",s.CustomerId,DateTimeOffset.FromUnixTimeMilliseconds(store.SessionCreatedAt[s.Id]).ToString("O"));
        foreach(var o in data.Orders.Values)Add(o.Str("id"),"order_created",o.Str("customer_id"),o.Str("created_at"),o.Str("id"),subject:o.Str("title"),code:o.Str("code"));
        foreach(var t in data.Tickets.Values)Add(t.Str("id"),"ticket_created",t.Str("customer_id"),t.Str("created_at"),ticketId:t.Str("id"),subject:t.Str("subject"));
        foreach(var e in data.Events.Where(e=>e.Str("status")!="new")) {var o=data.Orders[e.Str("order_id")];Add(e.Str("id"),"order_status",o.Str("customer_id"),e.Str("created_at"),o.Str("id"),subject:o.Str("title"),code:o.Str("code"),status:e.Str("status"),actor:e.Str("actor"));}
        var filtered=rows.Where(r=>(type==""||r.Str("type")==type)&&(query==""||(r.Str("customer_name")+r.Str("phone")).Contains(query,StringComparison.OrdinalIgnoreCase)))
            .Where(r=>(from==null||DateTimeOffset.Parse(r.Str("occurred_at")).UtcDateTime>=from)&&(until==null||DateTimeOffset.Parse(r.Str("occurred_at")).UtcDateTime<until))
            .OrderByDescending(r=>DateTimeOffset.Parse(r.Str("occurred_at"))).ThenBy(r=>r.Str("type")).ThenByDescending(r=>r.Str("id")).ToList();
        return Task.FromResult(((oldestFirst ? filtered.AsEnumerable().Reverse() : filtered).Skip((page-1)*pageSize).Take(pageSize).ToList(),(long)filtered.Count));
    }
}
