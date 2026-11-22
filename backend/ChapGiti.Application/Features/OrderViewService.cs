using ChapGiti.Application.Notifications;
using ChapGiti.Application.Realtime;

namespace ChapGiti.Application.Features;

public sealed class OrderViewService(IOrderViewRepository views, IAdminOrderRepository orders, SessionService sessions,
    IUnitOfWork unitOfWork, CommittedUpdates updates)
{
    public Task<bool> Available() => views.SupportsViews();
    public Task<long> Unseen(NotificationRecipient recipient) => views.UnseenOrders(recipient);
    public Task<long> Latest(string orderId, NotificationRecipient recipient) => views.LatestSequence(orderId, recipient);
    public async Task<UseCaseResult> Mark(string orderId, bool administrator, JsonObject body)
    {
        var identity = administrator ? (await sessions.Admin())! : (await sessions.Customer())!;
        var recipient = new NotificationRecipient(administrator ? "admin" : "customer", identity.Str("id"));
        if (!await Available()) throw new UseCaseException(ErrorKind.Unavailable, "ثبت مشاهدهٔ سفارش هنوز روی این سرور فعال نشده است.", code: "ORDER_VIEWS_UNAVAILABLE");
        var through = Input.Number(body, "seenThroughSequence", 0, 9_007_199_254_740_991);
        await unitOfWork.Transaction(async () => {
            var order = await orders.LockOrder(orderId) ?? throw new UseCaseException(ErrorKind.NotFound, "سفارش پیدا نشد.");
            if (!administrator && order.Str("customer_id") != identity.Str("id")) throw new UseCaseException(ErrorKind.NotFound, "سفارش پیدا نشد.");
            if (through > await views.LatestSequence(orderId, recipient))
                throw new UseCaseException(ErrorKind.Invalid, "شمارهٔ مشاهده از نسخهٔ نمایش‌داده‌شده جلوتر است.", code: "ORDER_VIEW_SEQUENCE_INVALID");
            if (await views.MarkViewed(orderId, recipient, through, Input.Now()))
                updates.Enqueue(RealtimeUpdate.Notifications(recipient, "order-view:" + Input.NewId()));
            return true;
        });
        return UseCaseResult.Json(new { ok = true });
    }
}
