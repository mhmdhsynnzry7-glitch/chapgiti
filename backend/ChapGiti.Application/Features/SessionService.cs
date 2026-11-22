using System.Security.Cryptography;
using System.Text;
using ChapGiti.Application.Authentication;

namespace ChapGiti.Application.Features;

public sealed class SessionService(
    ISessionRepository repository,
    IAccessSessionRepository credentials,
    IUnitOfWork unitOfWork,
    IRequestSession request,
    TimeProvider clock)
{
    private long Now => clock.GetUtcNow().ToUnixTimeMilliseconds();
    private static string Digest(string value, bool administrator = false) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("ChapGiti.Session.v2|" + (administrator ? "administrator|" : "customer|") + value)));
    private static string Token() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
    private static bool IsToken(string? token) => token != null && Regex.IsMatch(token, "^[a-f0-9]{64}$");
    public Task LockSecurity() => credentials.LockSecurity();

    public async Task<UseCaseResult> Status(bool administrator = false)
    {
        var identity = await Identity(administrator) ?? throw Expired();
        return UseCaseResult.Json(new { authenticated = true,
            isAdmin = administrator,
            isCustomer = identity.GetValueOrDefault("account_id") != null });
    }

    private Task<Dictionary<string, object?>?> Identity(bool administrator = false)
    {
        if (!IsToken(request.GetToken(administrator))) return Task.FromResult<Dictionary<string, object?>?>(null);
        request.RequireSecureTransport();
        return ResolveIdentity(administrator);
    }

    private async Task<Dictionary<string, object?>?> ResolveIdentity(bool administrator)
    {
        var row = await credentials.ResolveAccess(Digest(request.GetToken(administrator)!, administrator), Now);
        if (row == null || (row.GetValueOrDefault("legacy_admin_email") != null) != administrator) return null;
        return row;
    }

    public async Task<Dictionary<string, object?>?> Admin(bool required = true)
    {
        var identity = await Identity(true);
        if (identity == null)
        {
            if (required) throw Expired();
            return null;
        }
        var id = identity.Str("legacy_admin_email");
        var email = identity.Str("admin_email");
        return new Dictionary<string, object?>
        {
            ["id"] = id, ["email"] = email, ["username"] = AuthenticationPolicy.AdministratorUsername, ["legacy"] = false
        };
    }

    public async Task<Dictionary<string, object?>?> Customer(bool required = true)
    {
        var row = await Identity();
        if (row?.GetValueOrDefault("account_id") == null) row = null;
        if (required && (row == null || !Ready(row)))
            throw new UseCaseException(ErrorKind.Unauthenticated, "ابتدا وارد حساب شوید و مشخصات خود را تکمیل کنید.");
        return row;
    }
    public static bool Ready(Dictionary<string, object?> row) => row.GetValueOrDefault("account_id") != null &&
        row.Str("first_name") != "" && row.Str("last_name") != "";

    public static CustomerProfileResponse Profile(Dictionary<string, object?> row) => new(
        row.Str("id"), row.Str("phone"), row.Str("first_name"), row.Str("last_name"),
        Convert.ToBoolean(row["phone_verified"]), row.GetValueOrDefault("account_id") != null,
        false);

    public async Task Session(bool admin, string identity)
    {
        request.RequireSecureTransport();
        var access = Token();
        var refresh = Token();
        var now = Now;
        var expires = now + AuthenticationPolicy.RefreshSeconds * 1000L;
        await unitOfWork.Transaction(async () =>
        {
            await credentials.LockSecurity();
            await RevokeCurrent(now, admin);
            var session = new AccessSession(Input.NewId(), admin ? null : identity, admin ? identity : null,
                Digest(access, admin), now + AuthenticationPolicy.AccessSeconds * 1000L, expires, null);
            await credentials.InsertSession(session, new(Digest(refresh, admin), session.Id, expires, null), now);
            return true;
        });
        SetCookies(access, refresh, expires, admin);
    }

    public async Task<UseCaseResult> Refresh(bool admin = false)
    {
        request.RequireSecureTransport();
        await Limit(admin ? "admin-refresh" : "customer-refresh", 60);
        var token = request.GetRefreshToken(admin);
        if (!IsToken(token)) throw Expired();
        var access = Token();
        var refresh = Token();
        long expires = 0;
        var outcome = await unitOfWork.Transaction(async () =>
        {
            await credentials.LockSecurity();
            var previous = await credentials.FindRefresh(Digest(token!, admin));
            if (previous == null) return "invalid";
            var session = await credentials.FindSession(previous.SessionId);
            var now = Now;
            if (session == null || (session.LegacyAdminEmail != null) != admin || session.RevokedAt != null || session.Expires <= now || previous.Expires <= now)
                return "invalid";
            if (previous.ConsumedAt != null)
            {
                // A concurrent loser neither erases the winner's cookies nor receives credentials.
                if (now - previous.ConsumedAt <= AuthenticationPolicy.ConcurrentRefreshSeconds * 1000L)
                    return "concurrent";
                await credentials.RevokeSession(session.Id, now);
                await Audit(session.CustomerId ?? session.LegacyAdminEmail!, "ابطال نشست به علت استفادهٔ دوباره از مجوز تمدید");
                return "reused"; // Commit revocation before returning 401.
            }
            expires = session.Expires;
            await credentials.Rotate(session with { AccessHash = Digest(access, admin),
                AccessExpires = Math.Min(expires, now + AuthenticationPolicy.AccessSeconds * 1000L) }, previous,
                new(Digest(refresh, admin), session.Id, expires, null), now);
            return "ok";
        });
        if (outcome == "concurrent")
            throw new UseCaseException(ErrorKind.Conflict, "نشست هم‌زمان تمدید شد؛ درخواست را دوباره انجام دهید.", 1);
        if (outcome != "ok") throw Expired();
        SetCookies(access, refresh, expires, admin);
        return UseCaseResult.Json(new { ok = true, accessExpiresIn = AuthenticationPolicy.AccessSeconds });
    }

    private void SetCookies(string access, string refresh, long expires, bool admin)
    {
        request.SetToken(admin, access, TimeSpan.FromMilliseconds(Math.Min(AuthenticationPolicy.AccessSeconds * 1000L, expires - Now)));
        request.SetRefreshToken(refresh, TimeSpan.FromMilliseconds(Math.Max(0, expires - Now)), admin);
    }

    private async Task RevokeCurrent(long now, bool admin)
    {
        var refresh = request.GetRefreshToken(admin);
        if (IsToken(refresh))
        {
            var known = await credentials.FindRefresh(Digest(refresh!, admin));
            if (known != null && await credentials.FindSession(known.SessionId) is { } family &&
                (family.LegacyAdminEmail != null) == admin) await credentials.RevokeSession(known.SessionId, now);
        }
        var access = request.GetToken(admin);
        var identity = IsToken(access) ? await credentials.ResolveAccess(Digest(access!, admin), now) : null;
        if (identity != null && (identity.GetValueOrDefault("legacy_admin_email") != null) == admin) await credentials.RevokeSession(identity.Str("session_id"), now);
    }

    public Task RevokeCustomer(string id) => credentials.RevokeCustomer(id, Now);
    public Task RevokeLegacyAdmin(string email) => credentials.RevokeLegacyAdmin(email, Now);

    public async Task Logout(bool admin = false)
    {
        await unitOfWork.Transaction(async () => { await credentials.LockSecurity(); await RevokeCurrent(Now, admin); return true; });
        request.SetToken(admin, "", TimeSpan.Zero);
        request.SetRefreshToken("", TimeSpan.Zero, admin);
    }

    public async Task Limit(string scope, int max)
    {
        var key = scope + ":" + Digest(request.ClientAddress);
        if (await repository.IncrementRateLimit(key, Now, Now + 900000) > max)
            throw new UseCaseException(ErrorKind.TooManyRequests, "تعداد درخواست‌ها زیاد است. ۱۵ دقیقه دیگر تلاش کنید.");
    }
    public async Task LimitSubject(string scope, string subject, int max)
    {
        if (await repository.IncrementRateLimit(scope + ":" + Digest(subject), Now, Now + 900000) > max)
            throw new UseCaseException(ErrorKind.TooManyRequests, "تعداد تلاش‌ها زیاد است. ۱۵ دقیقه دیگر تلاش کنید.");
    }
    public Task Audit(string actor, string action) => repository.Audit(Input.NewId(), actor, action, Input.Now());
    private static UseCaseException Expired() => new(ErrorKind.Unauthenticated, "نشست منقضی شده است. دوباره وارد شوید.");
}
