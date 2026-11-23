using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChapGiti.Infrastructure.Persistence.Ef.Migrations
{
    /// <inheritdoc />
    public partial class ProposedCutTariffs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_proposed",
                table: "Cut_Tariff_Versions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                -- Immutable proposal policy v1. Existing cut histories are outside this seed.
                DECLARE @cuts TABLE (id varchar(36) PRIMARY KEY, width decimal(12,6), height decimal(12,6), factor decimal(12,6));
                INSERT @cuts
                SELECT o.id, d.width, d.height, d.factor
                FROM dbo.Print_Catalog_Options o
                CROSS APPLY OPENJSON(o.metadata) WITH (
                    width decimal(12,6), height decimal(12,6), factor decimal(12,6), isCustom bit, unit varchar(8)
                ) d
                WHERE o.kind = 'bookSizes' AND o.active = 1 AND d.isCustom = 0 AND d.unit = 'cm'
                  AND NOT EXISTS (SELECT 1 FROM dbo.Cut_Tariff_Versions t WITH (UPDLOCK,HOLDLOCK) WHERE t.cut_id = o.id);
                IF EXISTS (SELECT 1 FROM @cuts)
                BEGIN
                    IF EXISTS (SELECT 1 FROM @cuts WHERE width IS NULL OR height IS NULL OR factor IS NULL
                        OR width NOT BETWEEN 1 AND 100 OR height NOT BETWEEN 1 AND 100 OR factor NOT BETWEEN 0.1 AND 20)
                        THROW 51124, 'Invalid fixed-cut dimensions or factor for a proposed tariff.', 1;
                    DECLARE @stored nvarchar(max) = (SELECT [value] FROM dbo.Settings WHERE [key] = 'rates');
                    -- Archived Chap Giti reference rates supply only keys absent on fresh or partially configured installations.
                    DECLARE @reference nvarchar(max) = N'{"bw":190,"color":1150,"cream":250,"coated":490,"coverPaper":4000,"coverPrint":6000,"soft":18000,"hard":88000,"spiral":23000,"lamination":5000,"setup":120000}';
                    DECLARE @base TABLE (name nvarchar(32) PRIMARY KEY, amount decimal(20,8), json_type int);
                    INSERT @base
                    SELECT ref.[key], TRY_CONVERT(decimal(20,8), CASE WHEN saved.[key] IS NULL THEN ref.value ELSE saved.value END),
                           COALESCE(saved.type,ref.type)
                    FROM OPENJSON(@reference) ref
                    LEFT JOIN OPENJSON(@stored) saved ON saved.[key] = ref.[key];
                    IF EXISTS (SELECT 1 FROM @base WHERE json_type <> 2 OR amount IS NULL OR amount <> FLOOR(amount) OR amount NOT BETWEEN 0 AND 10000000)
                        THROW 51125, 'Invalid historical reference rate for a proposed tariff.', 1;
                    DECLARE @lines TABLE (id varchar(36), name nvarchar(32), amount bigint, PRIMARY KEY(id,name));
                    INSERT @lines
                    SELECT c.id, b.name, CONVERT(bigint,ROUND(CASE
                        WHEN b.name = 'setup' THEN b.amount
                        WHEN b.name IN ('bw','color','coverPrint') THEN b.amount * c.factor
                        WHEN b.name IN ('soft','hard','spiral') THEN b.amount * (21.5 + c.height) / 43
                        ELSE b.amount * c.width * c.height / 311.75 END,0))
                    FROM @cuts c CROSS JOIN @base b;
                    IF EXISTS (SELECT 1 FROM @lines WHERE amount NOT BETWEEN 0 AND 10000000)
                        THROW 51126, 'Proposed rate outside the supported range.', 1;
                    INSERT dbo.Cut_Tariff_Versions (cut_id,version,updated_at,rates,is_proposed)
                    SELECT id, 1, SYSUTCDATETIME(), '{' + STRING_AGG(CONCAT('"',name,'":',amount),',') WITHIN GROUP (ORDER BY name) + '}', 1
                    FROM @lines GROUP BY id;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dropping the flag would make unapproved proposals look like administrator-approved prices.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM dbo.Cut_Tariff_Versions WHERE is_proposed = 1)
                    THROW 51127, 'Cannot downgrade while proposed tariff history exists.', 1;
                """);
            migrationBuilder.DropColumn(
                name: "is_proposed",
                table: "Cut_Tariff_Versions");
        }
    }
}

