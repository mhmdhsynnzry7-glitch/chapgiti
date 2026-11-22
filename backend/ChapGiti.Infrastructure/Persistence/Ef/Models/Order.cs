using System;
using System.Collections.Generic;

namespace ChapGiti.Infrastructure.Persistence.Ef.Models;

public partial class Order
{
    public string Id { get; set; } = null!;

    public string Code { get; set; } = null!;

    public string Customer { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Service { get; set; } = null!;

    public string Spec { get; set; } = null!;

    public long Estimate { get; set; }

    public long? FinalPrice { get; set; }

    public string Status { get; set; } = null!;

    public string Notes { get; set; } = null!;

    public string AdminNotes { get; set; } = null!;

    public int Version { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string? CustomerId { get; set; }

    public byte WorkflowVersion { get; set; }

    public string? InformationRequest { get; set; }

    public bool InformationRequiresFile { get; set; }

    public string? ApprovedFileId { get; set; }











}
