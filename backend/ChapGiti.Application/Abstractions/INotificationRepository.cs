using ChapGiti.Application.Notifications;

namespace ChapGiti.Application.Abstractions;

public interface INotificationRepository
{
    Task<bool> SupportsNotifications();
    Task<List<string>> AdministratorRecipients();
    // Must participate in the current business transaction; a duplicate is a no-op.
    Task<bool> InsertNotification(StoredNotification notification);
    Task<List<NotificationItem>> ListNotifications(NotificationRecipient recipient, int skip, int take);
    Task<long> CountNotifications(NotificationRecipient recipient);
    Task<NotificationSummary> NotificationSummary(NotificationRecipient recipient);
    Task<bool> ReadNotification(NotificationRecipient recipient, string id, string now);
    Task ReadAllNotifications(NotificationRecipient recipient, string now);
}
