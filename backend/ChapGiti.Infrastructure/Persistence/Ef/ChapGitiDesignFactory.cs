using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ChapGiti.Infrastructure.Persistence.Ef;

// Model/scaffolding never loads .env.local or opens the user's configured database.
// Applying schema remains an explicit application maintenance operation with validated target.
public sealed class ChapGitiDesignFactory : IDesignTimeDbContextFactory<ChapGitiDbContext>
{
    public ChapGitiDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<ChapGitiDbContext>()
        .UseSqlServer("Server=127.0.0.1,1;Database=ChapGiti_DesignOnly;Integrated Security=True;Encrypt=True;Connect Timeout=1",sql=>sql.UseCompatibilityLevel(150))
        .Options);
}
