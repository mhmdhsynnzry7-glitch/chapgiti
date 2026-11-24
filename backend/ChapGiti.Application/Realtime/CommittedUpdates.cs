using ChapGiti.Application.Abstractions;
using ChapGiti.Application.Notifications;

namespace ChapGiti.Application.Realtime;

// Wake-up hints only. Content, counts, ordering and permissions remain REST/SQL authoritative.
public sealed record RealtimeUpdate(string Kind, string Key, NotificationRecipient? Recipient = null, string? TicketId = null)
{
    public static RealtimeUpdate Notifications(NotificationRecipient recipient, string key) =>
        new("notifications", $"notifications:{recipient.Kind}:{recipient.Id}:{key}", recipient);
    public static RealtimeUpdate Ticket(string id, string key)
    {
        var canonical = Guid.TryParseExact(id, "D", out var value) ? value.ToString("D") : id;
        return new("ticket", $"ticket:{canonical}:{key}", TicketId: canonical);
    }
}

public interface IRealtimeDispatcher
{
    Task Dispatch(RealtimeUpdate update);
}

/// <summary>Request-scoped commit buffer. Nested operations never flush an outer transaction.</summary>
public sealed class CommittedUpdates(IRealtimeDispatcher dispatcher)
{
    private bool active;
    private readonly Dictionary<string, RealtimeUpdate> pending = [];

    public void Enqueue(RealtimeUpdate update)
    {
        if (!active) throw new InvalidOperationException("Realtime changes must be buffered inside the persistence transaction.");
        pending.TryAdd(update.Key, update);
    }

    public async Task<T> Transaction<T>(Func<Task<T>> persist)
    {
        if (active) return await persist();
        active = true;
        T result;
        try { result = await persist(); }
        catch { pending.Clear(); throw; }
        finally { active = false; }
        var committed = pending.Values.ToArray();
        pending.Clear();
        foreach (var update in committed) await PublishCommitted(update);
        return result;
    }

    // Also used after a successful standalone atomic read-state UPDATE.
    public async Task PublishCommitted(RealtimeUpdate update)
    {
        if (active) { Enqueue(update); return; }
        try { await dispatcher.Dispatch(update); }
        catch { /* Best-effort delivery cannot undo a commit. The API-backed polling path recovers. */ }
    }
}

public sealed class CommitAwareUnitOfWork(IUnitOfWork inner, CommittedUpdates updates) : IUnitOfWork
{
    public Task<T> ReadOnly<T>(Func<Task<T>> query) => inner.ReadOnly(query);
    public Task<T> Transaction<T>(Func<Task<T>> action) => updates.Transaction(() => inner.Transaction(action));
}
