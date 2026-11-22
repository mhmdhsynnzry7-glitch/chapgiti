using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ChapGiti.Api.Http;
using ChapGiti.Application.Abstractions;
using ChapGiti.Application.Authentication;
using ChapGiti.Infrastructure;
using ChapGiti.Infrastructure.Messaging;
using ChapGiti.Application.Realtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ChapGiti.AuthenticationTests;

internal sealed class TestApiHost : IAsyncDisposable
{
    public AuthFixture Fixture {get;}
    public MemoryApiData Data {get;}
    public WebApplication App {get;}
    public string Origin => App.Urls.Single();
    private TestApiHost(AuthFixture fixture,MemoryApiData data,WebApplication app){Fixture=fixture;Data=data;App=app;}
    public static async Task<TestApiHost> Start(bool browser=false, bool production=false, bool otpVerificationEnabled=true,
        HttpMessageHandler? otpProviderHandler=null, ILoggerProvider? errorLogger=null)
    {
        var f=new AuthFixture();var actor=f.Existing(true);f.Store.Administrators.Add(actor);
        f.Store.LegacyAdmins[AuthFixture.AdminEmail]=f.Passwords.Hash(AuthFixture.Password);
        f.Clock.TrackWallClock=browser;
        var memory=new MemoryApiData();
        memory.Store=f.Store;f.Store.SnapshotExternalState=memory.Snapshot;
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions {EnvironmentName=production?"Production":"Development",Args=[]});
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?> {
            ["OTP_VERIFICATION_ENABLED"]=otpVerificationEnabled ? "true" : "false",
            ["Site:Url"]="http://127.0.0.1",["Otp:Enabled"]="true",["Otp:CodeLength"]="4",
            ["Otp:ApiKey"]="isolated-api-test-key-not-a-real-secret"
        });
        builder.Logging.ClearProviders();if(errorLogger!=null)builder.Logging.AddProvider(errorLogger);
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddInfrastructure();builder.Services.AddChapGitiApi();
        builder.Services.AddSingleton<IAccountRepository>(f.Store);
        builder.Services.AddSingleton<ISessionRepository>(f.Store);
        builder.Services.AddSingleton<IAccessSessionRepository>(f.Store);
        builder.Services.AddSingleton<IUserManagementRepository>(f.Store);
        builder.Services.AddSingleton<IOtpRepository>(f.Store);
        builder.Services.AddScoped<IUnitOfWork>(provider => new CommitAwareUnitOfWork(f.Store, provider.GetRequiredService<CommittedUpdates>()));
        builder.Services.AddSingleton<TimeProvider>(f.Clock);
        if (otpProviderHandler == null)
            builder.Services.AddSingleton<IOtpSender>(f.Sender);
        else
            builder.Services.AddHttpClient<IOtpSender, MelipayamakOtpSender>()
                .RemoveAllLoggers()
                .ConfigurePrimaryHttpMessageHandler(() => otpProviderHandler);
        builder.Services.AddSingleton<IContentDefaults>(memory);
        builder.Services.AddSingleton<IContentRepository>(memory);
        builder.Services.AddSingleton<ICutTariffRepository>(new MemoryCutTariffs(memory));
        builder.Services.AddSingleton<IPrintCatalogRepository>(new MemoryPrintCatalogRepository(memory));
        builder.Services.AddSingleton<IMagazineRepository>(new MemoryMagazineRepository(memory));
        builder.Services.AddSingleton<IPrivateFileStore>(memory);
        builder.Services.AddSingleton<ISmsNotifier>(memory);
        builder.Services.AddSingleton<IDatabaseMaintenance>(memory);
        builder.Services.AddSingleton(memory.Repository<IOrderRepository>());
        builder.Services.AddSingleton<ChapGiti.Application.Orders.IOrderInboxRepository>(new MemoryOrderInbox(memory));
        builder.Services.AddSingleton(memory.Repository<IOrderQuoteRepository>());
        builder.Services.AddSingleton<IOrderCorrectionRepository>(new MemoryOrderCorrectionRepository(memory));
        builder.Services.AddSingleton<IOrderViewRepository>(new MemoryOrderViewRepository(memory));
        builder.Services.AddSingleton(memory.Repository<IAdminOrderRepository>());
        builder.Services.AddSingleton(memory.Repository<IAdminContentRepository>());
        builder.Services.AddSingleton(memory.Repository<ITicketRepository>());
        builder.Services.AddSingleton<INotificationRepository>(new MemoryNotificationRepository(memory));
        builder.Services.AddSingleton<ICustomerActivityRepository>(new MemoryActivityRepository(memory,f.Store));
        var app=builder.Build();app.MapChapGitiApi();await app.StartAsync();
        var origin=app.Urls.Single();builder.Configuration["Site:Url"]=origin;
        if(browser && Environment.GetEnvironmentVariable("CHAP_GITI_TEST_SITE_URL") is string site)
        {
            var uri=new Uri(site);
            if(!uri.IsLoopback||uri.Scheme!="http")throw new InvalidOperationException("The test frontend must be loopback HTTP.");
            builder.Configuration["Site:Url"]=uri.GetLeftPart(UriPartial.Authority);
        }

        if(browser)
        {
            f.Store.LegacyAdmins["second.browser@example.test"]=f.Store.LegacyAdmins[AuthFixture.AdminEmail];
            for(var n=0;n<21;n++)
            {
                var id=Guid.NewGuid().ToString();var phone=(9100000000L+n).ToString().PadLeft(11,'0');
                f.Store.Profiles[id]=new(id,phone,"Sample","Customer "+n,true);
                f.Store.Accounts[phone]=(id,f.Store.Accounts[AuthFixture.Phone].Hash);
            }
            f.Store.LegacyAdmins["legacy@example.test"]=f.Passwords.Hash(AuthFixture.Password);
        }
        app.MapPost("/__test/control", async (Microsoft.AspNetCore.Http.HttpContext context) =>
        {
            var body=await System.Text.Json.Nodes.JsonNode.ParseAsync(context.Request.Body);
            ChapGiti.Application.Authentication.OtpChallengeResponse? staleRecovery = null;
            string? archiveTicketId=null;
            if(browser && body?["seedArchivedTicketFor"] is JsonValue archivePhone && archivePhone.TryGetValue<string>(out var archivedPhone) && f.Store.Accounts.TryGetValue(archivedPhone,out var archivedOwner))
            {
                archiveTicketId=WorkflowTestData.Ticket(memory,archivedOwner.Id,subject:"Archived browser conversation");
                var messageId=Guid.NewGuid().ToString();
                memory.MessagesById[messageId]=new(){["id"]=messageId,["ticket_id"]=archiveTicketId,["actor"]="admin",["message"]="Retained historical administrator reply",["created_at"]=DateTimeOffset.UtcNow.AddMilliseconds(1).ToString("O")};
                await new MemoryNotificationRepository(memory).InsertNotification(new(new("customer",archivedOwner.Id),
                    new(Guid.NewGuid().ToString(),"ticket.message","ticket",archiveTicketId,"پاسخ ثبت‌شدهٔ قبلی","این اعلان مربوط به سوابق گفت‌وگو است.","/tickets/"+archiveTicketId,ChapGiti.Domain.Common.Input.Now(),null),"historical:"+Guid.NewGuid()));
            }
            if(body?["advanceSeconds"]!=null)f.Clock.Advance(body["advanceSeconds"]!.GetValue<int>());
            if(body?["smsAvailable"]!=null)f.Sender.Available=body["smsAvailable"]!.GetValue<bool>();
            if(browser && body?["disconnectRealtime"]?.GetValue<bool>() == true)
                foreach(var connection in app.Services.GetRequiredService<ChapGiti.Api.Realtime.RealtimeConnections>().Current.ToArray()) connection.Context.Abort();
            if(body?["seedStaleRecovery"] is JsonValue staleValue && staleValue.TryGetValue<string>(out var stalePhone))
            {
                // Browser-only historical-state fixture; never a production route or SMS send.
                if (!browser || !System.Text.RegularExpressions.Regex.IsMatch(stalePhone, "^09[0-9]{9}$"))
                    return Microsoft.AspNetCore.Http.Results.BadRequest();
                var id = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
                var now = f.Clock.GetUtcNow().ToUnixTimeMilliseconds();
                var protector = app.Services.GetRequiredService<IOtpCodeProtector>();
                f.Store.Challenges[id] = new(id, stalePhone, "reset", null,
                    protector.Hash(id, stalePhone, "reset", "0123"), now, now + 120000, 0, false, "sent");
                staleRecovery = new(true, id, now + 120000, 60, 4, "Historical recovery fixture");
            }
            if(body?["failNextSave"]!=null)memory.FailNextSettingSave=body["failNextSave"]!.GetValue<bool>();
            if(body?["failNextOrderList"]!=null)memory.FailNextOrderList=body["failNextOrderList"]!.GetValue<bool>();
            if(body?["orderListDelayMs"]!=null)memory.OrderListDelayMs=Math.Clamp(body["orderListDelayMs"]!.GetValue<int>(),0,2000);
            if(body?["seedOrdersFor"] is JsonValue phoneValue&&phoneValue.TryGetValue<string>(out var phone)&&f.Store.Accounts.TryGetValue(phone,out var account))
            {
                var count=Math.Clamp(body["orderCount"]?.GetValue<int>()??0,0,30);
                var statuses=new[]{"new","review","confirmed","printing","binding","ready","completed","cancelled"};
                for(var n=0;n<count;n++)
                {
                    var id=Guid.NewGuid().ToString();var status=statuses[n%statuses.Length];
                    var created=DateTimeOffset.Parse("2026-10-01T10:00:00Z",System.Globalization.CultureInfo.InvariantCulture).AddMinutes(n).ToString("O");
                    memory.Orders[id]=new(){["id"]=id,["code"]=Guid.NewGuid().ToString("N")[..20].ToUpperInvariant(),["title"]="سفارش آزمایشی "+(n+1),
                        ["phone"]=phone,["customer_id"]=account.Id,["service"]="book-printing",["status"]=status,["estimate"]=123456L+n,
                        ["final_price"]=status is "new" or "review" or "cancelled"?null:246800L,["created_at"]=created,["updated_at"]=created,
                        ["spec"]="{\"priceBreakdown\":{\"print\":100,\"setup\":200}}",["admin_notes"]="internal-only",["version"]=1};
                    memory.Events.Add(new(){["id"]=Guid.NewGuid().ToString(),["order_id"]=id,["status"]="new",["note"]="درخواست شما ثبت شد.",["created_at"]=created});
                    if(status!="new")memory.Events.Add(new(){["id"]=Guid.NewGuid().ToString(),["order_id"]=id,["status"]=status,["note"]="وضعیت سفارش به‌روز شد.",["created_at"]=created});
                }
            }
            if(browser && body?["seedTicketsFor"] is JsonValue ticketPhoneValue &&
                ticketPhoneValue.TryGetValue<string>(out var ticketPhone) && f.Store.Accounts.TryGetValue(ticketPhone,out var ticketAccount))
            {
                // Synthetic pagination fixture only; never an application route or a real SMS.
                var count=Math.Clamp(body["ticketCount"]?.GetValue<int>()??0,0,45);
                for(var n=0;n<count;n++)
                {
                    var id=Guid.NewGuid().ToString();
                    var created=DateTimeOffset.Parse("2026-10-02T10:00:00Z",System.Globalization.CultureInfo.InvariantCulture).AddMinutes(n).ToString("O");
                    memory.Tickets[id]=new(){["id"]=id,["customer_id"]=ticketAccount.Id,["subject"]="Pagination fixture "+n,
                        ["status"]=n%2==0?"open":"closed",["created_at"]=created,["updated_at"]=created,["order_id"]=null};
                    memory.MessagesById[id]=new(){["id"]=id,["ticket_id"]=id,["actor"]="customer",["message"]="Synthetic pagination request",["created_at"]=created};
                }
            }
            return Microsoft.AspNetCore.Http.Results.Json(new {ok=true, staleRecovery, archiveTicketId, smsSends=f.Sender.Sends,
                otpChallenges=f.Store.Challenges.Values.Count(c=>c.DeliveryStatus!="grant"), accounts=f.Store.Accounts.Count});
        });
        return new(f,memory,app);
    }
    public async ValueTask DisposeAsync(){await App.StopAsync();await App.DisposeAsync();}
}
