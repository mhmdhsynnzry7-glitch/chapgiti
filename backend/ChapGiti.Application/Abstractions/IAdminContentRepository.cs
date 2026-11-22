namespace ChapGiti.Application.Abstractions;

public interface IAdminContentRepository
{
    Task<bool> ServiceInUse(string code);
    Task<int> InsertAsset(string id, string mime, int size, string createdAt);
    Task<Dictionary<string, object?>?> LockSetting(string key);
    Task<Dictionary<string, object?>?> FindAsset(string id);
}
