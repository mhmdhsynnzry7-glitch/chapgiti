using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using ChapGiti.Infrastructure.Persistence.Ef;

namespace ChapGiti.Infrastructure.Persistence;

public static class ReleaseMetadata
{
    // GetMigrations reads the compiled assembly. No connection is configured or opened.
    public static object Read()
    {
        using var context = new ChapGitiDbContext(new DbContextOptionsBuilder<ChapGitiDbContext>()
            .UseSqlServer().Options);
        string[] names = ["Legacy009Baseline.sql", "InitialPrintCatalog.json"];
        var resources = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            using var stream = typeof(ReleaseMetadata).Assembly.GetManifestResourceStream("ChapGiti." + name)
                ?? throw new InvalidOperationException("Missing embedded migration resource: " + name);
            resources.Add(name, Convert.ToHexStringLower(SHA256.HashData(stream)));
        }
        return new { legacy = Migrations.LegacyNames, ef = context.Database.GetMigrations().ToArray(), resources };
    }
}
