using ChapGiti.Application.Orders;

namespace ChapGiti.Application.Abstractions;

public interface IOrderCorrectionRepository
{
    Task<bool> SupportsCorrections();
    Task<List<OrderCorrection>> Corrections(string orderId);
    Task InsertCorrection(OrderCorrection correction);
    Task<bool> SaveCorrection(OrderCorrection previous, OrderCorrection next);
    Task<List<OrderFileRecord>> Files(string orderId);
    Task LinkFile(string orderId, string fileId, string? revisionId, int version);
    Task SaveSpecification(string orderId, string specification);
    Task SetApprovedFile(string orderId, string? fileId);
}
