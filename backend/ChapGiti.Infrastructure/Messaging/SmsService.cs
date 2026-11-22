using System.Text.Json.Nodes;
using ChapGiti.Application.Features;
using ChapGiti.Application.Authentication;
using ChapGiti.Domain.Orders;
using ChapGiti.Infrastructure.Persistence;
namespace ChapGiti.Infrastructure.Messaging;

internal sealed class SmsService(SqlStore db, IConfiguration config, ContentService content, IHttpClientFactory clients, OtpVerificationPolicy verification) : ISmsNotifier
{
    public SmsAvailability Availability => new(Enabled, Configured, !string.IsNullOrWhiteSpace(config["Sms:BodyId"]));
    public bool Enabled => verification.Enabled && config.GetValue<bool>("Sms:Enabled");
    public bool Configured => !string.IsNullOrWhiteSpace(config["Sms:Username"]) && !string.IsNullOrWhiteSpace(config["Sms:Password"]) && (!string.IsNullOrWhiteSpace(config["Sms:BodyId"]) || !string.IsNullOrWhiteSpace(config["Sms:From"]));
    public async Task Notify(string orderId, string code, string phone, string status, string eventId)
    {
        if (!verification.Enabled)
            return;
        var id = Input.NewId();
        var inserted = await db.Execute("INSERT dbo.Sms_Logs(id,event_id,order_id,phone,status,created_at) SELECT @p0,@p1,@p2,@p3,@p4,@p5 WHERE NOT EXISTS(SELECT 1 FROM dbo.Sms_Logs WITH(UPDLOCK,HOLDLOCK) WHERE event_id=@p1)", id, eventId, orderId, phone, Enabled && Configured ? "sending" : "disabled", Input.Now());
        if (inserted == 0 || !Enabled || !Configured)
            return;
        try
        {
            var template = config["Sms:BodyId"];
            var site = await content.Read("site", "settings");
            var form = new Dictionary<string, string> { { "username", config["Sms:Username"]! }, { "password", config["Sms:Password"]! }, { "to", phone } };
            if (!string.IsNullOrWhiteSpace(template))
            {
                form["bodyId"] = template;
                form["text"] = code + ";" + OrderWorkflow.Labels.GetValueOrDefault(status, status);
            }
            else
            {
                form["from"] = config["Sms:From"]!;
                form["text"] = site["brand"]!.GetValue<string>() + "\nسفارش " + code + "\nوضعیت: " + OrderWorkflow.Labels.GetValueOrDefault(status, status);
                form["isFlash"] = "false";
            }
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://rest.payamak-panel.com/api/SendSMS/" + (string.IsNullOrWhiteSpace(template) ? "SendSMS" : "BaseServiceNumber")) { Content = new FormUrlEncodedContent(form) };
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var response = await clients.CreateClient().SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException("HTTP_" + (int)response.StatusCode);
            var result = JsonNode.Parse(await response.Content.ReadAsStringAsync());
            var receipt = result?["Value"]?.ToString();
            if (result?["RetStatus"]?.ToString() != "1" || receipt == null || !System.Text.RegularExpressions.Regex.IsMatch(receipt, @"^\d{10,}$"))
                throw new InvalidOperationException("PROVIDER_REJECTED");
            await db.Execute("UPDATE dbo.Sms_Logs SET status='accepted',receipt=@p0 WHERE id=@p1", receipt, id);
        }
        catch (Exception error)
        {
            var errorCode = System.Text.RegularExpressions.Regex.IsMatch(error.Message, @"^(HTTP_\d+|PROVIDER_REJECTED)$") ? error.Message : "NETWORK_OR_RESPONSE_ERROR";
            await db.Execute("UPDATE dbo.Sms_Logs SET status='failed',error=@p0 WHERE id=@p1", errorCode, id);
        }
    }
}

