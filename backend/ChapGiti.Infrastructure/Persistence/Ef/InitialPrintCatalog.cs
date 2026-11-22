namespace ChapGiti.Infrastructure.Persistence.Ef;

internal static class InitialPrintCatalog
{
    public static string Sql
    {
        get
        {
            using var stream=typeof(InitialPrintCatalog).Assembly.GetManifestResourceStream("ChapGiti.InitialPrintCatalog.json")
                ?? throw new InvalidOperationException("The reviewed initial catalog is missing.");
            using var reader=new StreamReader(stream);
            var json=reader.ReadToEnd().Replace("'","''");
            return """
                DECLARE @catalog nvarchar(max)=N'
                """+json+"""
                ';
                INSERT dbo.Print_Catalog_Options(id,kind,code,label,pricing_rule,metadata,display_order,active,is_default,version,created_at,updated_at)
                SELECT e.id,e.kind,e.code,e.label,e.pricingRule,e.metadata,e.displayOrder,e.active,e.isDefault,1,SYSUTCDATETIME(),SYSUTCDATETIME()
                FROM OPENJSON(@catalog) WITH(id varchar(36),kind varchar(40),code varchar(80),label nvarchar(160),pricingRule varchar(50),metadata nvarchar(max) AS JSON,displayOrder int,active bit,isDefault bit) e
                WHERE NOT EXISTS(SELECT 1 FROM dbo.Print_Catalog_Options o WHERE o.kind=e.kind AND o.code=e.code);
                """;
        }
    }
}
