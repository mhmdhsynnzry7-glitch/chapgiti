namespace ChapGiti.Application.Abstractions;

public interface IUnitOfWork
{
    Task<T> Transaction<T>(Func<Task<T>> action);
}
