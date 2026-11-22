using System.Security.Cryptography;
using System.Text;
using ChapGiti.Application.Authentication;

namespace ChapGiti.Infrastructure.Persistence.Repositories;

internal sealed class OtpRepository : IOtpRepository
{
    private readonly IOtpSqlExecutor db;
    public OtpRepository(SqlStore db) : this((IOtpSqlExecutor)db) { }
    internal OtpRepository(IOtpSqlExecutor db) => this.db = db;

    public async Task LockPhone(string phone)
    {
        var key = "ChapGiti.Otp." + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(phone)));
        await db.Execute("""
            DECLARE @result int;
            EXEC @result = sp_getapplock @Resource=@p0, @LockMode='Exclusive',
                @LockOwner='Transaction', @LockTimeout=10000;
            IF @result < 0 THROW 51001, 'Authentication request is busy.', 1;
            """, key);
    }

    public async Task<OtpChallenge?> Latest(string phone) =>
        Map(await db.One("SELECT TOP(1) * FROM dbo.Customer_Otp_Challenges WHERE phone=@p0 AND NOT (" + RegistrationGrantStorage.Predicate + ") ORDER BY created_at DESC", phone));

    public async Task<int> CountRequests(string phone, long since) =>
        (int)(await db.One("SELECT COUNT(*) n FROM dbo.Customer_Otp_Challenges WHERE phone=@p0 AND NOT (" + RegistrationGrantStorage.Predicate + ") AND created_at>=@p1", phone, since))!.Num("n");

    public async Task Insert(OtpChallenge challenge) =>
        await db.Execute("""
            INSERT dbo.Customer_Otp_Challenges
                (id,phone,purpose,customer_id,created_at,expires)
            VALUES (@p0,@p1,@p2,@p3,@p4,@p5);
            DELETE dbo.Customer_Otp_Challenges WHERE phone=@p1 AND expires<@p6;
            """, challenge.Id, challenge.Phone, challenge.Purpose, challenge.CustomerId,
            challenge.CreatedAt, challenge.Expires, challenge.CreatedAt - 172800000L);

    public async Task<bool> Activate(string id, string codeHash, long now) =>
        await db.Execute("""
            UPDATE dbo.Customer_Otp_Challenges SET code_hash=@p1,delivery_status='sent'
            WHERE id=@p0 AND consumed_at IS NULL AND delivery_status='pending' AND expires>@p2
            """, id, codeHash, now) == 1;

    public async Task Fail(string id, long now) =>
        await db.Execute("""
            UPDATE dbo.Customer_Otp_Challenges SET delivery_status='failed',consumed_at=@p1,code_hash=NULL
            WHERE id=@p0
            """, id, now);

    public async Task<OtpChallenge?> Find(string id) =>
        Map(await db.One("SELECT * FROM dbo.Customer_Otp_Challenges WHERE id=@p0", id));

    public async Task<OtpChallenge?> LockChallenge(string id) =>
        Map(await db.One("SELECT * FROM dbo.Customer_Otp_Challenges WITH(UPDLOCK,HOLDLOCK) WHERE id=@p0", id));

    public async Task RecordFailure(string id, bool exhaust, long now) =>
        await db.Execute("""
            UPDATE dbo.Customer_Otp_Challenges SET attempts=attempts+1,
                consumed_at=CASE WHEN @p1=1 THEN @p2 ELSE consumed_at END,
                code_hash=CASE WHEN @p1=1 THEN NULL ELSE code_hash END
            WHERE id=@p0 AND consumed_at IS NULL AND attempts<5
            """, id, exhaust, now);

    public async Task Consume(string id, long now) =>
        await db.Execute("UPDATE dbo.Customer_Otp_Challenges SET consumed_at=@p1,code_hash=NULL WHERE id=@p0", id, now);

    public async Task Invalidate(string phone, long now) =>
        await db.Execute("""
            UPDATE dbo.Customer_Otp_Challenges SET consumed_at=@p1,code_hash=NULL
            WHERE phone=@p0 AND consumed_at IS NULL
            """, phone, now);

    // Use the deployed CHECK-compatible grant encoding; 'grant' is a logical state only.
    // The primary key stores only a purpose/mode-bound token digest.
    public async Task AddRegistrationGrant(string tokenHash, string phone, long expires, long now) =>
        await db.Execute("""
            INSERT dbo.Customer_Otp_Challenges(id,phone,purpose,created_at,expires,delivery_status,attempts)
            VALUES(@p0,@p1,'register',@p2,@p3,'pending',5)
            """, tokenHash, phone, now, expires);

    public async Task<RegistrationGrant?> FindRegistrationGrant(string tokenHash, bool locked = false)
    {
        var hint = locked ? " WITH(UPDLOCK,HOLDLOCK)" : "";
        var row = await db.One("SELECT * FROM dbo.Customer_Otp_Challenges" + hint +
            " WHERE id=@p0 AND " + RegistrationGrantStorage.Predicate, tokenHash);
        return row == null ? null : new(row.Str("id"), row.Str("phone"), row.Num("expires"),
            row.GetValueOrDefault("consumed_at") != null);
    }

    public async Task<bool> ConsumeRegistrationGrant(string tokenHash, long now) =>
        await db.Execute("UPDATE dbo.Customer_Otp_Challenges SET consumed_at=@p1 WHERE id=@p0 AND " +
            RegistrationGrantStorage.Predicate + " AND consumed_at IS NULL AND expires>@p1", tokenHash, now) == 1;

    public async Task AddReset(string tokenHash, string customerId, long expires, long now) =>
        await db.Execute("""
            INSERT dbo.Customer_Password_Resets(token_hash,customer_id,expires,created_at)
            VALUES(@p0,@p1,@p2,@p3);
            DELETE dbo.Customer_Password_Resets WHERE customer_id=@p1 AND expires<@p3;
            """, tokenHash, customerId, expires, now);

    public async Task<PasswordResetGrant?> FindReset(string tokenHash, bool locked = false)
    {
        var hint = locked ? " WITH(UPDLOCK,HOLDLOCK)" : "";
        var row = await db.One("""
            SELECT g.*,r.phone FROM dbo.Customer_Password_Resets g
            """ + hint + """
             JOIN dbo.Customer_Registrations r ON r.id=g.customer_id WHERE g.token_hash=@p0
            """, tokenHash);
        return row == null ? null : new(row.Str("token_hash"), row.Str("customer_id"),
            row.Str("phone"), row.Num("expires"), row.GetValueOrDefault("consumed_at") != null);
    }

    public async Task RevokeResets(string customerId, long now) =>
        await db.Execute("""
            UPDATE dbo.Customer_Password_Resets SET consumed_at=@p1
            WHERE customer_id=@p0 AND consumed_at IS NULL
            """, customerId, now);

    public async Task SavePending(PendingRegistration registration) => await db.Execute("""
        INSERT dbo.Pending_Customer_Registrations(challenge_id,first_name,last_name,password_hash)
        VALUES(@p0,@p1,@p2,@p3)
        """, registration.ChallengeId, registration.FirstName, registration.LastName, registration.PasswordHash);

    public async Task<PendingRegistration?> FindPending(string challengeId)
    {
        var row = await db.One("SELECT * FROM dbo.Pending_Customer_Registrations WHERE challenge_id=@p0", challengeId);
        return row == null ? null : new(row.Str("challenge_id"), row.Str("first_name"), row.Str("last_name"), row.Str("password_hash"));
    }
    public async Task RemovePending(string challengeId) =>
        await db.Execute("DELETE dbo.Pending_Customer_Registrations WHERE challenge_id=@p0", challengeId);

    private static OtpChallenge? Map(Dictionary<string, object?>? row) => row == null ? null : new(
        row.Str("id"), row.Str("phone"), row.Str("purpose"), row.GetValueOrDefault("customer_id") as string,
        row.GetValueOrDefault("code_hash") as string, row.Num("created_at"), row.Num("expires"),
        (int)row.Num("attempts"), row.GetValueOrDefault("consumed_at") != null,
        RegistrationGrantStorage.IsGrant(row) ? "grant" : row.Str("delivery_status"));
}
