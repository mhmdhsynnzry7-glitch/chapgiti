using ChapGiti.Application.Notifications;
using ChapGiti.Domain.Notifications;
using ChapGiti.Application.Realtime;

namespace ChapGiti.Application.Features;

/// <summary>Projects committed business events into the in-app channel, inside the caller's UoW.</summary>
public sealed class NotificationPublisher(INotificationRepository repository, CommittedUpdates? updates = null)
{
    public async Task Publish(BusinessNotification notification)
    {
        // Compatibility with installations before 007. No startup migration/backfill or invented events.
        if (!await repository.SupportsNotifications()) return;
        if (!Guid.TryParseExact(notification.EventId, "D", out _))
            throw new ArgumentException("Invalid business event identity.");
        var (eventType, entity, title, body) = Content(notification.Type);
        var recipients = notification.ToAdministrators
            ? (await repository.AdministratorRecipients()).Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(id => new NotificationRecipient("admin", id))
            : string.IsNullOrEmpty(notification.CustomerId) ? [] : [new NotificationRecipient("customer", notification.CustomerId)];
        foreach (var recipient in recipients)
        {
            var item = new NotificationItem(Input.NewId(), eventType, entity, notification.EntityId,
                title, body, NotificationTargets.For(recipient, entity, notification.EntityId), Input.Now(), null);
            var key = eventType + ":" + notification.EventId;
            if (await repository.InsertNotification(new(recipient, item, key)))
                updates?.Enqueue(RealtimeUpdate.Notifications(recipient, key));
        }
    }

    public Task Status(string status, string eventId, string orderId, string? customerId) => status switch
    {
        "printing" => Publish(new(NotificationEvent.PrintingStarted, eventId, orderId, customerId)),
        "binding" => Publish(new(NotificationEvent.BindingStarted, eventId, orderId, customerId)),
        "ready" => Publish(new(NotificationEvent.OrderReady, eventId, orderId, customerId)),
        "completed" => Publish(new(NotificationEvent.OrderCompleted, eventId, orderId, customerId)),
        "cancelled" => Publish(new(NotificationEvent.RequestRejected, eventId, orderId, customerId)),
        _ => Task.CompletedTask
    };

    // Deliberately no user-entered text, price, phone, PDF URL or private administrative note.
    private static (string EventType, string Entity, string Title, string Body) Content(NotificationEvent type) => type switch
    {
        NotificationEvent.RequestCreated => ("order.created", "order", "درخواست چاپ جدید", "یک درخواست چاپ برای بررسی ثبت شده است."),
        NotificationEvent.DeliveryTermsIssued => ("order.terms_issued", "order", "شرایط نهایی آماده است", "در جزئیات سفارش، فایل و شرایط تحویل را بررسی کنید."),
        NotificationEvent.DeliveryTermsAmendmentRequested => ("order.terms_amendment", "order", "درخواست اصلاح شرایط", "مشتری برای شرایط نهایی چاپ و تحویل درخواست اصلاح ثبت کرد."),
        NotificationEvent.DeliveryTermsAccepted => ("order.terms_accepted", "order", "تأیید شرایط تحویل", "مشتری شرایط نهایی چاپ و تحویل را پذیرفت."),
        NotificationEvent.QuoteIssued => ("quote.issued", "order", "پیشنهاد قیمت آماده است", "پیشنهاد فعلی را ببینید و تصمیم خود را ثبت کنید."),
        NotificationEvent.QuoteAccepted => ("quote.accepted", "order", "تأیید پیشنهاد توسط مشتری", "مشتری قیمت را پذیرفته است. فایل و شرایط نهایی را بررسی کنید."),
        NotificationEvent.QuoteRevisionRequested => ("quote.revision_requested", "order", "درخواست اصلاح پیشنهاد", "مشتری درخواست اصلاح پیشنهاد قیمت را ثبت کرده است."),
        NotificationEvent.QuoteDeclined => ("quote.declined", "order", "انصراف مشتری", "مشتری از ادامهٔ این درخواست انصراف داده است."),
        NotificationEvent.InformationRequested => ("order.information_requested", "order", "تکمیل اطلاعات درخواست", "چاپخانه برای ادامهٔ بررسی به اطلاعات یا فایل شما نیاز دارد."),
        NotificationEvent.InformationProvided => ("order.information_provided", "order", "اطلاعات تکمیلی دریافت شد", "مشتری اطلاعات درخواستی را تکمیل کرده است."),
        NotificationEvent.CorrectionApproved => ("order.correction_approved", "order", "اصلاحیه تأیید شد", "چاپخانه آخرین اصلاحیه را تأیید کرده است؛ جزئیات سفارش را ببینید."),
        NotificationEvent.CorrectionRejected => ("order.correction_rejected", "order", "اصلاحیه نیاز به بررسی دارد", "اصلاحیه تأیید نشده است؛ دلیل چاپخانه را در جزئیات سفارش ببینید."),
        NotificationEvent.RequestRejected => ("order.rejected", "order", "نتیجهٔ بررسی درخواست", "چاپخانه درخواست را رد کرده؛ دلیل را در جزئیات ببینید."),
        NotificationEvent.PrintingStarted => ("order.printing", "order", "چاپ شروع شد", "درخواست شما وارد مرحلهٔ چاپ شده است."),
        NotificationEvent.BindingStarted => ("order.binding", "order", "شروع صحافی", "درخواست شما وارد مرحلهٔ صحافی شده است."),
        NotificationEvent.OrderReady => ("order.ready", "order", "سفارش آمادهٔ تحویل است", "جزئیات سفارش آمادهٔ خود را ببینید."),
        NotificationEvent.OrderCompleted => ("order.completed", "order", "تحویل سفارش ثبت شد", "تحویل سفارش شما در سامانه ثبت شده است."),
        NotificationEvent.TicketCreated => ("ticket.created", "ticket", "درخواست پشتیبانی جدید", "یک گفت‌وگوی جدید در صندوق پشتیبانی ثبت شده است."),
        NotificationEvent.ConsultationRequested => ("consultation.created", "ticket", "درخواست مشاوره جدید", "یک درخواست مشاوره در صندوق پشتیبانی ثبت شده است."),
        NotificationEvent.TicketMessage => ("ticket.message", "ticket", "پیام جدید پشتیبانی", "پیام تازه‌ای در گفت‌وگوی پشتیبانی دارید."),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
