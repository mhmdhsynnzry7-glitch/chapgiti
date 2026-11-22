using Microsoft.EntityFrameworkCore;

namespace ChapGiti.Infrastructure.Persistence.Ef;

public partial class ChapGitiDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        LegacySchemaChecks.Configure(modelBuilder);
        modelBuilder.Entity<Models.Order>().Property(x=>x.Version).IsConcurrencyToken();
        modelBuilder.Entity<Models.OrderQuote>().Property(x=>x.Version).IsConcurrencyToken();
        modelBuilder.Entity<Models.OrderCorrection>().Property(x=>x.Version).IsConcurrencyToken();
    }
}
