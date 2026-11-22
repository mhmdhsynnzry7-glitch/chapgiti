using ChapGiti.Infrastructure.Persistence.Ef;
using Microsoft.EntityFrameworkCore;
using Models = ChapGiti.Infrastructure.Persistence.Ef.Models;
using ChapGiti.Application.Notifications;

namespace ChapGiti.Infrastructure.Persistence.Repositories;

internal sealed class NotificationRepository(SqlStore db) : INotificationRepository
{
    private Task<T> Retry<T>(Func<Task<T>> operation) => SqlDeadlockRetry.Run(operation, db.InTransaction);
    public Task<bool> SupportsNotifications() => db.SupportsNotifications();
    public Task<List<string>> AdministratorRecipients() => (from u in db.Context.AdminUsers.AsNoTracking() join name in db.Context.AdminUsernames.AsNoTracking() on u.Email equals name.Email where name.Username=="admin" select u.Email).ToListAsync();

    private static string OwnerColumn(NotificationRecipient recipient) => recipient.Kind switch
    {
        "customer" => "customer_id", "admin" => "admin_email",
        _ => throw new ArgumentException("Invalid notification context.")
    };
    // SqlStore binds strings as nvarchar. Cast the parameter rather than the indexed varchar column.
    private static string OwnerFilter(NotificationRecipient recipient) => OwnerColumn(recipient) +
        (recipient.Kind == "customer" ? "=CONVERT(varchar(36),@p0)" : "=@p0");

    public async Task<bool> InsertNotification(StoredNotification notification)
    {
        if (!db.InTransaction) throw new InvalidOperationException("Notifications require the business transaction.");
        var recipient = notification.Recipient;
        var item = notification.Item;
        if (NotificationTargets.For(recipient, item.EntityKind, item.EntityId) != item.TargetPath)
            throw new ArgumentException("Invalid notification target.");
        return (await db.One($"""
            IF NOT EXISTS (SELECT 1 FROM dbo.Notifications WITH(UPDLOCK,HOLDLOCK) WHERE {OwnerFilter(recipient)} AND dedupe_key=CONVERT(varchar(100),@p1))
            INSERT dbo.Notifications(id,recipient_kind,customer_id,admin_email,event_type,entity_kind,entity_id,title,body,target_path,created_at,dedupe_key)
            OUTPUT inserted.id
            VALUES(@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12,@p1)
            """, recipient.Id, notification.DedupeKey, item.Id, recipient.Kind,
            recipient.Kind == "customer" ? recipient.Id : null, recipient.Kind == "admin" ? recipient.Id : null,
            item.EventType, item.EntityKind, item.EntityId, item.Title, item.Body, item.TargetPath, item.CreatedAt)) != null;
    }

    private IQueryable<Models.Notification> Owned(NotificationRecipient r)
    {
        _=OwnerColumn(r);
        return db.Context.Notifications.AsNoTracking().Where(n=>n.RecipientKind==r.Kind&&(r.Kind=="customer"?n.CustomerId==r.Id:n.AdminEmail==r.Id));
    }
    public async Task<List<NotificationItem>> ListNotifications(NotificationRecipient r,int skip,int take) =>
        (await Retry(()=>Owned(r).OrderByDescending(n=>n.Sequence).Skip(skip).Take(take).Select(n=>new{n.Id,n.EventType,n.EntityKind,n.EntityId,n.Title,n.Body,n.TargetPath,n.CreatedAt,n.ReadAt,
            OrderCode=db.Context.Orders.Where(o=>(r.Kind=="admin" || o.CustomerId==r.Id) && (
                n.EntityKind=="order" && o.Id==n.EntityId || n.EntityKind=="ticket" && db.Context.Tickets.Any(t=>t.Id==n.EntityId && t.OrderId==o.Id && t.CustomerId==o.CustomerId)))
                .Select(o=>o.Code).FirstOrDefault()}).ToListAsync()))
        .Select(n=>new NotificationItem(n.Id,n.EventType,n.EntityKind,n.EntityId,n.Title,n.Body,n.TargetPath,EfRows.Stamp(n.CreatedAt)!,EfRows.Stamp(n.ReadAt),n.OrderCode)).ToList();
    public Task<long> CountNotifications(NotificationRecipient r) => Retry(()=>Owned(r).LongCountAsync());
    public async Task<NotificationSummary> NotificationSummary(NotificationRecipient r) => new(await Retry(()=>Owned(r).LongCountAsync(n=>n.ReadAt==null)),await Retry(()=>Owned(r).MaxAsync(n=>(long?)n.Sequence))??0);
    public async Task<bool> ReadNotification(NotificationRecipient r,string id,string now){var time=EfRows.Date(now);return await Retry(()=>Owned(r).Where(n=>n.Id==id).ExecuteUpdateAsync(s=>s.SetProperty(n=>n.ReadAt,n=>n.ReadAt??(n.CreatedAt>time?n.CreatedAt:time))))==1;}
    public async Task ReadAllNotifications(NotificationRecipient r,string now){var time=EfRows.Date(now);await Retry(()=>Owned(r).Where(n=>n.ReadAt==null).ExecuteUpdateAsync(s=>s.SetProperty(n=>n.ReadAt,n=>n.CreatedAt>time?n.CreatedAt:time)));}
}
