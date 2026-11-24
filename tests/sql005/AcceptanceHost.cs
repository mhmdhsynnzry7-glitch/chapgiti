using System.Net.Http.Json;
using System.Net;
using System.Text.Json.Nodes;
using ChapGiti.Api.Http;
using ChapGiti.Application.Abstractions;
using ChapGiti.Infrastructure;
using ChapGiti.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ChapGiti.SqlAcceptanceTests;

internal sealed class AcceptanceHost(WebApplication app) : IAsyncDisposable
{
    public string Origin => app.Urls.Single();
    public void AllowIsolatedFrontend(string origin) {
        if(!new Uri(origin).IsLoopback)throw new InvalidOperationException("Only a loopback test frontend is permitted.");
        app.Configuration["Site:Url"]=origin;
    }
    public IServiceProvider Services=>app.Services;
    public HttpClient Client(CookieContainer? cookies=null) {
        var client=new HttpClient(new HttpClientHandler { UseCookies=true,CookieContainer=cookies??new() }) { BaseAddress=new Uri(Origin) };
        client.DefaultRequestHeaders.Add("Origin",Origin);return client;
    }
    public static async Task<AcceptanceHost> Start(IsolatedDatabase database)
    {
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{EnvironmentName="Development",ContentRootPath=AppContext.BaseDirectory,Args=[]});
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?> {
            ["ConnectionStrings:SqlServer"]=database.ConnectionString(),["Site:Url"]="http://127.0.0.1",
            ["OTP_VERIFICATION_ENABLED"]="true",["Otp:Enabled"]="true",["Sms:Enabled"]="false",
            ["Otp:ApiKey"]="isolated-sql-fixture-only-not-a-real-provider-key",
            ["Storage:Root"]=Path.Combine(database.DirectoryPath,"unused-private-storage")
        });
        builder.Logging.ClearProviders();builder.Logging.AddProvider(new SafeErrors());builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddInfrastructure();builder.Services.AddChapGitiApi();
        builder.Services.AddSingleton<ISmsNotifier, NoSms>();builder.Services.AddSingleton<IOtpSender, NoOtp>();
        var app=builder.Build();
        // Check the very same scoped SqlStore connection used by services before each API operation.
        app.Use(async (context,next)=>{await database.Guard(context.RequestServices.GetRequiredService<SqlStore>());await next(context);});
        app.MapChapGitiApi();await app.StartAsync();builder.Configuration["Site:Url"]=app.Urls.Single();
        return new(app);
    }
    public async ValueTask DisposeAsync(){await app.StopAsync();await app.DisposeAsync();}
    private sealed class NoSms : ISmsNotifier {
        public SmsAvailability Availability=>new(false,false,false);
        public Task Notify(string orderId,string code,string phone,string status,string eventId)=>Task.CompletedTask;
    }
    private sealed class NoOtp : IOtpSender {
        public bool Available=>true;public int CodeLength=>4;
        // Fixed fake transport response; never constructs a provider HTTP client.
        public Task<string> Send(string phone)=>Task.FromResult("0123");
    }
    private sealed class SafeErrors : ILoggerProvider {
        public ILogger CreateLogger(string categoryName)=>new ErrorLogger();public void Dispose(){}
        private sealed class ErrorLogger : ILogger {
            public IDisposable? BeginScope<TState>(TState state) where TState:notnull=>null;
            public bool IsEnabled(LogLevel level)=>level>=LogLevel.Error;
            public void Log<TState>(LogLevel level,EventId id,TState state,Exception? error,Func<TState,Exception?,string> formatter) {
                if(level>=LogLevel.Error&&error!=null)Console.WriteLine("TEST_API_EXCEPTION="+error.GetType().Name+(error is Microsoft.Data.SqlClient.SqlException sql?" SQL_NUMBER="+sql.Number:""));
                if(error is Microsoft.Data.SqlClient.SqlException { Number:1205 })
                {
                    var frames=new System.Diagnostics.StackTrace(error).GetFrames();
                    var trace=frames.Select(frame=>frame.GetMethod()?.DeclaringType?.FullName)
                        .Where(name=>name?.StartsWith("ChapGiti.",StringComparison.Ordinal)==true);
                    Console.WriteLine("SQL_DEADLOCK_CALLS="+string.Join(" -> ",trace));
                }
            }
        }
    }
}

internal sealed class AcceptanceChecks
{
    public List<string> Passed { get; }=[];
    public void Check(bool condition,string label) {
        if(!condition)throw new InvalidOperationException(label);
        lock(Passed){Passed.Add(label);Console.WriteLine("PASS "+label);}
    }
    public async Task<JsonNode> Request(HttpClient client,string method,string path,object? body=null,int expected=200) {
        using var request=new HttpRequestMessage(new HttpMethod(method),"/api/"+path);
        if(body!=null)request.Content=JsonContent.Create(body);
        using var response=await client.SendAsync(request);var result=await response.Content.ReadAsStringAsync();
        Check((int)response.StatusCode==expected,"SQL API "+method+" "+path+" -> "+expected+" (actual "+(int)response.StatusCode+")");
        return JsonNode.Parse(result)!;
    }
}
