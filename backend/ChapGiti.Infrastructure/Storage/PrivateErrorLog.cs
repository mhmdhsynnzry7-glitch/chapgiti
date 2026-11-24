using System.Text.Json;

namespace ChapGiti.Infrastructure.Storage;

internal sealed class PrivateErrorLog(IConfiguration config, TimeProvider clock) : IPrivateErrorLog
{
    private readonly object gate = new();

    public void Write(string operation, string code, string requestId, int status)
    {
        var storage = config["Storage:Root"];
        if (storage is null || !Path.IsPathFullyQualified(storage)) return;
        var directory = Path.Combine(storage, "logs");
        var now = clock.GetUtcNow();
        var entry = JsonSerializer.Serialize(new {
            time = now, level = "Error", code, requestId, operation, status
        });
        lock (gate)
        {
            try
            {
                Directory.CreateDirectory(directory);
                var size = Math.Clamp(config.GetValue("Diagnostics:FileBytes", 1024*1024), 1024, 16*1024*1024);
                var retain = Math.Clamp(config.GetValue("Diagnostics:RetainFiles", 12), 2, 60);
                var files = new DirectoryInfo(directory).GetFiles("errors-*.jsonl").OrderBy(f => f.Name, StringComparer.Ordinal).ToList();
                var prefix = "errors-" + now.ToString("yyyyMMdd") + "-";
                var current = files.LastOrDefault();
                if (current is null || !current.Name.StartsWith(prefix, StringComparison.Ordinal) || current.Length + System.Text.Encoding.UTF8.GetByteCount(entry) + 2 > size)
                {
                    var index = current != null && current.Name.StartsWith(prefix, StringComparison.Ordinal) &&
                        int.TryParse(Path.GetFileNameWithoutExtension(current.Name)[prefix.Length..], out var i) ? i+1 : 0;
                    current = new FileInfo(Path.Combine(directory, prefix + index.ToString("D6") + ".jsonl"));
                    files.Add(current);
                }
                File.AppendAllText(current.FullName, entry + Environment.NewLine);
                foreach (var old in files.Take(Math.Max(0, files.Count - retain))) old.Delete();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or FormatException) { }
        }
    }
}
