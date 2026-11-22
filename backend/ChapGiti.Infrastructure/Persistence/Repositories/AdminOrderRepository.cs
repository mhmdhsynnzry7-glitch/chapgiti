using ChapGiti.Infrastructure.Persistence.Ef;
using Microsoft.EntityFrameworkCore;
using Models = ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Repositories;

internal sealed class AdminOrderRepository(SqlStore db) : IAdminOrderRepository
{
    private IQueryable<Models.Order> Orders => db.Context.Orders.AsNoTracking();
    public Task<List<Dictionary<string,object?>>> StatusCounts() => Orders.GroupBy(o=>o.Status).Select(g=>new{Status=g.Key,Count=g.Count()}).Rows(db.Context);
    public async Task<Dictionary<string,object?>?> Summary() => new(){["total"]=await Orders.CountAsync(),["value"]=await Orders.Where(o=>o.Status!="cancelled").SumAsync(o=>(long?)(o.FinalPrice??o.Estimate))??0L,["customers"]=await Orders.Select(o=>o.Phone).Distinct().CountAsync()};
    public Task<List<Dictionary<string,object?>>> RecentOrders() => Orders.OrderByDescending(o=>o.CreatedAt).ThenByDescending(o=>o.Id).Take(6).Select(o=>new{o.Id,o.Code,o.Customer,o.Phone,o.Title,o.Status,o.Estimate,o.FinalPrice,o.CreatedAt}).Rows(db.Context);
    public async Task<List<Dictionary<string,object?>>> DailyTrend(DateTime since) => (await Orders.Where(o=>o.CreatedAt>=since).GroupBy(o=>o.CreatedAt.Date).Select(g=>new{Day=g.Key,Count=g.Count()}).OrderBy(g=>g.Day).ToListAsync()).Select(g=>new Dictionary<string,object?>{["day"]=g.Day.ToString("yyyy-MM-dd"),["count"]=g.Count}).ToList();
    public async Task<Dictionary<string,object?>?> UnreadMessages() => new(){["count"]=await db.Context.Messages.CountAsync(m=>!m.Read)};
    private IQueryable<Models.Order> Search(string status,string search,string pattern) => Orders.Where(o=>(status==""||o.Status==status)&&(search==""||EF.Functions.Like(o.Customer,pattern)||EF.Functions.Like(o.Title,pattern)||EF.Functions.Like(o.Code,pattern)||EF.Functions.Like(o.Phone,pattern)));
    public Task<List<Dictionary<string,object?>>> SearchOrders(string status,string search,string pattern,int offset,int limit, bool oldestFirst = false) => (oldestFirst ? Search(status,search,pattern).OrderBy(o=>o.CreatedAt).ThenBy(o=>o.Id) : Search(status,search,pattern).OrderByDescending(o=>o.CreatedAt).ThenByDescending(o=>o.Id)).Skip(offset).Take(limit).Select(o=>new{o.Id,o.Code,o.Customer,o.Phone,o.Title,o.Status,o.Estimate,o.FinalPrice,o.CreatedAt}).Rows(db.Context);
    public async Task<Dictionary<string,object?>?> CountOrders(string status,string search,string pattern) => new(){["count"]=await Search(status,search,pattern).CountAsync()};
    public Task<List<Dictionary<string,object?>>> ExportOrders() => Orders.OrderByDescending(o=>o.CreatedAt).Take(10000).Select(o=>new{o.Code,o.Customer,o.Phone,o.Title,o.Status,o.Estimate,o.FinalPrice,o.CreatedAt}).Rows(db.Context);
    public Task<Dictionary<string,object?>?> FindOrder(string id) => Orders.Where(o=>o.Id==id).FirstRow(db.Context);
    public Task<Dictionary<string,object?>?> LockOrder(string id) => db.One("SELECT * FROM dbo.Orders WITH(UPDLOCK,HOLDLOCK) WHERE id=@p0",id);
    public Task<bool> SupportsOrderNotes() => db.SupportsOrderNotes();
    public Task<List<Dictionary<string,object?>>> InternalNotes(string id) => db.Context.OrderNotes.AsNoTracking().Where(n=>n.OrderId==id).OrderByDescending(n=>n.CreatedAt).ThenByDescending(n=>n.Id).Select(n=>new{n.Id,n.Note,n.Actor,n.CreatedAt}).Rows(db.Context);
    public Task<Dictionary<string,object?>?> LockInternalNote(string id) => db.One("SELECT order_id,note,actor FROM dbo.Order_Notes WITH(UPDLOCK,HOLDLOCK) WHERE id=@p0",id);
    public async Task InsertInternalNote(string id,string orderId,string note,string actor,string createdAt) => await db.Insert(new Models.OrderNote{Id=id,OrderId=orderId,Note=note,Actor=actor,CreatedAt=EfRows.Date(createdAt)});
    public Task<List<Dictionary<string,object?>>> OrderFiles(string id) => db.Context.Files.AsNoTracking().Where(f=>f.OrderId==id).Select(f=>new{f.Id,f.Name,f.Size,f.CreatedAt}).Rows(db.Context);
    public Task<List<Dictionary<string,object?>>> OrderEvents(string id) => db.Context.Events.AsNoTracking().Where(e=>e.OrderId==id).OrderBy(e=>e.CreatedAt).ThenBy(e=>e.Id).Rows(db.Context);
    public Task<List<Dictionary<string,object?>>> OrderSms(string id) => db.Context.SmsLogs.AsNoTracking().Where(s=>s.OrderId==id).OrderByDescending(s=>s.CreatedAt).Rows(db.Context);
    public Task<int> UpdateOrder(string status,long? finalPrice,string notes,string updatedAt,string id,long version) => db.Context.Orders.Where(o=>o.Id==id&&o.Version==version).ExecuteUpdateAsync(s=>s.SetProperty(o=>o.Status,status).SetProperty(o=>o.FinalPrice,finalPrice).SetProperty(o=>o.AdminNotes,notes).SetProperty(o=>o.UpdatedAt,EfRows.Date(updatedAt)).SetProperty(o=>o.Version,o=>o.Version+1));
    public async Task<int> InsertEvent(string id,string orderId,string status,string note,string actor,string createdAt){await db.Insert(new Models.Event{Id=id,OrderId=orderId,Status=status,Note=note,Actor=actor,CreatedAt=EfRows.Date(createdAt)});return 1;}
    public Task<Dictionary<string,object?>?> FindFile(string id) => db.Context.Files.AsNoTracking().Where(f=>f.Id==id).Select(f=>new{f.ObjectKey,f.Name}).FirstRow(db.Context);
    public Task<List<Dictionary<string,object?>>> CustomerTotals() => Orders.GroupBy(o=>o.Phone).Select(g=>new{Phone=g.Key,Name=g.Max(o=>o.Customer),Email=g.Max(o=>o.Email),Orders=g.Count(),Value=g.Sum(o=>o.Status!="cancelled"?(o.FinalPrice??o.Estimate):0),LastOrder=g.Max(o=>o.CreatedAt)}).OrderByDescending(g=>g.LastOrder).Take(500).Rows(db.Context);
    public Task<List<Dictionary<string,object?>>> CustomerProfiles() => db.Context.CustomerRegistrations.AsNoTracking().Where(r=>r.FirstName!=""&&r.LastName!="").OrderByDescending(r=>r.UpdatedAt).Take(1000).Select(r=>new{r.Phone,r.FirstName,r.LastName,r.UpdatedAt}).Rows(db.Context);
    public Task<List<Dictionary<string,object?>>> Messages() => db.Context.Messages.AsNoTracking().OrderByDescending(m=>m.CreatedAt).Take(200).Rows(db.Context);
    public Task<int> MarkMessageRead(string id) => db.Context.Messages.Where(m=>m.Id==id).ExecuteUpdateAsync(s=>s.SetProperty(m=>m.Read,true));
    public Task<List<Dictionary<string,object?>>> SmsLogs() => db.Context.SmsLogs.AsNoTracking().OrderByDescending(s=>s.CreatedAt).Take(200).Rows(db.Context);
    public Task<List<Dictionary<string,object?>>> AuditLogs() => db.Context.AuditLogs.AsNoTracking().OrderByDescending(a=>a.CreatedAt).Take(100).Rows(db.Context);
}
