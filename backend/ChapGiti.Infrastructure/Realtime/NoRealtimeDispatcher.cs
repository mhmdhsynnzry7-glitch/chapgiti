using ChapGiti.Application.Realtime;

namespace ChapGiti.Infrastructure.Realtime;

// Maintenance/CLI hosts have no connections. The API composition root supplies SignalR.
internal sealed class NoRealtimeDispatcher : IRealtimeDispatcher
{
    public Task Dispatch(RealtimeUpdate update) => Task.CompletedTask;
}
