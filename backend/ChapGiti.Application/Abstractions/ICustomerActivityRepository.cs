namespace ChapGiti.Application.Abstractions;

public interface ICustomerActivityRepository
{
    Task<(List<Dictionary<string,object?>> Items,long Total)> List(string query,string type,DateTime? from,DateTime? until,int page,int pageSize, bool oldestFirst = false);
}
