using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ChapGiti.Application.Realtime;
using ChapGiti.Application.Notifications;
using ChapGiti.Api.Realtime;
using Microsoft.Extensions.DependencyInjection;

namespace ChapGiti.AuthenticationTests;

internal static class RealtimeScenarios
{
    private sealed class Recorder : IRealtimeDispatcher
    {
        public readonly List<RealtimeUpdate> Sent = [];
        public bool Committed, Fail;
        public Task Dispatch(RealtimeUpdate update)
        {
            if (!Committed) throw new InvalidOperationException("Before commit");
            Sent.Add(update); if (Fail) throw new IOException("isolated channel failure"); return Task.CompletedTask;
        }
    }
    public static async Task Run(Action<bool,string> check)
    {
        var recorder = new Recorder(); var buffer = new CommittedUpdates(recorder);
        var hint = RealtimeUpdate.Notifications(new("customer", "owner"), "event");
        await buffer.Transaction(async () => {
            buffer.Enqueue(hint); buffer.Enqueue(hint);
            await buffer.Transaction(() => { buffer.Enqueue(hint); return Task.FromResult(true); });
            check(recorder.Sent.Count == 0, "realtime nested/duplicate updates stay buffered until the outer commit");
            recorder.Committed = true; return true;
        });
        check(recorder.Sent.Count == 1, "realtime exact event is dispatched once after successful commit");
        try { await buffer.Transaction<bool>(() => { buffer.Enqueue(RealtimeUpdate.Ticket("ticket", "rolled-back")); throw new IOException("isolated rollback"); }); } catch (IOException) { }
        check(recorder.Sent.Count == 1, "realtime rolled-back operations dispatch nothing");
        await buffer.Transaction(() => Task.FromResult(true));
        check(recorder.Sent.Count == 1, "realtime rollback cannot leak buffered events into a later transaction");
        recorder.Fail = true;
        check(await buffer.Transaction(() => { buffer.Enqueue(RealtimeUpdate.Ticket("ticket", "committed")); return Task.FromResult(true); }),
            "realtime transport failure cannot fail an already committed business operation");

        await using var host = await TestApiHost.Start();
        var customerCookies = new CookieContainer(); var adminCookies = new CookieContainer(); var otherCookies = new CookieContainer();
        using var customer = new HttpClient(new HttpClientHandler { CookieContainer = customerCookies }) { BaseAddress = new(host.Origin) };
        using var admin = new HttpClient(new HttpClientHandler { CookieContainer = adminCookies }) { BaseAddress = new(host.Origin) };
        using var other = new HttpClient(new HttpClientHandler { CookieContainer = otherCookies }) { BaseAddress = new(host.Origin) };
        using var guest = new HttpClient { BaseAddress = new(host.Origin) };
        async Task<JsonNode> Request(HttpClient client, string method, string path, object? body = null, int expected = 200, string? origin = null)
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), "/api/" + path);
            request.Headers.Add("Origin", origin ?? host.Origin); if (body != null) request.Content = path is "orders" or "/api/orders" ? WorkflowTestData.PrintRequest(body) : JsonContent.Create(body);
            using var response = await client.SendAsync(request);
            check((int)response.StatusCode == expected, $"realtime HTTP {method} {path.Split('?')[0]} returns {expected}");
            var text = await response.Content.ReadAsStringAsync(); return text == "" ? new JsonObject() : JsonNode.Parse(text)!;
        }
        await Request(customer,"POST","customer/login",new { phone=AuthFixture.Phone,password=AuthFixture.Password });
        await Request(admin,"POST","auth/login",new { username="admin",password=AuthFixture.Password });
        var otherId=Guid.NewGuid().ToString(); const string otherPhone="09177777777";
        host.Fixture.Store.Profiles[otherId]=new(otherId,otherPhone,"Other","Owner",true);
        host.Fixture.Store.Accounts[otherPhone]=(otherId,host.Fixture.Store.Accounts[AuthFixture.Phone].Hash);
        await Request(other,"POST","customer/login",new { phone=otherPhone,password=AuthFixture.Password });
        foreach(var area in new[]{"notifications","support"})
        {
            await Request(guest,"POST","realtime/customer/"+area+"/negotiate",expected:401);
            await Request(guest,"POST","realtime/admin/"+area+"/negotiate",expected:401);
            await Request(customer,"POST","realtime/admin/"+area+"/negotiate",expected:401);
            await Request(admin,"POST","realtime/customer/"+area+"/negotiate",expected:401);
        }
        await Request(customer,"POST","realtime/customer/notifications/negotiate",expected:403,origin:"https://untrusted.example");
        await using var own=await RealtimeSocket.Open(host,customer,customerCookies,"customer","notifications");
        await using var foreign=await RealtimeSocket.Open(host,other,otherCookies,"customer","notifications");
        await using var management=await RealtimeSocket.Open(host,admin,adminCookies,"admin","notifications");
        check(true,"realtime authorized customer and administrator WebSocket connections coexist independently");
        var key=Guid.NewGuid().ToString(); object spec=new{pages=120,quantity=1,size="raghi",paper="white",color="bw",binding="soft"};
        var creation=new{idempotencyKey=key,title="Realtime isolated request",spec};
        var order=(await Request(customer,"POST","orders",creation,201))["id"]!.ToString();
        await RealtimeSocket.Eventually(()=>management.Events.Count==1);
        check(own.Events.IsEmpty && foreign.Events.IsEmpty,"realtime print requests notify only the administrator audience");
        await Request(customer,"POST","orders",creation);
        await Task.Delay(80);
        check(management.Events.Count==1,"realtime idempotent HTTP retries do not dispatch duplicate notification events");
        await Request(admin,"POST","admin/orders/"+order+"/open",new{});
        await Request(admin,"POST","admin/orders/"+order+"/quotes",new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=host.Data.Orders[order]["version"],amountToman=220000,scopeText="Isolated terms",deliveryTerms="",exclusionsText=""});
        await RealtimeSocket.Eventually(()=>own.Events.Count==1);
        check(foreign.Events.IsEmpty && management.Events.Count==1,"realtime quote notification reaches only its customer, never another customer or the admin inbox");
        var payload=own.Events.Single().AsObject();
        check(payload.Count==2 && payload["kind"]!.ToString()=="notifications" && payload["eventId"]!.ToString().Length==64,
            "realtime payload is an opaque invalidation hint without content, personal information, credentials or entity data");
        await using var secondDevice=await RealtimeSocket.Open(host,customer,customerCookies,"customer","notifications");
        var noticeId=host.Data.NotificationRows.Values.Single(row=>row.Notice.Item.EventType=="quote.issued").Notice.Item.Id;
        await Request(customer,"POST","customer/notifications/"+noticeId+"/read",new{});
        await RealtimeSocket.Eventually(()=>own.Events.Count==2&&secondDevice.Events.Count==1);
        check(foreign.Events.IsEmpty,"realtime read-state changes synchronize other devices of the same customer only");
        var ticket=(await Request(customer,"POST","tickets",new{subject="General support",message="Open conversation"},201))["id"]!.ToString();
        await using var thread=await RealtimeSocket.Open(host,customer,customerCookies,"customer","support");
        await using var agent=await RealtimeSocket.Open(host,admin,adminCookies,"admin","support");
        await using var outsider=await RealtimeSocket.Open(host,other,otherCookies,"customer","support");
        check((await thread.Invoke("JoinTicket",ticket))["error"]==null && (await agent.Invoke("JoinTicket",ticket))["error"]==null,"both authorized participants join");
        check((await outsider.Invoke("JoinTicket",ticket))["error"]!=null,"another customer cannot join");
        var reply=new{message="پاسخ پشتیبانی",idempotencyKey=Guid.NewGuid().ToString()};
        await Request(admin,"POST","admin/tickets/"+ticket+"/messages",reply,201);
        await Request(admin,"POST","admin/tickets/"+ticket+"/messages",reply);
        await RealtimeSocket.Eventually(()=>thread.Events.Count==1&&agent.Events.Count==1);
        await Request(customer,"POST","tickets/"+ticket+"/messages",new{message="تأیید است",idempotencyKey=Guid.NewGuid().ToString()},201);
        await RealtimeSocket.Eventually(()=>thread.Events.Count==2&&agent.Events.Count==2);
        check(outsider.Events.IsEmpty,"ticket messages are isolated and retries do not emit duplicates");
        await Request(customer,"POST","customer/logout",new{});
        var before=own.Events.Count;
        await host.App.Services.GetRequiredService<IRealtimeDispatcher>().Dispatch(RealtimeUpdate.Notifications(new("customer",host.Fixture.Store.Accounts[AuthFixture.Phone].Id),"logout-probe"));
        await host.App.Services.GetRequiredService<IRealtimeDispatcher>().Dispatch(RealtimeUpdate.Ticket(ticket,"logout-probe"));
        await RealtimeSocket.Eventually(()=>!host.App.Services.GetRequiredService<RealtimeConnections>().Current.Any(c=>c.Identity.Scope=="customer"&&c.Identity.Owner!=otherId));
        check(own.Events.Count==before,"revoked customer sessions receive no notification hints");
        check(host.App.Services.GetRequiredService<RealtimeConnections>().Current.Any(c=>c.Identity.Scope=="admin"),"customer logout preserves administrator notification connections");
        host.Fixture.Clock.Advance(16 * 60);
        await host.App.Services.GetRequiredService<IRealtimeDispatcher>().Dispatch(RealtimeUpdate.Notifications(new("customer",otherId),"expiry-probe"));
        check(foreign.Events.IsEmpty,"realtime expired access sessions cannot receive further hints");
    }
}
