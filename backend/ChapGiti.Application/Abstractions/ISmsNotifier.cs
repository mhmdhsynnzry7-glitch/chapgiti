namespace ChapGiti.Application.Abstractions;

public sealed record SmsAvailability(bool Enabled, bool Configured, bool Template);
public interface ISmsNotifier
{
    SmsAvailability Availability
    {
        get;
    }
    Task Notify(string orderId, string code, string phone, string status, string eventId);
}
