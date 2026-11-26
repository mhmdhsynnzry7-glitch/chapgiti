using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ChapGiti.SqlAcceptanceTests;

internal static class PackagedReleaseAcceptance
{
    public static async Task<int> Run(string extracted)
    {
        var root = Path.GetFullPath(extracted);
        var qa = Path.GetFullPath(".qa") + Path.DirectorySeparatorChar;
        if (!root.StartsWith(qa, StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(Path.Combine(root, "RELEASE-MANIFEST.json")))
            throw new ArgumentException("Provide an extracted release under the local .qa directory.");
        var backend = Path.Combine(root, "backend", "publish", "ChapGiti.Api.exe");
        var browser = Path.GetFullPath("tests/sql005/packaged-release-smoke.mjs");
        await using var database = new IsolatedDatabase();
        await database.Create();
        using var tlsKey=RSA.Create(2048);
        var request=new CertificateRequest("CN=localhost",tlsKey,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        var names=new SubjectAlternativeNameBuilder();
        names.AddIpAddress(IPAddress.Loopback);names.AddDnsName("localhost");
        request.CertificateExtensions.Add(names.Build());
        using var certificate=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5),DateTimeOffset.UtcNow.AddHours(1));
        var certificatePath=Path.Combine(database.DirectoryPath,"release-loopback-cert.pem");
        await File.WriteAllTextAsync(certificatePath,certificate.ExportCertificatePem());

        async Task Execute(string command, params string[] arguments)
        {
            await using (var store = database.Store()) await database.Guard(store);
            var start = new ProcessStartInfo(command) { UseShellExecute=false, CreateNoWindow=true, WorkingDirectory=root };
            foreach (var name in start.Environment.Keys.ToArray())
                if (new[]{"SQLSERVER","ConnectionStrings","CHAP_","MELIPAYAMAK","SMS_","OTP_","ASPNETCORE","DOTNET_ENVIRONMENT","Storage__","Site__","Otp__","Sms__","DATA_DIR","SITE_URL","DOTNET_API_URL"}.Any(p=>name.StartsWith(p,StringComparison.OrdinalIgnoreCase)))
                    start.Environment.Remove(name);
            start.Environment["ConnectionStrings__SqlServer"] = database.ConnectionString();
            start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
            start.Environment["DOTNET_ENVIRONMENT"] = "Production";
            start.Environment["Site__Url"] = "http://127.0.0.1:3000";
            start.Environment["Storage__Root"] = Path.Combine(database.DirectoryPath,"release-private-files");
            start.Environment["Sms__Enabled"] = "false";
            start.Environment["Otp__Enabled"] = "false";
            start.Environment["OTP_VERIFICATION_ENABLED"] = "true";
            start.Environment["Logging__LogLevel__Default"] = "Warning";
            var serve=arguments.Length==1 && arguments[0]==browser;
            if(serve)
            {
                start.RedirectStandardInput=true;
                start.Environment["NODE_EXTRA_CA_CERTS"]=certificatePath;
            }
            foreach (var argument in arguments) start.ArgumentList.Add(argument);
            using var process=Process.Start(start) ?? throw new InvalidOperationException("Cannot start packaged acceptance process.");
            if(serve)
            {
                await process.StandardInput.WriteAsync(tlsKey.ExportPkcs8PrivateKeyPem());
                process.StandardInput.Close();
            }
            try { await process.WaitForExitAsync().WaitAsync(serve ? TimeSpan.FromMinutes(5) : TimeSpan.FromMinutes(2)); }
            catch { if (!process.HasExited) process.Kill(entireProcessTree:true); throw; }
            if (process.ExitCode!=0) throw new InvalidOperationException("Packaged acceptance failed: "+Path.GetFileName(command)+" "+process.ExitCode);
        }

        await Execute(backend, "--migrate");
        await new OrderWorkflowFixtures(database).SeedIdentities();
        await Execute(backend, "--check");
        await Execute("node", Path.GetFullPath("scripts/test-readonly.mjs"), "--authorize-configured-database-readonly");
        await Execute("node", Path.Combine(root,"scripts","initialize-frontend.mjs"));
        await Execute("node", Path.Combine(root,"scripts","initialize-frontend.mjs"), "--check");
        await Execute("node", browser);
        await Execute(backend, "--check");
        var saved = await database.Read("""
            SELECT (SELECT COUNT(*) FROM dbo.Orders) AS orders,
                   (SELECT COUNT(*) FROM dbo.Files) AS order_files,
                   (SELECT COUNT(*) FROM dbo.Ticket_Attachments) AS ticket_files
            """);
        if (saved is null || Convert.ToInt32(saved["orders"]) != 2 ||
            Convert.ToInt32(saved["order_files"]) != 2 || Convert.ToInt32(saved["ticket_files"]) != 1)
            throw new InvalidOperationException("Unexpected order or file counts in the isolated release database.");
        Console.WriteLine("PASS packaged binaries persist the API and browser orders, their PDFs and the initial browser ticket attachment; final schema check passed.");
        return 0;
    }
}
