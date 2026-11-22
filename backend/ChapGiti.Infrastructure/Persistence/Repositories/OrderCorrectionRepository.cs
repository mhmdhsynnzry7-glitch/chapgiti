using ChapGiti.Infrastructure.Persistence.Ef;
using Microsoft.EntityFrameworkCore;
using Models = ChapGiti.Infrastructure.Persistence.Ef.Models;
using OrderCorrection = ChapGiti.Application.Orders.OrderCorrection;

namespace ChapGiti.Infrastructure.Persistence.Repositories;

internal sealed class OrderCorrectionRepository(SqlStore db) : IOrderCorrectionRepository
{
    public Task<bool> SupportsCorrections() => db.SupportsCorrections();
    public async Task<List<OrderCorrection>> Corrections(string id) => !await SupportsCorrections()?[]:(await db.Context.OrderCorrections.AsNoTracking().Where(c=>c.OrderId==id).OrderBy(c=>c.Revision).ToListAsync()).Select(c=>new OrderCorrection(c.Id,c.OrderId,c.Revision,c.State,c.RequestText,c.RequiresNewPdf,c.RequestedByAdmin,EfRows.Stamp(c.RequestedAt),c.IsLegacy,c.BaseFileId,c.SubmissionText,c.SubmittedByCustomer,EfRows.Stamp(c.SubmittedAt),c.FileId,c.ReviewedByAdmin,c.ReviewReason,EfRows.Stamp(c.ReviewedAt),c.ApprovedFileId,c.Version)).ToList();
    public async Task InsertCorrection(OrderCorrection c) => await db.Insert(new Models.OrderCorrection{Id=c.Id,OrderId=c.OrderId,Revision=c.Revision,State="requested",IsActive=true,RequestText=c.RequestText,RequiresNewPdf=c.RequiresNewPdf,RequestedByAdmin=c.RequestedByAdmin,RequestedAt=EfRows.Date(c.RequestedAt,true),IsLegacy=c.Legacy,BaseFileId=c.BaseFileId,Version=1});
    public async Task<bool> SaveCorrection(OrderCorrection previous,OrderCorrection c) => await db.Context.OrderCorrections.Where(r=>r.Id==c.Id&&r.OrderId==c.OrderId&&r.Version==previous.Version&&r.State==previous.State&&r.IsActive).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.State,c.State).SetProperty(r=>r.IsActive,ChapGiti.Domain.Orders.OrderCorrectionWorkflow.Active(c.State)).SetProperty(r=>r.SubmissionText,c.SubmissionText).SetProperty(r=>r.SubmittedByCustomer,c.SubmittedByCustomer).SetProperty(r=>r.SubmittedAt,EfRows.Date(c.SubmittedAt,true)).SetProperty(r=>r.FileId,c.FileId).SetProperty(r=>r.ReviewedByAdmin,c.ReviewedByAdmin).SetProperty(r=>r.ReviewReason,c.ReviewReason).SetProperty(r=>r.ReviewedAt,EfRows.Date(c.ReviewedAt,true)).SetProperty(r=>r.ApprovedFileId,c.ApprovedFileId).SetProperty(r=>r.Version,r=>r.Version+1))==1;
    public async Task<List<ChapGiti.Application.Orders.OrderFileRecord>> Files(string id) => (await db.Context.Files.AsNoTracking().Where(f=>f.OrderId==id).OrderBy(f=>f.FileVersion).ThenBy(f=>f.CreatedAt).ThenBy(f=>f.Id).ToListAsync()).Select(f=>new ChapGiti.Application.Orders.OrderFileRecord(f.Id,f.OrderId,f.ObjectKey,f.Name,f.Size,EfRows.Stamp(f.CreatedAt)!,f.CorrectionId,f.FileVersion)).ToList();
    public async Task LinkFile(string id,string fileId,string? correction,int version){if(await SupportsCorrections())await db.Context.Files.Where(f=>f.OrderId==id&&f.Id==fileId).ExecuteUpdateAsync(s=>s.SetProperty(f=>f.CorrectionId,correction).SetProperty(f=>f.FileVersion,version));}
    public async Task SaveSpecification(string id, string spec) => await db.Context.Orders.Where(o => o.Id == id).ExecuteUpdateAsync(s => s.SetProperty(o => o.Spec, spec));
    public async Task SetApprovedFile(string id,string? fileId){if(await SupportsCorrections())await db.Context.Orders.Where(o=>o.Id==id).ExecuteUpdateAsync(s=>s.SetProperty(o=>o.ApprovedFileId,fileId));}
}

