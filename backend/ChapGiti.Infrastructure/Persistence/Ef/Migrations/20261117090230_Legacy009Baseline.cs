using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ChapGiti.Infrastructure.Persistence.Ef.Migrations;

/// <summary>Adopts the already validated legacy schema without recreating existing tables.</summary>
public partial class Legacy009Baseline : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(LegacyEfMigrations.ValidationSql);
    protected override void Down(MigrationBuilder migrationBuilder) => throw new NotSupportedException("The legacy adoption baseline cannot be reverted. Restore a reviewed database backup instead.");
}
