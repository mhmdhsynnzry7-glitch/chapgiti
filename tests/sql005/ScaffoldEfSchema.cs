using System.Diagnostics;
using ChapGiti.Application.Common;

namespace ChapGiti.SqlAcceptanceTests;

internal static class ScaffoldEfSchema
{
    public static async Task<int> Run(bool metadataOnly = false)
    {
        var database=new IsolatedDatabase();
        try
        {
            await database.Create();
            foreach(var file in Directory.GetFiles("backend/ChapGiti.Infrastructure/Persistence/Migrations","*.sql").Order())database.Stage(Path.GetFileName(file));
            await database.Migrate();
            if (metadataOnly)
            {
                var contract = await database.Read("""
                    SELECT (
                    SELECT JSON_QUERY((SELECT t.name tableName,i.name indexName,i.is_primary_key primaryKey,i.is_unique uniqueIndex,i.is_unique_constraint uniqueConstraint,i.filter_definition filterDefinition,
                        JSON_QUERY((SELECT c.name columnName,ic.is_descending_key descendingKey,ic.is_included_column includedColumn FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id ORDER BY ic.index_column_id FOR JSON PATH)) columns
                        FROM sys.tables t JOIN sys.indexes i ON i.object_id=t.object_id WHERE i.index_id>0 AND SCHEMA_NAME(t.schema_id)='dbo' ORDER BY t.name,i.index_id FOR JSON PATH)) indexes,
                    JSON_QUERY((SELECT t.name tableName,f.name foreignKey,f.delete_referential_action deleteAction,f.update_referential_action updateAction,rt.name referencedTable,
                        JSON_QUERY((SELECT c.name columnName,rc.name referencedColumn FROM sys.foreign_key_columns fc JOIN sys.columns c ON c.object_id=fc.parent_object_id AND c.column_id=fc.parent_column_id JOIN sys.columns rc ON rc.object_id=fc.referenced_object_id AND rc.column_id=fc.referenced_column_id WHERE fc.constraint_object_id=f.object_id ORDER BY fc.constraint_column_id FOR JSON PATH)) columns
                        FROM sys.foreign_keys f JOIN sys.tables t ON t.object_id=f.parent_object_id JOIN sys.tables rt ON rt.object_id=f.referenced_object_id ORDER BY t.name,f.name FOR JSON PATH)) foreignKeys,
                    JSON_QUERY((SELECT t.name tableName,c.name constraintName,c.definition FROM sys.check_constraints c JOIN sys.tables t ON t.object_id=c.parent_object_id ORDER BY t.name,c.name FOR JSON PATH)) checks,
                    JSON_QUERY((SELECT t.name tableName,c.name columnName,d.definition FROM sys.default_constraints d JOIN sys.tables t ON t.object_id=d.parent_object_id JOIN sys.columns c ON c.object_id=d.parent_object_id AND c.column_id=d.parent_column_id ORDER BY t.name,c.name FOR JSON PATH)) defaults
                    FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) [value]
                    """);
                await File.WriteAllTextAsync("backend/ChapGiti.Infrastructure/Persistence/Ef/Legacy009Constraints.json",contract!.Str("value"));
                Console.WriteLine("PASS legacy key/index/FK/check/default contract exported from a pinned disposable SQL009 database.");
                return 0;
            }
            var columns=await database.Read("""
                SELECT (SELECT t.name tableName,c.name columnName,TYPE_NAME(c.user_type_id) sqlType,c.max_length maxLength,c.precision,c.scale,c.is_nullable nullable,c.is_identity identityColumn,c.is_computed computedColumn
                FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id WHERE SCHEMA_NAME(t.schema_id)='dbo'
                ORDER BY t.name,c.column_id FOR JSON PATH) [value]
                """);
            Directory.CreateDirectory("backend/ChapGiti.Infrastructure/Persistence/Ef");
            await File.WriteAllTextAsync("backend/ChapGiti.Infrastructure/Persistence/Ef/Legacy009Schema.json",columns!.Str("value"));
            await using(var store=database.Store())await database.Guard(store);
            var start=new ProcessStartInfo(Path.GetFullPath(".qa/onion-ef-2026-11-17/ef-tools/dotnet-ef.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(var key in start.Environment.Keys.ToArray())if(key.StartsWith("ConnectionStrings",StringComparison.OrdinalIgnoreCase)||key.StartsWith("SQLSERVER",StringComparison.OrdinalIgnoreCase)||key.StartsWith("MELIPAYAMAK",StringComparison.OrdinalIgnoreCase))start.Environment.Remove(key);
            foreach(var arg in new[]{"dbcontext","scaffold",database.ConnectionString(),"Microsoft.EntityFrameworkCore.SqlServer","--project","backend/ChapGiti.Infrastructure","--context","ChapGitiDbContext","--context-dir","Persistence/Ef","--output-dir","Persistence/Ef/Models","--namespace","ChapGiti.Infrastructure.Persistence.Ef.Models","--context-namespace","ChapGiti.Infrastructure.Persistence.Ef","--no-onconfiguring","--schema","dbo"})start.ArgumentList.Add(arg);
            using var child=Process.Start(start)!;var output=child.StandardOutput.ReadToEndAsync();var errors=child.StandardError.ReadToEndAsync();await child.WaitForExitAsync();
            await File.WriteAllTextAsync(".qa/onion-ef-2026-11-17/scaffold-details.log",await output+await errors);
            if(child.ExitCode!=0)throw new InvalidOperationException("EF scaffold failed; inspect the isolated metadata log.");
            Console.WriteLine("PASS EF models were reverse-engineered exclusively from the verified disposable SQL001–009 schema.");return 0;
        }
        finally{await database.DisposeAsync();}
    }
}
