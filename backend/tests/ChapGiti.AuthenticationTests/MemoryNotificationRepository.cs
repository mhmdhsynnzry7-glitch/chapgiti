using ChapGiti.Application.Abstractions;
using ChapGiti.Application.Common;
using ChapGiti.Application.Notifications;

namespace ChapGiti.AuthenticationTests;

internal sealed class MemoryNotificationRepository(MemoryApiData data) : INotificationRepository
{
    public Task<bool> SupportsNotifications() => Task.FromResult(data.NotificationsAvailable);
    public Task<List<string>> AdministratorRecipients() => Task.FromResult(data.Store.AdminUsernames.TryGetValue("admin", out var id) ? new List<string>{id} : []);
    private IEnumerable<(StoredNotification Notice, long Sequence)> Owned(NotificationRecipient recipient) =>
        data.NotificationRows.Values.Where(row => row.Notice.Recipient == recipient);
    public Task<bool> InsertNotification(StoredNotification notification)
    {
        if (Owned(notification.Recipient).Any(row => row.Notice.DedupeKey == notification.DedupeKey)) return Task.FromResult(false);
        data.NotificationRows[notification.Item.Id] = (notification, ++data.NotificationSequence);
        if (data.FailNextNotification) { data.FailNextNotification = false; throw new UseCaseException(ErrorKind.Conflict, "Isolated notification persistence failure"); }
        return Task.FromResult(true);
    }
    public Task<List<NotificationItem>> ListNotifications(NotificationRecipient recipient, int skip, int take) =>
        Task.FromResult(Owned(recipient).OrderByDescending(row => row.Sequence).Skip(skip).Take(take).Select(row => {
            var item=row.Notice.Item;
            var id=item.EntityKind=="order" ? item.EntityId : data.Tickets.GetValueOrDefault(item.EntityId)?.Str("order_id");
            var order=id==null ? null : data.Orders.GetValueOrDefault(id);
            return item with { OrderCode=order!=null && (recipient.Kind=="admin" || order.Str("customer_id")==recipient.Id) ? order.Str("code") : null };
        }).ToList());
    public Task<long> CountNotifications(NotificationRecipient recipient) => Task.FromResult(Owned(recipient).LongCount());
    public Task<NotificationSummary> NotificationSummary(NotificationRecipient recipient) =>
        Task.FromResult(new NotificationSummary(Owned(recipient).LongCount(row => row.Notice.Item.ReadAt == null), Owned(recipient).Select(row => row.Sequence).DefaultIfEmpty().Max()));
    public Task<bool> ReadNotification(NotificationRecipient recipient, string id, string now)
    {
        if (!data.NotificationRows.TryGetValue(id, out var row) || row.Notice.Recipient != recipient) return Task.FromResult(false);
        data.NotificationRows[id] = (row.Notice with { Item = row.Notice.Item with { ReadAt = row.Notice.Item.ReadAt ?? now } }, row.Sequence);
        return Task.FromResult(true);
    }
    public async Task ReadAllNotifications(NotificationRecipient recipient, string now)
    {
        foreach (var id in Owned(recipient).Select(row => row.Notice.Item.Id).ToArray()) await ReadNotification(recipient, id, now);
    }
}
