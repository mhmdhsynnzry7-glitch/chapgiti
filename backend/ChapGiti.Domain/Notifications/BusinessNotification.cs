namespace ChapGiti.Domain.Notifications;

// Channel independent events. No browser/device token, SMS or delivery transport lives here.
public enum NotificationEvent
{
    RequestCreated, QuoteIssued, QuoteAccepted, QuoteRevisionRequested, QuoteDeclined,
    InformationRequested, InformationProvided, RequestRejected, PrintingStarted,
    BindingStarted, OrderReady, OrderCompleted, TicketCreated, ConsultationRequested, TicketMessage,
    CorrectionApproved, CorrectionRejected, DeliveryTermsIssued, DeliveryTermsAccepted, DeliveryTermsAmendmentRequested
}

public sealed record BusinessNotification(NotificationEvent Type, string EventId, string EntityId,
    string? CustomerId, bool ToAdministrators = false);
