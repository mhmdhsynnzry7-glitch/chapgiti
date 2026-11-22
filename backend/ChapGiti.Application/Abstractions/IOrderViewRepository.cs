using ChapGiti.Application.Notifications;

namespace ChapGiti.Application.Abstractions;

public interface IOrderViewRepository
{
    Task<bool> SupportsViews();
    Task<long> UnseenOrders(NotificationRecipient recipient);
    Task<long> LatestSequence(string orderId, NotificationRecipient recipient);
    Task<bool> MarkViewed(string orderId, NotificationRecipient recipient, long throughSequence, string now);
}
