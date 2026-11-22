using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChapGiti.Infrastructure.Persistence.Ef.Migrations;

/// <inheritdoc />
public partial class CatalogAndMagazine : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "content_format",
            table: "Articles",
            type: "varchar(24)",
            unicode: false,
            maxLength: 24,
            nullable: false,
            defaultValue: "markdown-v1");

        migrationBuilder.AddColumn<string>(
            name: "cover_alt",
            table: "Articles",
            type: "nvarchar(250)",
            maxLength: 250,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "cover_asset_id",
            table: "Articles",
            type: "varchar(36)",
            unicode: false,
            maxLength: 36,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "public_author_name",
            table: "Articles",
            type: "nvarchar(120)",
            maxLength: 120,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "publication_status",
            table: "Articles",
            type: "varchar(16)",
            unicode: false,
            maxLength: 16,
            nullable: false,
            defaultValue: "draft");

        migrationBuilder.AddColumn<DateTime>(
            name: "published_at",
            table: "Articles",
            type: "datetime2(3)",
            precision: 3,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "version",
            table: "Articles",
            type: "int",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.CreateTable(
            name: "Article_Slug_Aliases",
            columns: table => new
            {
                slug = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: false),
                article_id = table.Column<string>(type: "varchar(36)", unicode: false, maxLength: 36, nullable: false),
                created_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Article_Slug_Aliases", x => x.slug);
                table.ForeignKey(
                    name: "FK_ArticleAliases_Article",
                    column: x => x.article_id,
                    principalTable: "Articles",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "Print_Catalog_Options",
            columns: table => new
            {
                id = table.Column<string>(type: "varchar(36)", unicode: false, maxLength: 36, nullable: false),
                kind = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                code = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                label = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                pricing_rule = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                metadata = table.Column<string>(type: "nvarchar(max)", nullable: false),
                display_order = table.Column<int>(type: "int", nullable: false),
                active = table.Column<bool>(type: "bit", nullable: false),
                is_default = table.Column<bool>(type: "bit", nullable: false),
                version = table.Column<int>(type: "int", nullable: false),
                created_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                updated_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Print_Catalog_Options", x => x.id);
                table.CheckConstraint("CK_PrintCatalog_Default", "[is_default]=0 OR [active]=1");
                table.CheckConstraint("CK_PrintCatalog_Metadata", "ISJSON([metadata])=1");
                table.CheckConstraint("CK_PrintCatalog_Version", "[version]>=1 AND [display_order]>=0");
            });

        migrationBuilder.CreateIndex(
            name: "IX_Articles_CategoryStatus",
            table: "Articles",
            columns: new[] { "category", "publication_status" });

        migrationBuilder.CreateIndex(
            name: "IX_Articles_cover_asset_id",
            table: "Articles",
            column: "cover_asset_id");

        migrationBuilder.CreateIndex(
            name: "IX_Articles_PublicList",
            table: "Articles",
            columns: new[] { "publication_status", "published_at", "id" });

        migrationBuilder.Sql("UPDATE dbo.Articles SET publication_status=CASE WHEN published=1 THEN 'published' ELSE 'draft' END;");

        migrationBuilder.AddCheckConstraint(
            name: "CK_Article_Format",
            table: "Articles",
            sql: "[content_format]='markdown-v1'");

        migrationBuilder.AddCheckConstraint(
            name: "CK_Article_Status",
            table: "Articles",
            sql: "[publication_status] IN ('draft','published','archived') AND ([published]=1 AND [publication_status]='published' OR [published]=0 AND [publication_status]<>'published')");

        migrationBuilder.AddCheckConstraint(
            name: "CK_Article_Version",
            table: "Articles",
            sql: "[version]>=1");

        migrationBuilder.CreateIndex(
            name: "IX_ArticleAliases_Article",
            table: "Article_Slug_Aliases",
            column: "article_id");

        migrationBuilder.CreateIndex(
            name: "IX_PrintCatalog_List",
            table: "Print_Catalog_Options",
            columns: new[] { "kind", "active", "display_order", "code" });

        migrationBuilder.CreateIndex(
            name: "UX_PrintCatalog_Code",
            table: "Print_Catalog_Options",
            columns: new[] { "kind", "code" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "UX_PrintCatalog_Default",
            table: "Print_Catalog_Options",
            column: "kind",
            unique: true,
            filter: "[is_default]=1 AND [active]=1");

        migrationBuilder.AddForeignKey(
            name: "FK_Articles_CoverAsset",
            table: "Articles",
            column: "cover_asset_id",
            principalTable: "Banner_Assets",
            principalColumn: "id");
        migrationBuilder.Sql(InitialPrintCatalog.Sql);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Articles_CoverAsset",
            table: "Articles");

        migrationBuilder.DropTable(
            name: "Article_Slug_Aliases");

        migrationBuilder.DropTable(
            name: "Print_Catalog_Options");

        migrationBuilder.DropIndex(
            name: "IX_Articles_CategoryStatus",
            table: "Articles");

        migrationBuilder.DropIndex(
            name: "IX_Articles_cover_asset_id",
            table: "Articles");

        migrationBuilder.DropIndex(
            name: "IX_Articles_PublicList",
            table: "Articles");

        migrationBuilder.DropCheckConstraint(
            name: "CK_Article_Format",
            table: "Articles");

        migrationBuilder.DropCheckConstraint(
            name: "CK_Article_Status",
            table: "Articles");

        migrationBuilder.DropCheckConstraint(
            name: "CK_Article_Version",
            table: "Articles");

        migrationBuilder.DropColumn(
            name: "content_format",
            table: "Articles");

        migrationBuilder.DropColumn(
            name: "cover_alt",
            table: "Articles");

        migrationBuilder.DropColumn(
            name: "cover_asset_id",
            table: "Articles");

        migrationBuilder.DropColumn(
            name: "public_author_name",
            table: "Articles");

        migrationBuilder.DropColumn(
            name: "publication_status",
            table: "Articles");

        migrationBuilder.DropColumn(
            name: "published_at",
            table: "Articles");

        migrationBuilder.DropColumn(
            name: "version",
            table: "Articles");
    }
}
