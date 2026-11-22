using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;

namespace ChapGiti.SqlAcceptanceTests;

internal static class BrowserAudit
{
    public static async Task Run(IsolatedDatabase database, AcceptanceHost host, AcceptanceChecks checks)
    {
        // The host checks the actual SqlStore server/catalog before every HTTP request.
        await database.Read("SELECT DB_NAME() name");
        var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
        var port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
        var frontend="http://127.0.0.1:"+port;
        host.AllowIsolatedFrontend(frontend);
        var start=new ProcessStartInfo("node") {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        start.ArgumentList.Add("tests/sql005/browser-journey.mjs");
        start.ArgumentList.Add(host.Origin);start.ArgumentList.Add(frontend);
        using var process=Process.Start(start)??throw new InvalidOperationException("Cannot start isolated SQL browser audit.");
        var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Directory.CreateDirectory(".qa/notifications");
        await File.WriteAllTextAsync(".qa/notifications/sql-browser-process.log",await stdout+Environment.NewLine+await stderr);
        if(process.ExitCode!=0)Console.WriteLine("SQL_BROWSER_FAILURE_LOG=.qa/notifications/sql-browser-process.log");
        checks.Check(process.ExitCode==0,"real Browser -> Next proxy -> API -> guarded disposable SQL journey passes");
        var result=JsonNode.Parse(File.ReadAllText(".qa/full-audit/sql-browser.json"))!;
        var orderId=result["orderId"]!.ToString();
        checks.Check((await database.Read("SELECT COUNT(*) n FROM dbo.Orders WHERE id=@p0 AND title=N'SQL browser audit book'",orderId))!["n"]!.ToString()=="1",
            "browser-created order is persisted in the exact isolated SQL catalog");
        checks.Check((await database.Read("SELECT COUNT(*) n FROM dbo.Order_Notes WHERE order_id=@p0 AND note=N'Synthetic browser internal note'",orderId))!["n"]!.ToString()=="1",
            "browser-created administrator note is persisted privately in SQL");
        // Delete only the synthetic PDF created by this browser; retain sanitized evidence.
        var fileId=result["fileId"]!.ToString();
        if(!Guid.TryParse(orderId,out _)||!Guid.TryParse(fileId,out _))throw new InvalidOperationException("Unexpected isolated file identity.");
        var file=Path.GetFullPath(Path.Combine(database.DirectoryPath,"unused-private-storage","uploads",orderId,fileId+".pdf"));
        if(!file.StartsWith(Path.GetFullPath(database.DirectoryPath)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Unsafe test file cleanup target.");
        File.Delete(file);
        foreach(var extra in result["workflowFiles"]?.AsArray() ?? []) {
            var extraOrder=extra!["orderId"]!.ToString();var extraFile=extra["fileId"]!.ToString();
            if(!Guid.TryParse(extraOrder,out _)||!Guid.TryParse(extraFile,out _))throw new InvalidOperationException("Invalid synthetic workflow file identity.");
            var target=Path.GetFullPath(Path.Combine(database.DirectoryPath,"unused-private-storage","uploads",extraOrder,extraFile+".pdf"));
            if(!target.StartsWith(Path.GetFullPath(database.DirectoryPath)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Unsafe synthetic workflow file target.");
            File.Delete(target);
        }
    }
}
