using System;
using System.Collections.Generic;

namespace ChapGiti.Infrastructure.Persistence.Ef.Models;

public partial class OrderQuote
{
    public string Id { get; set; } = null!;

    public string OrderId { get; set; } = null!;

    public int Revision { get; set; }

    public long AmountToman { get; set; }

    public string ScopeText { get; set; } = null!;

    public string DeliveryTerms { get; set; } = null!;

    public string ExclusionsText { get; set; } = null!;

    public string CreatedByAdmin { get; set; } = null!;

    public DateTime SentAt { get; set; }

    public string State { get; set; } = null!;

    public DateTime? AcceptedAt { get; set; }

    public string? AcceptedByCustomer { get; set; }

    public DateTime? DecidedAt { get; set; }

    public string DecisionReason { get; set; } = null!;

    public int Version { get; set; }



}
