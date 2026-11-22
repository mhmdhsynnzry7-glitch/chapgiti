using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using ChapGiti.Application.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace ChapGiti.Api.Realtime;

public sealed class NotificationsHub(RealtimeConnections connections) : Hub
{
    public override Task OnConnectedAsync() => connections.Open(Context, Groups, "notifications");
    public override Task OnDisconnectedAsync(Exception? exception) { connections.Remove(Context.ConnectionId); return Task.CompletedTask; }
}

public sealed class SupportHub(RealtimeConnections connections) : Hub
{
    public override Task OnConnectedAsync() => connections.Open(Context, Groups, "ticket");
    public override Task OnDisconnectedAsync(Exception? exception) { connections.Remove(Context.ConnectionId); return Task.CompletedTask; }
    public Task JoinTicket(string ticketId) => connections.Join(Context.ConnectionId, Groups, ticketId);
    public Task LeaveTicket() => connections.Leave(Context.ConnectionId, Groups);
}

public sealed class RealtimeConnections(RealtimeAccess access, ILogger<RealtimeConnections> logger)
{
    public sealed class Connection(HubCallerContext context, RealtimeIdentity identity, string kind)
    {
        public HubCallerContext Context { get; } = context;
        public RealtimeIdentity Identity { get; } = identity;
        public string Kind { get; } = kind;
        public string? TicketId { get; set; }
    }
    private readonly ConcurrentDictionary<string, Connection> connections = new();
    public IEnumerable<Connection> Current => connections.Values;
    public async Task Open(HubCallerContext context, IGroupManager groups, string kind)
    {
        if (context.GetHttpContext()?.Items[RealtimeAccess.ItemKey] is not RealtimeIdentity identity || !await access.Validate(identity))
        { context.Abort(); throw new HubException("SESSION_EXPIRED"); }
        connections[context.ConnectionId] = new(context, identity, kind);
        await groups.AddToGroupAsync(context.ConnectionId, kind + ":" + identity.Group);
        logger.LogInformation("Realtime connected: {Scope} {Kind}.", identity.Scope, kind);
    }
    public void Remove(string id)
    {
        if (connections.TryRemove(id, out var connection)) logger.LogInformation("Realtime disconnected: {Scope} {Kind}.", connection.Identity.Scope, connection.Kind);
    }
    public async Task Join(string id, IGroupManager groups, string ticketId)
    {
        if (!Guid.TryParseExact(ticketId, "D", out var parsed)) throw new HubException("TICKET_ACCESS_DENIED");
        ticketId = parsed.ToString("D");
        if (!connections.TryGetValue(id, out var connection) || connection.Kind != "ticket" ||
            !await access.Validate(connection.Identity, ticketId))
        { logger.LogWarning("Realtime ticket subscription rejected."); throw new HubException("TICKET_ACCESS_DENIED"); }
        await Leave(id, groups);
        connection.TicketId = ticketId;
        await groups.AddToGroupAsync(id, "ticket:" + ticketId);
    }
    public async Task Leave(string id, IGroupManager groups)
    {
        if (connections.TryGetValue(id, out var connection) && connection.TicketId is string ticket)
        { connection.TicketId = null; await groups.RemoveFromGroupAsync(id, "ticket:" + ticket); }
    }
}

public sealed class SignalRDispatcher(RealtimeConnections connections, RealtimeAccess access,
    IHubContext<NotificationsHub> notifications, IHubContext<SupportHub> support, ILogger<SignalRDispatcher> logger) : IRealtimeDispatcher
{
    public async Task Dispatch(RealtimeUpdate update)
    {
        var candidates = connections.Current.Where(connection => connection.Kind == update.Kind &&
            (update.Kind == "notifications" ? update.Recipient?.Kind == connection.Identity.Scope && update.Recipient.Id == connection.Identity.Owner
                : update.TicketId == connection.TicketId)).ToArray();
        foreach (var connection in candidates)
        {
            try
            {
                // Groups are routing aids, not authorization. Revalidate before EVERY delivery,
                // including logout, revocation, access expiry and ticket ownership changes.
                if (!await access.Validate(connection.Identity, update.Kind == "ticket" ? update.TicketId : null))
                { connections.Remove(connection.Context.ConnectionId); connection.Context.Abort(); continue; }
                var hub = update.Kind == "notifications" ? notifications.Clients : support.Clients;
                using var delivery = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await hub.Client(connection.Context.ConnectionId).SendAsync("Changed", new {
                    kind = update.Kind, eventId = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(update.Key)))
                }, delivery.Token);
                logger.LogInformation("Realtime update dispatched: {Scope} {Kind}.", connection.Identity.Scope, update.Kind);
            }
            catch (Exception error) { logger.LogWarning("Realtime dispatch failed: {Kind} {ErrorType}.", update.Kind, error.GetType().Name); }
        }
    }
}
