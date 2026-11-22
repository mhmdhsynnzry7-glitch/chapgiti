using ChapGiti.Infrastructure.Persistence.Ef;
using Microsoft.EntityFrameworkCore;
using Models = ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Repositories;

internal sealed class AdminContentRepository(SqlStore db) : IAdminContentRepository
{
    public async Task<bool> ServiceInUse(string code) =>
        await db.One("SELECT TOP (1) 1 used FROM dbo.Orders WHERE service=@p0", code) != null;

    public Task<Dictionary<string,object?>?> LockSetting(string key)=>
        db.One("SELECT [value] FROM dbo.Settings WITH(UPDLOCK,HOLDLOCK) WHERE [key]=@p0",key);
    public Task<int> InsertAsset(string id,string mime,int size,string createdAt)=>
        db.Insert(new Models.BannerAsset{Id=id,Mime=mime,Size=size,CreatedAt=EfRows.Date(createdAt)});
    public Task<Dictionary<string,object?>?> FindAsset(string id)=>
        db.Context.BannerAssets.AsNoTracking().Where(a=>a.Id==id).Select(a=>new{a.Mime,a.Size}).FirstRow(db.Context);
}
