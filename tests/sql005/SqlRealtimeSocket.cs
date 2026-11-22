using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ChapGiti.Api.Realtime;
using Microsoft.Extensions.DependencyInjection;

namespace ChapGiti.SqlAcceptanceTests;

// Same JSON wire protocol as the existing memory API probe, now backed by the pinned SQL host.
internal sealed class SqlRealtimeSocket : IAsyncDisposable
{
    private readonly ClientWebSocket socket=new();
    private readonly CancellationTokenSource lifetime=new();
    private readonly TaskCompletionSource ready=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ConcurrentDictionary<string,TaskCompletionSource<JsonNode>> invocations=new();
    public ConcurrentQueue<JsonNode> Events {get;}=new();
    private Task? reader;
    public static async Task<SqlRealtimeSocket> Open(OrderWorkflowApi api,HttpClient http,CookieContainer cookies,string scope)
    {
        var path="/api/realtime/"+scope+"/notifications";
        using var request=new HttpRequestMessage(HttpMethod.Post,path+"/negotiate?negotiateVersion=1");request.Headers.Add("Origin",api.Host.Origin);
        using var response=await http.SendAsync(request);response.EnsureSuccessStatusCode();
        var negotiated=(await response.Content.ReadFromJsonAsync<JsonObject>())!;var probe=new SqlRealtimeSocket();
        try
        {
            probe.socket.Options.Cookies=cookies;probe.socket.Options.SetRequestHeader("Origin",api.Host.Origin);
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(8));
            await probe.socket.ConnectAsync(new(api.Host.Origin.Replace("http:","ws:")+path+"?id="+Uri.EscapeDataString(negotiated["connectionToken"]!.ToString())),timeout.Token);
            probe.reader=probe.Receive();await probe.Send(new{protocol="json",version=1});await probe.ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Eventually(()=>api.Host.Services.GetRequiredService<RealtimeConnections>().Current.Any(c=>c.Context.ConnectionId==negotiated["connectionId"]!.ToString()));
            return probe;
        }
        catch{await probe.DisposeAsync();throw;}
    }
    public static async Task Eventually(Func<bool> condition)
    {
        var until=DateTime.UtcNow.AddSeconds(5);
        while(!condition()&&DateTime.UtcNow<until)await Task.Delay(15);
        if(!condition())throw new InvalidOperationException("SQL realtime probe did not receive the expected committed event.");
    }
    private async Task Send(object value)=>await socket.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)+'\u001e'),WebSocketMessageType.Text,true,lifetime.Token);
    public async Task<JsonNode> Invoke(string method)
    {
        var id=Guid.NewGuid().ToString();var completion=new TaskCompletionSource<JsonNode>(TaskCreationOptions.RunContinuationsAsynchronously);invocations[id]=completion;
        await Send(new{type=1,invocationId=id,target=method,arguments=Array.Empty<string>()});return await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }
    private async Task Receive()
    {
        var bytes=new byte[8192];
        try
        {
            while(!lifetime.IsCancellationRequested)
            {
                using var message=new MemoryStream();WebSocketReceiveResult part;
                do{part=await socket.ReceiveAsync(new ArraySegment<byte>(bytes),lifetime.Token);if(part.MessageType==WebSocketMessageType.Close)return;message.Write(bytes,0,part.Count);}while(!part.EndOfMessage);
                foreach(var segment in Encoding.UTF8.GetString(message.ToArray()).Split('\u001e',StringSplitOptions.RemoveEmptyEntries))
                {
                    var json=JsonNode.Parse(segment)!;
                    if(json["type"]==null){ready.TrySetResult();continue;}
                    if(json["type"]!.GetValue<int>()==1&&json["target"]?.ToString()=="Changed")Events.Enqueue(json["arguments"]![0]!.DeepClone());
                    if(json["type"]!.GetValue<int>()==3&&invocations.TryRemove(json["invocationId"]!.ToString(),out var completion))completion.TrySetResult(json);
                }
            }
        }
        catch(Exception)when(lifetime.IsCancellationRequested||socket.State!=WebSocketState.Open){}
    }
    public async ValueTask DisposeAsync()
    {
        if(lifetime.IsCancellationRequested)return;
        lifetime.Cancel();socket.Abort();socket.Dispose();if(reader!=null){try{await reader;}catch{}}lifetime.Dispose();
    }
}
