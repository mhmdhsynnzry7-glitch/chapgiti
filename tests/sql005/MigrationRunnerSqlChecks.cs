using ChapGiti.Application.Common;
using Microsoft.Data.SqlClient;

namespace ChapGiti.SqlAcceptanceTests;

internal static class MigrationRunnerSqlChecks
{
    public static async Task Run(IsolatedDatabase database,AcceptanceChecks checks)
    {
        await Task.WhenAll(database.Migrate(),database.Migrate());
        checks.Check((await database.Read("SELECT COUNT(*) n FROM dbo.Schema_Migrations"))!.Num("n")==9,"concurrent runner invocations retain exactly nine registered migrations");
        var history=await OrderWorkflowSqlScenarios.JsonSnapshot(database,"SELECT * FROM dbo.Schema_Migrations ORDER BY name FOR JSON PATH");
        var staged=Path.Combine(database.DirectoryPath,"Data","008_order_corrections.sql");
        // Change only an owned disposable copy, never the migration source or registered checksum.
        try
        {
            await File.AppendAllTextAsync(staged,"\n-- deliberate isolated checksum rejection probe\n");
            try{await database.Migrate();checks.Check(false,"checksum drift must be rejected");}
            catch(InvalidOperationException error)when(error.Message=="Applied SQL Server migration was modified: 008_order_corrections.sql")
            {checks.Check(true,"real runner refuses an altered staged migration without rewriting the stored checksum");}
        }
        finally{database.Stage("008_order_corrections.sql");}
        var failing=Path.Combine(database.DirectoryPath,"Data","010_acceptance_failure_probe.sql");
        try
        {
            await File.WriteAllTextAsync(failing,"CREATE TABLE dbo.Acceptance_RollbackProbe(id int NOT NULL); THROW 51082,'Isolated migration rollback probe',1;");
            try{await database.Migrate();checks.Check(false,"synthetic failing migration must roll back");}
            catch(InvalidOperationException error)when(error.Message.StartsWith("Legacy SQL ownership ends at 009.")){checks.Check(true,"legacy runner refuses new SQL ownership beyond 009 before executing the staged probe");}
            checks.Check((await database.Read("SELECT CASE WHEN OBJECT_ID('dbo.Acceptance_RollbackProbe') IS NULL THEN 1 ELSE 0 END absent"))!.Num("absent")==1,"migration DDL and registration roll back atomically on SQL failure");
        }
        finally{File.Delete(failing);}
        checks.Check(await OrderWorkflowSqlScenarios.JsonSnapshot(database,"SELECT * FROM dbo.Schema_Migrations ORDER BY name FOR JSON PATH")==history,"checksum rejection and failed synthetic migration preserve all nine original history rows");
        await database.Migrate();
    }
}
