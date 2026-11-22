namespace ChapGiti.Application.Orders;

public sealed record OrderCorrection(
    string Id, string OrderId, int Revision, string State, string RequestText, bool RequiresNewPdf,
    string? RequestedByAdmin, string? RequestedAt, bool Legacy, string? BaseFileId,
    string? SubmissionText = null, string? SubmittedByCustomer = null, string? SubmittedAt = null, string? FileId = null,
    string? ReviewedByAdmin = null, string? ReviewReason = null, string? ReviewedAt = null, string? ApprovedFileId = null,
    long Version = 1);

public sealed record OrderFileRecord(string Id, string OrderId, string ObjectKey, string Name, int Size,
    string CreatedAt, string? RevisionId = null, int? FileVersion = null);
