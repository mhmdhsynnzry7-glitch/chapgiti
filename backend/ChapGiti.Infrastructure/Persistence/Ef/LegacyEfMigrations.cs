using Microsoft.EntityFrameworkCore;

namespace ChapGiti.Infrastructure.Persistence.Ef;

internal static class LegacyEfMigrations
{
    public static string ValidationSql
    {
        get
        {
            using var stream = typeof(LegacyEfMigrations).Assembly.GetManifestResourceStream("ChapGiti.Legacy009Baseline.sql")
                ?? throw new InvalidOperationException("The reviewed legacy schema contract is missing.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }

    // Invoked only by the explicit maintenance command, never application startup.
    public static async Task Apply(SqlStore db)
    {
        await db.Connection();
        await db.Execute("DECLARE @lock int; EXEC @lock=sp_getapplock @Resource='ChapGiti.EF.Schema',@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=30000; IF @lock<0 THROW 51107,'EF schema migration is busy.',1;");
        try
        {
            await db.Execute(ValidationSql);
            await db.Context.Database.MigrateAsync();
        }
        finally { await db.Execute("EXEC sp_releaseapplock @Resource='ChapGiti.EF.Schema',@LockOwner='Session';"); }
    }
}
