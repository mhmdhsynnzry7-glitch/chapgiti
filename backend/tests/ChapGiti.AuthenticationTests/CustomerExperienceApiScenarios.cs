using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ChapGiti.Application.Common;

namespace ChapGiti.AuthenticationTests;

internal static class CustomerExperienceApiScenarios
{
    public static async Task Run(Action<bool, string> check)
    {
        await using var host = await TestApiHost.Start();
        using var client = new HttpClient(new HttpClientHandler { UseCookies = false }) { BaseAddress = new Uri(host.Origin) };
        var cookies = new Dictionary<string, string>();
        async Task<JsonNode> Request(string method, string path, object? body = null, int expected = 200, bool authenticated = true)
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), "/api/" + path);
            request.Headers.Add("Origin", host.Origin);
            if (authenticated && cookies.Count > 0) request.Headers.Add("Cookie", string.Join("; ", cookies.Select(pair => pair.Key + "=" + pair.Value)));
            if (body != null) request.Content = JsonContent.Create(body);
            using var response = await client.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();
            if ((int)response.StatusCode != expected) throw new InvalidOperationException(path + " expected " + expected + " got " + (int)response.StatusCode + ": " + text);
            if (response.Headers.TryGetValues("Set-Cookie", out var values)) foreach (var value in values)
            {
                var pair = value.Split(';')[0].Split('=', 2);
                if (pair[0] is "giti_customer_access" or "giti_customer_refresh" or "giti_admin_access" or "giti_admin_refresh") { if (pair[1] == "") cookies.Remove(pair[0]); else cookies[pair[0]] = pair[1]; }
            }
            check(true, "customer UX HTTP " + method + " " + path + " -> " + expected);
            return JsonNode.Parse(text)!;
        }
        const string phone = "09177777777";
        var start = await Request("POST", "customer/start", new { phone = "۰۹۱۷۷۷۷۷۷۷۷" });
        check(start["step"]!.ToString() == "register" && start["phone"]!.ToString() == phone && host.Fixture.Sender.Sends == 0 && host.Fixture.Store.Pending.Count == 0,
            "mobile-first registration normalizes digits and performs no SMS, pending-registration or account write");
        var existing = await Request("POST", "customer/start", new { phone = AuthFixture.Phone });
        check(existing["step"]!.ToString() == "password" && existing.AsObject().Count == 3,
            "existing mobile lookup leads to password login without returning personal information");
        await Request("POST", "customer/start", new { phone = "invalid" }, 400);
        await Request("POST", "customer/start", new { phone, firstName = "unexpected" }, 400);
        await Request("POST", "customer/otp/request", new { phone, purpose = "register", firstName = "Premature" }, 400);
        await Request("POST", "customer/otp/request", new { phone = AuthFixture.Phone, purpose = "register" }, 409);
        check(host.Fixture.Sender.Sends == 0, "existing registration is rejected before sending another OTP");

        var challenge = await Request("POST", "customer/otp/request", new { phone, purpose = "register" });
        await Request("POST", "customer/otp/verify", new { challengeId = challenge["challengeId"]!.ToString(), code = "9999" }, 400);
        var registration = await Request("POST", "customer/otp/verify", new { challengeId = challenge["challengeId"]!.ToString(), code = "۰۱۲۳" });
        check(!host.Fixture.Store.Accounts.ContainsKey(phone), "OTP verification does not create a customer before profile completion");
        await Request("POST", "customer/registration/complete", AuthFixture.RegistrationBody(registration.AsObject()));
        check(cookies.Count == 0, "successful mobile-first registration still requires password login");
        await Request("POST", "customer/otp/verify", new { challengeId = challenge["challengeId"]!.ToString(), code = "0123" }, 400);
        await Request("POST", "customer/login", new { phone, password = "Wrong.Password@1" }, 401);
        await Request("POST", "customer/login", new { phone, password = AuthFixture.Password });
        check(cookies.ContainsKey("giti_customer_access") && cookies.ContainsKey("giti_customer_refresh"), "a newly registered customer receives persistent customer-only session cookies after password login");
        var profile = await Request("GET", "customer/me");
        check(profile["phone"]!.ToString() == phone && profile["phoneVerified"]!.GetValue<bool>(), "new-account session resolves the correct verified mobile owner");
        var registered = await Request("POST", "customer/start", new { phone });
        check(registered["step"]!.ToString() == "password", "a completed registration is immediately recognized as an existing account");
        await Request("POST", "customer/otp/request", new { phone, purpose = "register" }, 409);
        check(host.Fixture.Sender.Sends == 1 && host.Fixture.Passwords.Verify(AuthFixture.Password, host.Fixture.Store.Accounts[phone].Hash), "a stale registration profile cannot replace a registered password or trigger a second SMS");

        var empty = await Request("GET", "customer/orders?page=1&pageSize=10");
        check(empty["total"]!.GetValue<int>() == 0 && empty["items"]!.AsArray().Count == 0, "empty customer order lists have a valid paginated response");
        check(await Request("GET", "customer/orders") is JsonArray, "the pre-existing unpaginated order API keeps its array contract");
        var owner = host.Fixture.Store.Accounts[phone].Id;
        var other = host.Fixture.Store.Accounts[AuthFixture.Phone].Id;
        var states = new[] { "new", "review", "confirmed", "printing", "binding", "ready", "completed", "cancelled" };
        string AddOrder(int n, string customerId, string mobile)
        {
            var id = Guid.NewGuid().ToString();
            var created = DateTimeOffset.Parse("2026-10-01T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture).AddMinutes(n).ToString("O");
            host.Data.Orders[id] = new() {
                ["id"] = id, ["code"] = n.ToString("D20"), ["title"] = "Order " + n, ["customer_id"] = customerId,
                ["phone"] = mobile, ["customer"] = "Test Customer", ["email"] = "", ["service"] = "book-printing",
                ["status"] = states[n % states.Length], ["estimate"] = 123456L, ["final_price"] = 246800L,
                ["created_at"] = created, ["updated_at"] = created, ["version"] = 1,
                ["spec"] = "{\"priceBreakdown\":{\"print\":123,\"binding\":456,\"total\":123456}}", ["admin_notes"] = "internal-only"
            };
            host.Data.Events.Add(new() { ["id"] = Guid.NewGuid().ToString(), ["order_id"] = id, ["status"] = states[n % states.Length], ["note"] = "Public status note", ["created_at"] = created });
            return id;
        }
        var ids = Enumerable.Range(0, 23).Select(n => AddOrder(n, owner, phone)).ToArray();
        var foreign = AddOrder(99, other, AuthFixture.Phone);
        var historical = host.Data.Orders[ids[0]]["spec"]!.ToString();
        var first = await Request("GET", "customer/orders?page=1&pageSize=10");
        var last = await Request("GET", "customer/orders?page=3&pageSize=10");
        check(first["total"]!.GetValue<int>() == 23 && first["items"]!.AsArray().Count == 10 && last["items"]!.AsArray().Count == 3,
            "all customer orders are reachable through bounded pagination beyond the former twenty-order limit");
        check(!first.ToJsonString().Contains(foreign) && !first.ToJsonString().Contains("internal-only") && !first.ToJsonString().Contains("priceBreakdown"),
            "customer order summaries exclude other owners and private pricing/admin fields");
        foreach (var query in new[] { "page=0", "page=-1", "page=100001", "page=1&pageSize=0", "page=1&pageSize=51" })
            await Request("GET", "customer/orders?" + query, expected: 400);
        await Request("GET", "customer/orders?page=1", expected: 401, authenticated: false);
        await Request("GET", "customer/orders/" + foreign, expected: 404);
        await Request("GET", "customer/orders/" + Guid.NewGuid(), expected: 404);
        await Request("GET", "customer/orders/" + ids[0], expected: 401, authenticated: false);
        var detail = await Request("GET", "customer/orders/" + ids[0]);
        check(detail["order"]!["estimate"]!.GetValue<int>() == 123456 && detail["order"]!["final_price"]!.GetValue<int>() == 246800 && detail["events"]!.AsArray().Count == 1,
            "owned order details expose authoritative totals and public order history");
        await Request("POST", "track", new { code = host.Data.Orders[ids[0]].Str("code") }, 401, authenticated: false);
        var tracked = await Request("POST", "track", new { code = host.Data.Orders[ids[0]].Str("code").ToLowerInvariant(), phone = "۰۹۱۷۷۷۷۷۷۷۷" });
        check(JsonNode.DeepEquals(detail["events"], tracked["events"]) && detail["order"]!["id"]!.ToString() == tracked["order"]!["id"]!.ToString(),
            "owner tracking and authenticated details reuse the same tracking history behavior");
        await Request("POST", "track", new { code = host.Data.Orders[ids[0]].Str("code"), phone = AuthFixture.Phone }, 404);
        var cancelled = await Request("GET", "customer/orders/" + ids[7]);
        check(cancelled["order"]!["status"]!.ToString() == "cancelled", "cancelled remains a supported readable order state");
        check(host.Data.Orders[ids[0]]["spec"]!.ToString() == historical, "customer list/detail/tracking reads preserve the stored historical price breakdown");

        await Request("POST", "customer/login", new { phone = AuthFixture.Phone, password = AuthFixture.Password });
        await Request("GET", "admin/overview", expected: 401);
        await Request("POST", "auth/login", new { username = "admin", password = AuthFixture.Password });
        await Request("GET", "admin/overview");
        var adminDetail = await Request("GET", "admin/orders/" + ids[0]);
        check(adminDetail["order"]!["spec"]!["priceBreakdown"] != null && host.Data.Orders[ids[0]]["spec"]!.ToString() == historical,
            "administrators keep access to stored historical calculation details without rewriting them");
        await Request("GET", "customer/orders/" + ids[0], expected: 404);
        check(true, "independently authenticated administrators retain access while customer order routes enforce customer ownership");
    }
}
