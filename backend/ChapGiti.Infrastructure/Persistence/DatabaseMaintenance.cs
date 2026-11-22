using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace ChapGiti.Infrastructure.Persistence;

internal sealed class DatabaseMaintenance(SqlStore db, IHostEnvironment environment) : IDatabaseMaintenance
{
    public async Task Migrate()
    {
        Migrations.VerifyInventory(environment.ContentRootPath);
        await Migrations.Apply(db, environment.ContentRootPath);
        await Ef.LegacyEfMigrations.Apply(db);
    }
    public Task Import(string snapshot) => Importer.Apply(db, snapshot);

    public async Task VerifyAdministrator()
    {
        var count = await db.One("SELECT (SELECT COUNT(*) FROM dbo.Admin_Users)+(SELECT COUNT(*) FROM dbo.Customer_Roles WHERE role='admin') n");
        if (count!.Num("n") == 0)
            throw new InvalidOperationException("No administrator is configured.");
    }

    public async Task VerifyStartup()
    {
        Migrations.VerifyInventory(environment.ContentRootPath);
        var legacy = await ReadMigrationHistory();
        foreach (var row in legacy)
        {
            var name = row.Str("name");
            var sql = await File.ReadAllTextAsync(Path.Combine(environment.ContentRootPath, "Data", name));
            if (Migrations.Checksum(sql) != row.Str("checksum"))
                throw new InvalidOperationException("[MIGRATION_HISTORY] Legacy checksum differs for " + name + ". Inspect the database history and matching build.");
        }
        await db.Execute(Ef.LegacyEfMigrations.ValidationSql);
    }

    public Task Health() => ReadMigrationHistory();

    private async Task<List<Dictionary<string, object?>>> ReadMigrationHistory()
    {
        try { await db.Connection(); }
        catch (SqlException error)
        {
            throw new InvalidOperationException($"[DATABASE_CONNECTION] Could not open the configured catalog (SQL {error.Number}). Verify the instance, catalog and access.", error);
        }
        var legacy = await db.Query("IF OBJECT_ID('dbo.Schema_Migrations','U') IS NOT NULL SELECT name,checksum FROM dbo.Schema_Migrations ORDER BY name");
        var applied = legacy.Select(row => row.Str("name")).ToArray();
        RequireHistory("legacy SQL", Migrations.LegacyNames, applied);
        var ef = await db.Query("IF OBJECT_ID('dbo.__EFMigrationsHistory','U') IS NOT NULL SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId");
        RequireHistory("EF", db.Context.Database.GetMigrations().ToArray(), ef.Select(row => row.Str("MigrationId")).ToArray());
        return legacy;
    }

    private static void RequireHistory(string owner, string[] expected, string[] applied)
    {
        if (expected.Length == 0 || !applied.SequenceEqual(expected.Take(applied.Length), StringComparer.Ordinal))
            throw new InvalidOperationException($"[MIGRATION_HISTORY] Unexpected {owner} history. Verify the target, migration order and backend version before maintenance.");
        if (applied.Length < expected.Length)
            throw new InvalidOperationException($"[MIGRATION_REQUIRED] Pending {owner}: {string.Join(", ", expected.Skip(applied.Length))}. Verify the target, then use the explicit migration workflow.");
    }
}
