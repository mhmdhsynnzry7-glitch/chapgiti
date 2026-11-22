using System.Diagnostics;
using System.Text.RegularExpressions;
using ChapGiti.Application.Common;
using ChapGiti.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace ChapGiti.SqlAcceptanceTests;

// Explicit opt-in acceptance fixture, never referenced by the application or ordinary tests.
// No caller-provided connection strings or attach paths. The optional 2025 target
// is a fixed local QA instance whose edition and empty inventory are verified.
internal sealed class IsolatedDatabase : IAsyncDisposable
{
    public string Name { get; } = "ChapGiti_Acceptance005_" + Guid.NewGuid().ToString("N");
    public string Instance { get; }
    private readonly bool express2025;
    private string Source => express2025 ? @"lpc:.\ChapGitiQA2025" : @"(localdb)\"+Instance;
    // Only the local, dedicated, otherwise empty QA instance is eligible for this opt-in.
    private string EngineGuard => express2025 ? """
        CONVERT(int,SERVERPROPERTY('ProductMajorVersion'))=17 AND SERVERPROPERTY('EngineEdition')=4
        AND CONVERT(sysname,SERVERPROPERTY('InstanceName'))='ChapGitiQA2025' AND CONNECTIONPROPERTY('net_transport')='Shared memory'
        AND (SELECT compatibility_level FROM sys.databases WHERE name='model')=170
        """ : "SERVERPROPERTY('IsLocalDB')=1";
    public string DirectoryPath { get; }
    public string Server { get; private set; } = "";
    public bool Created { get; private set; }
    public bool Removed { get; private set; }
    private bool instanceCreated;
    private Guid? ownedBroker;
    public IsolatedDatabase(bool express2025 = false)
    {
        this.express2025 = express2025;
        Instance = express2025 ? "ChapGitiQA2025" : "ChapGiti005_" + Guid.NewGuid().ToString("N");
        var root=Path.GetFullPath(".qa");
        DirectoryPath=Path.GetFullPath(Path.Combine(root,Name));
        if(!DirectoryPath.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Unsafe artifact path.");
        Directory.CreateDirectory(Path.Combine(DirectoryPath,"Data"));
    }
    public string ConnectionString(string? catalog=null) => new SqlConnectionStringBuilder {
        DataSource=Source, InitialCatalog=catalog??Name, IntegratedSecurity=true,
        Encrypt=false, Pooling=false, ConnectTimeout=15, ApplicationName="ChapGiti.Isolated005Acceptance"
    }.ConnectionString;
    public IConfiguration Configuration => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
        ["ConnectionStrings:SqlServer"]=ConnectionString()
    }).Build();
    public SqlStore Store() => new(Configuration);

    public async Task Create()
    {
        if(!Regex.IsMatch(Name,"^ChapGiti_Acceptance005_[a-f0-9]{32}$"))throw new InvalidOperationException("Invalid test identity.");
        if(!express2025) {
            await LocalDb("create",Instance,"15.0");instanceCreated=true;
            await LocalDb("start",Instance);
        }
        await using var master=new SqlConnection(ConnectionString("master"));await master.OpenAsync();
        using(var command=master.CreateCommand()) {
            command.CommandText=$"SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) WHERE {EngineGuard} AND DB_NAME()='master' AND HAS_PERMS_BY_NAME(NULL,NULL,'VIEW ANY DATABASE')=1 AND NOT EXISTS(SELECT 1 FROM sys.databases WHERE database_id>4)";
            Server=Convert.ToString(await command.ExecuteScalarAsync())??"";
            if(Server=="")throw new InvalidOperationException("Dedicated instance identity, edition, compatibility or empty inventory could not be verified.");
        }
        await Guard(master,"master");
        using(var create=master.CreateCommand()) {
            string FilePath(string file)=>Path.Combine(DirectoryPath,file).Replace("'","''");
            create.CommandText=express2025 ? $"CREATE DATABASE [{Name}]" : $"CREATE DATABASE [{Name}] ON PRIMARY (NAME=N'{Name}',FILENAME=N'{FilePath("test.mdf")}') LOG ON (NAME=N'{Name}_log',FILENAME=N'{FilePath("test.ldf")}')";
            await create.ExecuteNonQueryAsync();Created=true;
        }
        using(var identify=master.CreateCommand()) {
            identify.CommandText="SELECT service_broker_guid FROM sys.databases WHERE name=@name";
            identify.Parameters.AddWithValue("@name",Name);
            ownedBroker=(Guid?)await identify.ExecuteScalarAsync() ?? throw new InvalidOperationException("Missing disposable catalog identity.");
        }
        await using var db=Store();await Guard(db);
        Console.WriteLine($"ISOLATED_INSTANCE={Instance} DATABASE={Name} BROKER_GUID={ownedBroker}");
    }
    public async Task Guard(SqlStore db) => await Guard(await db.Connection(),Name);
    private async Task Guard(SqlConnection connection,string catalog)
    {
        if(connection.DataSource!=Source || (catalog!=Name&&catalog!="master"))throw new InvalidOperationException("Unexpected SQL target.");
        using var query=connection.CreateCommand();
        query.CommandText=$"SELECT CASE WHEN DB_NAME()=@db AND CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))=@server AND {EngineGuard} AND (@db='master' OR @express=0 OR DATABASEPROPERTYEX(DB_NAME(),'Status')='ONLINE' AND (SELECT compatibility_level FROM sys.databases WHERE name=DB_NAME())=170) AND NOT EXISTS(SELECT 1 FROM sys.databases WHERE database_id>4 AND name<>@owned) THEN 1 ELSE 0 END";
        if (Created) {
            if (ownedBroker is not Guid broker) throw new InvalidOperationException("Unverified disposable catalog identity.");
            query.CommandText="IF NOT EXISTS(SELECT 1 FROM sys.databases WHERE name=@owned AND service_broker_guid=@broker) THROW 51000,'Disposable catalog identity changed',1; " + query.CommandText;
            query.Parameters.AddWithValue("@broker",broker);
        }
        query.Parameters.AddWithValue("@owned",Name);query.Parameters.AddWithValue("@express",express2025);
        query.Parameters.AddWithValue("@db",catalog);query.Parameters.AddWithValue("@server",Server);
        if(Convert.ToInt32(await query.ExecuteScalarAsync())!=1)throw new InvalidOperationException("SQL database/server identity mismatch; write refused.");
    }
    public async Task<int> Write(string sql,params object?[] values)
    {
        await using var db=Store();await Guard(db);return await db.Execute(sql,values);
    }
    public async Task<Dictionary<string,object?>?> Read(string sql,params object?[] values)
    {
        await using var db=Store();await Guard(db);return await db.One(sql,values);
    }
    public async Task Migrate()
    {
        await using var db=Store();await Guard(db);
        // The runner uses this same pinned connection for its transaction and commands; no USE statement.
        await Migrations.Apply(db,DirectoryPath);
    }
    public void Stage(string file) => File.Copy(Path.Combine("backend/ChapGiti.Infrastructure/Persistence/Migrations",file),Path.Combine(DirectoryPath,"Data",file),true);
    public async ValueTask DisposeAsync()
    {
        if(Created) {
            await using var master=new SqlConnection(ConnectionString("master"));await master.OpenAsync();await Guard(master,"master");
            using var drop=master.CreateCommand();drop.CommandText=$"DROP DATABASE [{Name}]";await drop.ExecuteNonQueryAsync();
            using var verify=master.CreateCommand();verify.CommandText="SELECT COUNT(*) FROM sys.databases WHERE name=@name";verify.Parameters.AddWithValue("@name",Name);
            Removed=Convert.ToInt32(await verify.ExecuteScalarAsync())==0;
            if(!Removed)throw new InvalidOperationException("Disposable database cleanup unconfirmed.");
            Console.WriteLine("PASS disposable database removed after server/name revalidation");
        }
        if(instanceCreated){await LocalDb("stop",Instance);await LocalDb("delete",Instance);instanceCreated=false;Console.WriteLine("PASS dedicated LocalDB instance removed");}
    }
    private static async Task LocalDb(params string[] args)
    {
        var start=new ProcessStartInfo("SqlLocalDB.exe"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in args)start.ArgumentList.Add(arg);
        using var process=Process.Start(start)??throw new InvalidOperationException("LocalDB tool unavailable.");
        var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();await Task.WhenAll(output,error);
        if(process.ExitCode!=0)throw new InvalidOperationException("LocalDB "+args[0]+" failed (exit "+process.ExitCode+").");
    }
}
