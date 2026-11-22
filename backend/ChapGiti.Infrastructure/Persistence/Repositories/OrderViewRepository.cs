using ChapGiti.Infrastructure.Persistence.Ef;
using Microsoft.EntityFrameworkCore;
using Models = ChapGiti.Infrastructure.Persistence.Ef.Models;
using ChapGiti.Application.Notifications;

namespace ChapGiti.Infrastructure.Persistence.Repositories;

internal sealed class OrderViewRepository(SqlStore db) : IOrderViewRepository
{
    public Task<bool> SupportsViews() => db.SupportsOrderViews();
    private static string Owner(NotificationRecipient r) => r.Kind switch { "customer"=>"customer_id", "admin"=>"admin_email", _=>throw new ArgumentException("Invalid recipient.") };
    public async Task<long> UnseenOrders(NotificationRecipient r)
    {
        if(!await SupportsViews())return 0;
        var owner=Owner(r);
        return (await db.One($"""
            SELECT COUNT_BIG(*) n FROM (
                SELECT n.entity_id,MAX(n.sequence) latest FROM dbo.Notifications n
                JOIN dbo.Orders o ON o.id=n.entity_id
                WHERE n.entity_kind='order' AND n.{owner}=@p0 AND (@p1='admin' OR o.customer_id=@p0)
                GROUP BY n.entity_id
            ) activity LEFT JOIN dbo.Order_Views v ON v.order_id=activity.entity_id AND v.{owner}=@p0
            CROSS APPLY(SELECT COALESCE((SELECT CONVERT(bigint,[value]) FROM dbo.Settings WHERE [key]='order_seen_baseline_sequence'),0) baseline) baselineState
            WHERE activity.latest>CASE WHEN v.seen_through_sequence>baselineState.baseline THEN v.seen_through_sequence ELSE baselineState.baseline END
            """,r.Id,r.Kind))!.Num("n");
    }
    public async Task<long> LatestSequence(string id,NotificationRecipient r) => !await SupportsViews()?0:await db.Context.Notifications.Where(n=>n.EntityKind=="order"&&n.EntityId==id&&n.RecipientKind==r.Kind&&(r.Kind=="customer"?n.CustomerId==r.Id:n.AdminEmail==r.Id)).MaxAsync(n=>(long?)n.Sequence)??0;
    public async Task<bool> MarkViewed(string id,NotificationRecipient r,long through,string now)
    {
        var owner=Owner(r);
        var existing=await db.One($"SELECT id,seen_through_sequence FROM dbo.Order_Views WITH(UPDLOCK,HOLDLOCK) WHERE order_id=@p0 AND {owner}=@p1",id,r.Id);
        if(existing!=null&&existing.Num("seen_through_sequence")>=through)return false;
        if(existing==null)await db.Insert(new Models.OrderView{OrderId=id,RecipientKind=r.Kind,CustomerId=r.Kind=="customer"?r.Id:null,AdminEmail=r.Kind=="admin"?r.Id:null,SeenThroughSequence=through,SeenAt=EfRows.Date(now)});
        else{var key=Convert.ToInt64(existing["id"]);await db.Context.OrderViews.Where(v=>v.Id==key&&v.SeenThroughSequence<through).ExecuteUpdateAsync(s=>s.SetProperty(v=>v.SeenThroughSequence,through).SetProperty(v=>v.SeenAt,EfRows.Date(now)));}
        return true;
    }
}
