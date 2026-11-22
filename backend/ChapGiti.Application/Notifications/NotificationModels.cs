namespace ChapGiti.Application.Notifications;

public sealed record NotificationRecipient(string Kind, string Id);
public sealed record NotificationItem(string Id, string EventType, string EntityKind, string EntityId,
    string Title, string Body, string TargetPath, string CreatedAt, string? ReadAt, string? OrderCode = null);
public sealed record NotificationSummary(long UnreadCount, long LatestSequence);
public sealed record StoredNotification(NotificationRecipient Recipient, NotificationItem Item, string DedupeKey);

public static class NotificationTargets
{
    public static string For(NotificationRecipient recipient, string entityKind, string entityId)
    {
        if (!Guid.TryParseExact(entityId, "D", out var id) || recipient.Kind is not ("customer" or "admin"))
            throw new ArgumentException("Invalid notification reference.");
        return (recipient.Kind, entityKind) switch
        {
            ("customer", "order") => "/account/orders/" + id,
            ("admin", "order") => "/admin/orders/" + id,
            ("customer", "ticket") => "/tickets/" + id,
            ("admin", "ticket") => "/admin/tickets/" + id,
            _ => throw new ArgumentException("Unsupported notification target.")
        };
    }
}
