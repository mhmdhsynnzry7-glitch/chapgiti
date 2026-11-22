using ChapGiti.Infrastructure.Persistence.Ef;
using Microsoft.EntityFrameworkCore;
using Models = ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Repositories;

internal sealed class OrderRepository(SqlStore db) : IOrderRepository
{
    public async Task<bool> TrackingCodeInUse(string code) =>
        await db.One("SELECT code FROM dbo.Orders WITH(UPDLOCK,HOLDLOCK) WHERE code=@p0",code) != null;

    private IQueryable<Models.Order> Orders => db.Context.Orders.AsNoTracking();
    public Task<Dictionary<string,object?>?> LockExistingOrder(string id) => db.One("SELECT id,code,phone,title,estimate,customer_id,spec FROM dbo.Orders WITH(UPDLOCK,HOLDLOCK) WHERE id=@p0",id);
    public async Task<int> InsertOrder(string id,string code,string customer,string phone,string email,string title,string service,string specification,long estimate,string createdAt,string customerId){await db.Insert(new Models.Order{Id=id,Code=code,Customer=customer,Phone=phone,Email=email,Title=title,Service=service,Spec=specification,Estimate=estimate,Status="new",Notes="",AdminNotes="",Version=1,WorkflowVersion=2,CreatedAt=EfRows.Date(createdAt),UpdatedAt=EfRows.Date(createdAt),CustomerId=customerId});return 1;}
    public async Task<int> InsertInitialEvent(string id,string orderId,string createdAt){await db.Insert(new Models.Event{Id=id,OrderId=orderId,Status="new",Note="درخواست شما ثبت شد.",Actor="customer",CreatedAt=EfRows.Date(createdAt)});return 1;}
    public Task<Dictionary<string,object?>?> TrackOrder(string code,string phone,string customerId) => Orders.Where(o=>o.Code==code&&o.Phone==phone&&o.CustomerId==customerId).Select(o=>new{o.Id,o.Code,o.Title,o.Status,o.Estimate,o.FinalPrice,o.CreatedAt,o.UpdatedAt,o.Spec,o.WorkflowVersion}).FirstRow(db.Context);
    public Task<List<Dictionary<string,object?>>> TrackingEvents(string id) => db.Context.Events.AsNoTracking().Where(e=>e.OrderId==id).OrderBy(e=>e.CreatedAt).ThenBy(e=>e.Id).Select(e=>new{e.Status,e.Note,e.CreatedAt,actor_role=e.Actor=="customer"||e.Actor.StartsWith("customer:")?"customer":e.Actor.Contains("@")||e.Actor.StartsWith("admin:")?"admin":"system"}).Rows(db.Context);
    private IQueryable<Models.Order> Owned(string id,string statuses){var states=statuses.Split(',',StringSplitOptions.RemoveEmptyEntries);return Orders.Where(o=>o.CustomerId==id&&(states.Length==0||states.Contains(o.Status)));}
    public Task<long> CountCustomerOrders(string id,string statuses) => Owned(id,statuses).LongCountAsync();
    public Task<List<Dictionary<string,object?>>> CustomerOrders(string id,int skip,int take,string statuses) => Owned(id,statuses).OrderByDescending(o=>o.CreatedAt).ThenByDescending(o=>o.Id).Skip(skip).Take(take).Select(o=>new{o.Id,o.Code,o.Title,o.Service,o.Status,o.Estimate,o.FinalPrice,o.CreatedAt,o.UpdatedAt,o.InformationRequest,QuoteAmount=db.Context.OrderQuotes.Where(q=>q.OrderId==o.Id).OrderByDescending(q=>q.Revision).Select(q=>(long?)q.AmountToman).FirstOrDefault(),QuoteRevision=db.Context.OrderQuotes.Where(q=>q.OrderId==o.Id).OrderByDescending(q=>q.Revision).Select(q=>(int?)q.Revision).FirstOrDefault(),QuoteState=db.Context.OrderQuotes.Where(q=>q.OrderId==o.Id).OrderByDescending(q=>q.Revision).Select(q=>q.State).FirstOrDefault()}).Rows(db.Context);
    public Task<Dictionary<string,object?>?> CustomerOrder(string id,string customerId) => Orders.Where(o=>o.Id==id&&o.CustomerId==customerId).Select(o=>new{o.Id,o.Code,o.Title,o.Service,o.Status,o.Estimate,o.FinalPrice,o.CreatedAt,o.UpdatedAt,o.Spec,o.Version,o.WorkflowVersion,o.InformationRequest,o.InformationRequiresFile,o.ApprovedFileId}).FirstRow(db.Context);
    public Task<List<Dictionary<string,object?>>> CustomerOrderFiles(string id,string customerId) => (from f in db.Context.Files.AsNoTracking() join o in Orders on f.OrderId equals o.Id where o.Id==id&&o.CustomerId==customerId orderby f.CreatedAt,f.Id select new{f.Id,f.Name,f.Size,f.CreatedAt}).Rows(db.Context);
    public Task<Dictionary<string,object?>?> FindOwnedOrder(string id,string code,string customerId) => Orders.Where(o=>o.Id==id&&o.Code==code&&o.CustomerId==customerId).Select(o=>new{o.Id}).FirstRow(db.Context);
    public Task<Dictionary<string,object?>?> FindOwnedFile(string id,string customerId) => (from f in db.Context.Files.AsNoTracking() join o in Orders on f.OrderId equals o.Id where f.Id==id&&o.CustomerId==customerId select new{f.ObjectKey,f.Name}).FirstRow(db.Context);
    public Task<Dictionary<string,object?>?> LockOwnedOrder(string id,string code,string customerId) => db.One("SELECT * FROM dbo.Orders WITH(UPDLOCK,HOLDLOCK) WHERE id=@p0 AND code=@p1 AND customer_id=@p2",id,code,customerId);
    public async Task<Dictionary<string,object?>?> CountFiles(string id) => new(){["n"]=await db.Context.Files.CountAsync(f=>f.OrderId==id)};
    public async Task<int> InsertFile(string id,string orderId,string key,string name,int size,string createdAt){await db.Insert(new Models.File{Id=id,OrderId=orderId,ObjectKey=key,Name=name,Size=size,CreatedAt=EfRows.Date(createdAt)});return 1;}
}
