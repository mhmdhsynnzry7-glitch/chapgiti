using Microsoft.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
namespace ChapGiti.Infrastructure.Persistence;

internal static class Migrations
{
    internal static readonly string[] LegacyNames = [
        "001_initial.sql", "002_customer_otp.sql", "003_unified_authentication.sql",
        "004_administrator_login_email.sql", "005_order_support.sql", "006_order_quotes.sql",
        "007_in_app_notifications.sql", "008_order_corrections.sql", "009_order_views.sql"
    ];

    internal static string Checksum(string sql) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sql.Replace("\r\n", "\n"))));

    internal static void VerifyInventory(string root)
    {
        var files = Directory.GetFiles(Path.Combine(root, "Data"), "*.sql").Select(Path.GetFileName).Order(StringComparer.Ordinal);
        if (!files.SequenceEqual(LegacyNames))
            throw new InvalidOperationException("[MIGRATION_FILES] The backend Data directory must contain exactly the reviewed SQL001-009 files. Rebuild from matching source.");
    }

    public static async Task Apply(SqlStore db, string contentRoot)
    {
        var files=Directory.GetFiles(Path.Combine(contentRoot,"Data"),"*.sql").OrderBy(x=>x).ToArray();
        if(files.Any(file=>!LegacyNames.Contains(Path.GetFileName(file),StringComparer.Ordinal)))
            throw new InvalidOperationException("Legacy SQL ownership ends at 009. New schema changes must use reviewed EF migrations.");
        var builder = new SqlConnectionStringBuilder(db.ConnectionString);
        var name = builder.InitialCatalog;
        if (!Regex.IsMatch(name, @"^[A-Za-z][A-Za-z0-9_]{0,63}$"))
            throw new InvalidOperationException("Use a dedicated SQL Server database name (letters, digits and underscore).");
        if (name.StartsWith("ChapGiti_Test_", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Retired test databases are not supported. Use the existing ChapGiti database.");
        if (!string.IsNullOrWhiteSpace(builder.AttachDBFilename) || builder.UserInstance)
            throw new InvalidOperationException("File attachment and automatic database provisioning are disabled.");
        try
        {
            // Connect directly to the configured catalog. Migrations only manage its schema.
            await db.Connection();
        }
        catch (SqlException error) when (error.Number is 4060 or 911)
        {
            throw new InvalidOperationException(
                $"Database '{name}' must already exist. Automatic database creation is disabled.", error);
        }
        await db.Execute("DECLARE @result int; EXEC @result=sp_getapplock @Resource='ChapGiti.Schema',@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=30000; IF @result<0 THROW 51107,'Legacy schema migration is busy.',1;");
        try
        {
            await db.Execute("IF OBJECT_ID('dbo.Schema_Migrations') IS NULL CREATE TABLE dbo.Schema_Migrations(name varchar(150) PRIMARY KEY,checksum char(64) NOT NULL,applied_at datetime2(3) NOT NULL)");
            foreach (var file in files)
            {
                var text = await File.ReadAllTextAsync(file);
                var hash = Checksum(text);
                var fileName = Path.GetFileName(file);
                var old = await db.One("SELECT checksum FROM dbo.Schema_Migrations WHERE name=@p0", fileName);
                if (old != null)
                {
                    if (old.Str("checksum") != hash)
                        throw new InvalidOperationException("Applied SQL Server migration was modified: " + fileName);
                    continue;
                }
                await db.Transaction(async () => { await db.Execute(text); await db.Execute("INSERT dbo.Schema_Migrations VALUES(@p0,@p1,@p2)", fileName, hash, Input.Now()); return true; });
            }
        }
        finally { await db.Execute("EXEC sp_releaseapplock @Resource='ChapGiti.Schema',@LockOwner='Session';"); }
    }
}
