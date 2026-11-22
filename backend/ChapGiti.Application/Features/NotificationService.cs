using ChapGiti.Application.Notifications;
using ChapGiti.Application.Realtime;

namespace ChapGiti.Application.Features;

public sealed class NotificationService(INotificationRepository repository, SessionService sessions, OrderViewService views, ITicketRepository tickets, CommittedUpdates? updates = null)
{
    private async Task<NotificationRecipient> Recipient(bool administrator)
    {
        // Admin's immutable legacy key survives profile email changes. Never accept a client recipient ID.
        var identity = administrator ? (await sessions.Admin())! : (await sessions.Customer())!;
        var recipient = new NotificationRecipient(administrator ? "admin" : "customer", identity.Str("id"));
        if (!await repository.SupportsNotifications())
            throw new UseCaseException(ErrorKind.Unavailable, "مرکز اعلان‌ها هنوز روی این سرور فعال نشده است.", code: "NOTIFICATIONS_UNAVAILABLE");
        return recipient;
    }

    public async Task<UseCaseResult> List(bool administrator, int page, int pageSize)
    {
        var recipient = await Recipient(administrator);
        if (page is < 1 or > 100000 || pageSize is < 1 or > 50)
            throw new UseCaseException(ErrorKind.Invalid, "شماره صفحه یا تعداد اعلان‌ها معتبر نیست.");
        var total = await repository.CountNotifications(recipient);
        return UseCaseResult.Json(new { items = await repository.ListNotifications(recipient, (page - 1) * pageSize, pageSize), total, page, pageSize });
    }

    public async Task<UseCaseResult> Summary(bool administrator)
    {
        var recipient = await Recipient(administrator);
        var summary = await repository.NotificationSummary(recipient);
        return UseCaseResult.Json(new { summary.UnreadCount, summary.LatestSequence,
            unreadSupportCount = await tickets.UnreadSupport(administrator ? null : recipient.Id, administrator ? "customer" : "admin"),
            unseenOrderCount = await views.Unseen(recipient), orderViewsAvailable = await views.Available() });
    }

    public async Task<UseCaseResult> Read(bool administrator, string id)
    {
        var recipient = await Recipient(administrator);
        if (!await repository.ReadNotification(recipient, id, Input.Now()))
            throw new UseCaseException(ErrorKind.NotFound, "اعلان پیدا نشد.");
        if (updates != null) await updates.PublishCommitted(RealtimeUpdate.Notifications(recipient, "read:" + Input.NewId()));
        return UseCaseResult.Json(new { ok = true });
    }

    public async Task<UseCaseResult> ReadAll(bool administrator)
    {
        var recipient = await Recipient(administrator);
        await repository.ReadAllNotifications(recipient, Input.Now());
        if (updates != null) await updates.PublishCommitted(RealtimeUpdate.Notifications(recipient, "read-all:" + Input.NewId()));
        return UseCaseResult.Json(new { ok = true });
    }
}
