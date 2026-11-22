using ChapGiti.Application.Abstractions;
using ChapGiti.Application.Common;
using ChapGiti.Application.Notifications;
using ChapGiti.Application.Orders;
using ChapGiti.Domain.Orders;

namespace ChapGiti.AuthenticationTests;

internal sealed class MemoryOrderCorrectionRepository(MemoryApiData data):IOrderCorrectionRepository
{
    public Task<bool> SupportsCorrections()=>Task.FromResult(data.CorrectionsAvailable);
    public Task<List<OrderCorrection>> Corrections(string id)=>Task.FromResult(data.CorrectionsAvailable?data.CorrectionRows.Values.Where(c=>c.OrderId==id).OrderBy(c=>c.Revision).ToList():[]);
    public Task InsertCorrection(OrderCorrection c)
    {
        if(data.CorrectionRows.Values.Any(x=>x.OrderId==c.OrderId && (x.Revision==c.Revision || OrderCorrectionWorkflow.Active(x.State))))
            throw new InvalidOperationException("Conflicting correction revision.");
        data.CorrectionRows.Add(c.Id,c);return Task.CompletedTask;
    }
    public Task<bool> SaveCorrection(OrderCorrection previous,OrderCorrection next)
    {
        if(!data.CorrectionRows.TryGetValue(previous.Id,out var current) || current!=previous || !OrderCorrectionWorkflow.Active(current.State))return Task.FromResult(false);
        data.CorrectionRows[previous.Id]=next;return Task.FromResult(true);
    }
    public Task<List<OrderFileRecord>> Files(string id)=>Task.FromResult(data.Files.Values.Where(f=>f.Str("order_id")==id)
        .OrderBy(f=>f.Num("file_version")).ThenBy(f=>f.Str("created_at")).ThenBy(f=>f.Str("id"))
        .Select(f=>new OrderFileRecord(f.Str("id"),id,f.Str("object_key"),f.Str("name"),(int)f.Num("size"),f.Str("created_at"),
            f.GetValueOrDefault("correction_id")==null?null:f.Str("correction_id"),f.GetValueOrDefault("file_version")==null?null:(int)f.Num("file_version"))).ToList());
    public Task LinkFile(string orderId,string fileId,string? revisionId,int version)
    {
        if(data.CorrectionsAvailable){var f=data.Files[fileId];f["correction_id"]=revisionId;f["file_version"]=version;}
        return Task.CompletedTask;
    }
    public Task SaveSpecification(string id, string spec) { data.Orders[id]["spec"] = spec; return Task.CompletedTask; }
    public Task SetApprovedFile(string orderId,string? fileId){if(data.CorrectionsAvailable)data.Orders[orderId]["approved_file_id"]=fileId;return Task.CompletedTask;}
}
internal sealed class MemoryOrderViewRepository(MemoryApiData data):IOrderViewRepository
{
    public Task<bool> SupportsViews()=>Task.FromResult(data.OrderViewsAvailable);
    private IEnumerable<(StoredNotification Notice,long Sequence)> Owned(NotificationRecipient r)=>data.NotificationRows.Values.Where(n=>n.Notice.Recipient==r && n.Notice.Item.EntityKind=="order" && data.Orders.TryGetValue(n.Notice.Item.EntityId,out var o) && (r.Kind=="admin" || o.Str("customer_id")==r.Id));
    public Task<long> UnseenOrders(NotificationRecipient r)
    {
        var baseline=data.Settings.TryGetValue("order_seen_baseline_sequence",out var value)?long.Parse(value):0;
        return Task.FromResult(data.OrderViewsAvailable?Owned(r).GroupBy(n=>n.Notice.Item.EntityId).LongCount(g=>g.Max(n=>n.Sequence)>Math.Max(baseline,data.OrderViews.GetValueOrDefault((g.Key,r.Kind,r.Id)))):0);
    }
    public Task<long> LatestSequence(string id,NotificationRecipient r)=>Task.FromResult(data.OrderViewsAvailable?Owned(r).Where(n=>n.Notice.Item.EntityId==id).Select(n=>n.Sequence).DefaultIfEmpty().Max():0);
    public Task<bool> MarkViewed(string id,NotificationRecipient r,long through,string now)
    {
        var key=(id,r.Kind,r.Id);var old=data.OrderViews.GetValueOrDefault(key);
        if(data.OrderViews.ContainsKey(key)&&old>=through)return Task.FromResult(false);
        data.OrderViews[key]=through;return Task.FromResult(true);
    }
}
