namespace ChapGiti.Application.Abstractions;

public interface IUnitOfWork
{
    // The delegate must only read persisted data and may be retried after a deadlock.
    Task<T> ReadOnly<T>(Func<Task<T>> query) => query();
    Task<T> Transaction<T>(Func<Task<T>> action);
}
