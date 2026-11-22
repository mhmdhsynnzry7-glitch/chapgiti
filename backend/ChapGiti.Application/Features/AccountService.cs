using ChapGiti.Application.Authentication;

namespace ChapGiti.Application.Features;

public sealed class AccountService(
    IAccountRepository repository,
    IUnitOfWork unitOfWork,
    SessionService sessions,
    IPasswordHasher passwords)
{
    public async Task<UseCaseResult> GetAdmin()
    {
        var a = (await sessions.Admin())!;
        return UseCaseResult.Json(new AdministratorProfileResponse(a.Str("id"), a.Str("email"),
            a.Str("username"), Convert.ToBoolean(a["legacy"]), AuthenticationPolicy.AdministratorUsername));
    }

    public async Task<UseCaseResult> LoginAdmin(JsonObject body)
    {
        await sessions.Limit("login", 10);
        var username = Input.Text(body, "username", 1, 32);
        await sessions.LimitSubject("admin-password", AuthenticationPolicy.AdministratorUsername, 20);
        var password = Password(body);
        await unitOfWork.Transaction(async () =>
        {
            await sessions.LockSecurity();
            var row = username == AuthenticationPolicy.AdministratorUsername
                ? await repository.FindAdminByUsername(AuthenticationPolicy.AdministratorUsername) : null;
            if (!passwords.Verify(password, row?.Str("password_hash")))
                throw new UseCaseException(ErrorKind.Unauthenticated, "نام کاربری یا رمز عبور صحیح نیست.");
            var administratorId = row!.Str("id");
            await sessions.Session(true, administratorId);
            await sessions.Audit(administratorId, "ورود به نشست مستقل مدیریت");
            return true;
        });
        return UseCaseResult.Json(new
        {
            ok = true
        });
    }

    public async Task<UseCaseResult> LogoutAdmin()
    {
        await sessions.Logout(true);
        return UseCaseResult.Json(new
        {
            ok = true
        });
    }

    public async Task<UseCaseResult> StartCustomer(JsonObject body)
    {
        await sessions.Limit("customer-start", 30);
        var phone = Input.Phone(body);
        var existing = await repository.FindAccountOwner(phone);
        // This approved mobile-first lookup never sends OTP or creates an identity.
        return UseCaseResult.Json(new CustomerStartResponse(true, existing == null ? "register" : "password", phone));
    }

    public async Task<UseCaseResult> LoginCustomer(JsonObject body)
    {
        await sessions.Limit("customer-login", 15);
        var phone = Input.Phone(body);
        await sessions.LimitSubject("password-phone", phone, 20);
        var password = Password(body);
        // Serialize credential checks/session creation with password updates.
        await unitOfWork.Transaction(async () =>
        {
            await sessions.LockSecurity();
            var account = await repository.LockCustomerAccount(phone);
            // Verify even a missing account's dummy hash to retain the existing hashing cost.
            var passwordMatches = passwords.Verify(password, account?.Str("password_hash"));
            if (account is null)
                throw new UseCaseException(ErrorKind.Unauthenticated, CustomerLoginErrors.AccountNotFoundMessage,
                    code: CustomerLoginErrors.AccountNotFound);
            if (!passwordMatches)
                throw new UseCaseException(ErrorKind.Unauthenticated, CustomerLoginErrors.IncorrectPasswordMessage,
                    code: CustomerLoginErrors.IncorrectPassword);
            await sessions.Session(false, account.Str("registration_id"));
            return true;
        });
        return UseCaseResult.Json(new
        {
            ok = true
        });
    }

    public async Task<UseCaseResult> GetCustomer()
    {
        var customer = await sessions.Customer(false) ?? throw new UseCaseException(ErrorKind.Unauthenticated, "برای ادامه وارد حساب شوید.");
        return UseCaseResult.Json(SessionService.Profile(customer));
    }

    public async Task<UseCaseResult> SaveProfile(JsonObject body)
    {
        await sessions.Limit("customer-profile", 40);
        var customer = await sessions.Customer(false) ?? throw new UseCaseException(ErrorKind.Unauthenticated, "برای ادامه وارد حساب شوید.");
        if (body.Any(field => !new[] { "firstName", "lastName", "phone" }.Contains(field.Key)))
            throw new UseCaseException(ErrorKind.Invalid, "فقط نام و نام خانوادگی قابل ویرایش است.");
        var first = Input.Text(body, "firstName", 2, 80);
        var last = Input.Text(body, "lastName", 2, 80);
        var phone = body["phone"] == null ? customer.Str("phone") : Input.Phone(body);
        if (phone != customer.Str("phone"))
            throw new UseCaseException(ErrorKind.Invalid, "شماره موبایل حساب قابل تغییر نیست.");
        await unitOfWork.Transaction(async () =>
        {
            await sessions.LockSecurity();
            var existing = await repository.LockAccountOwner(phone);
            if (existing?.Str("registration_id") != customer.Str("id"))
                throw new UseCaseException(ErrorKind.Unauthenticated, "ابتدا وارد حساب شوید.");
            await repository.UpdateProfile(first, last, Input.Now(), customer.Str("id"));
            return true;
        });
        return UseCaseResult.Json(new
        {
            ok = true
        });
    }

    public async Task<UseCaseResult> LogoutCustomer()
    {
        await sessions.Logout(false);
        return UseCaseResult.Json(new
        {
            ok = true
        });
    }

    public async Task<UseCaseResult> RecentOrders()
    {
        var customer = await sessions.Customer();
        return UseCaseResult.Json(await repository.RecentOrders(customer!.Str("id")));
    }

    private static string Password(JsonObject body, string key = "password")
    {
        if (body[key] is not JsonValue value || !value.TryGetValue<string>(out var password) || password.Length is < 1 or > 128)
            throw new UseCaseException(ErrorKind.Invalid, "رمز عبور معتبر نیست.");
        return password;
    }

    public async Task SetupAdmin(string email, string username, string password)
    {
        email = email.Trim().ToLowerInvariant();
        if (username != AuthenticationPolicy.AdministratorUsername)
            throw new UseCaseException(ErrorKind.Invalid, "نام کاربری مدیر ثابت و برابر admin است.");
        if (!Regex.IsMatch(email, @"^[^\s@]+@[^\s@]+\.[^\s@]+$"))
            throw new UseCaseException(ErrorKind.Invalid, "Admin email or username is invalid.");
        var hash = passwords.Hash(password);
        await unitOfWork.Transaction(async () =>
        {
            await sessions.LockSecurity();
            var administrator = await repository.FindAdmin(email);
            var administratorId = administrator?.Str("id") ?? email;
            if (administrator == null && await repository.FindAdminById(email) != null)
                throw new UseCaseException(ErrorKind.Conflict, "این ایمیل به شناسهٔ تاریخی یک مدیر دیگر اختصاص دارد.");
            var owner = await repository.LockUsernameOwner(username);
            if (owner != null && owner.Str("email") != administratorId)
                throw new UseCaseException(ErrorKind.Conflict, "Username belongs to another account.");
            await repository.SaveAdmin(administratorId, hash, Input.Now());
            await repository.SaveUsername(administratorId, username);
            await repository.RevokeAdminSessions(administratorId);
            await sessions.RevokeLegacyAdmin(administratorId);
            return true;
        });
    }
}
