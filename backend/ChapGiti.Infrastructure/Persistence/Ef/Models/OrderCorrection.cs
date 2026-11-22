using System;
using System.Collections.Generic;

namespace ChapGiti.Infrastructure.Persistence.Ef.Models;

public partial class OrderCorrection
{
    public string Id { get; set; } = null!;

    public string OrderId { get; set; } = null!;

    public int Revision { get; set; }

    public string State { get; set; } = null!;

    public bool IsActive { get; set; }

    public string RequestText { get; set; } = null!;

    public bool RequiresNewPdf { get; set; }

    public string? RequestedByAdmin { get; set; }

    public DateTime? RequestedAt { get; set; }

    public bool IsLegacy { get; set; }

    public string? BaseFileId { get; set; }

    public string? SubmissionText { get; set; }

    public string? SubmittedByCustomer { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public string? FileId { get; set; }

    public string? ReviewedByAdmin { get; set; }

    public string? ReviewReason { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? ApprovedFileId { get; set; }

    public int Version { get; set; }








}
