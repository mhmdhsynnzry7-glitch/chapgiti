using ChapGiti.Api.Http;
using ChapGiti.Application.Abstractions;
using ChapGiti.Application.Features;
using ChapGiti.Infrastructure;
using Microsoft.Data.SqlClient;

Console.InputEncoding = new System.Text.UTF8Encoding(false);
Console.OutputEncoding = new System.Text.UTF8Encoding(false);
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;

// Inspect only the packaged assembly. Do not construct a host or load a connection string.
if (args.Contains("--release-metadata"))
{
    if (args is not ["--release-metadata"]) { Environment.ExitCode = 2; return; }
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(ChapGiti.Infrastructure.Persistence.ReleaseMetadata.Read()));
    return;
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = [],
    ContentRootPath = AppContext.BaseDirectory
});
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 16 * 1024 * 1024);
if (args.Contains("--main-development"))
{
    if (!builder.Environment.IsDevelopment() ||
        !MainDevelopmentDatabase.TryPin(builder.Configuration.GetConnectionString("SqlServer"), out var pinned))
    {
        Console.Error.WriteLine("[DATABASE_CONFIG] Normal Development startup requires the existing local default SQL instance and database ChapGiti. Check .env.local and terminal overrides.");
        Environment.ExitCode = 1;
        return;
    }
    builder.Configuration["ConnectionStrings:SqlServer"] = pinned;
}
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping);
builder.Services.AddInfrastructure();
builder.Services.AddChapGitiApi();
var app = builder.Build();
if (args.Contains("--migrate") || args.Contains("--import-stdin") || args.Contains("--admin-setup") || args.Contains("--check"))
{
    try
    {
        if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("SqlServer")))
            throw new InvalidOperationException("[DATABASE_CONFIG] Configure ConnectionStrings__SqlServer for the intended database.");
        await using var scope = app.Services.CreateAsyncScope();
        var maintenance = scope.ServiceProvider.GetRequiredService<IDatabaseMaintenance>();
        if (args.Contains("--migrate") || args.Contains("--import-stdin"))
            await maintenance.Migrate();
        if (args.Contains("--import-stdin"))
        {
            await maintenance.Import(await Console.In.ReadToEndAsync());
            Console.WriteLine("SQLite data imported into SQL Server. Existing passwords and file references preserved; old sessions were not imported.");
        }
        else if (args.Contains("--admin-setup"))
        {
            var index = Array.IndexOf(args, "--admin-setup");
            if (args.Length < index + 3)
                throw new InvalidOperationException("Usage: --admin-setup email username (password on stdin).");
            var password = await Console.In.ReadLineAsync() ?? "";
            await scope.ServiceProvider.GetRequiredService<AccountService>().SetupAdmin(args[index + 1], args[index + 2], password);
            Console.WriteLine("Admin account updated; existing sessions revoked.");
        }
        else if (args.Contains("--check"))
        {
            await maintenance.VerifyStartup();
            await maintenance.VerifyAdministrator();
            Console.WriteLine("SQL Server connection, schema and administrator verified.");
        }
        else
            Console.WriteLine("SQL Server migrations applied.");
        return;
    }
    catch (Exception error)
    {
        Console.Error.WriteLine(error switch
        {
            SqlException sql when sql.Number is -2 or -1 or 2 or 26 or 40 or 53 or 258 or 4060 or 18456 or 10060 or 10061 =>
                $"[DATABASE_CONNECTION] SQL Server error {sql.Number}. Check the configured instance, catalog, credentials and connectivity.",
            SqlException sql => $"SQL Server operation failed ({sql.Number}): {sql.Message}",
            ArgumentException when args.Contains("--check") =>
                "[DATABASE_CONFIG] Invalid SQL connection configuration. Check the connection string keywords and values.",
            _ => error.Message
        });
        Environment.ExitCode = 1;
        return;
    }
}

if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("SqlServer")))
    throw new InvalidOperationException("Configure ConnectionStrings__SqlServer before starting the backend.");
if (!Uri.TryCreate(builder.Configuration["Site:Url"], UriKind.Absolute, out var site) ||
    site.AbsolutePath != "/" || !string.IsNullOrEmpty(site.Query) ||
    !string.IsNullOrEmpty(site.Fragment) || !string.IsNullOrEmpty(site.UserInfo) ||
    site.Scheme is not ("http" or "https"))
    throw new InvalidOperationException("Site:Url must be an HTTP(S) origin.");

// Read-only readiness validation. Applying migrations is an explicit maintenance operation.
await using (var readiness = app.Services.CreateAsyncScope())
    await readiness.ServiceProvider.GetRequiredService<IDatabaseMaintenance>().VerifyStartup();
app.MapChapGitiApi();
await app.RunAsync();
