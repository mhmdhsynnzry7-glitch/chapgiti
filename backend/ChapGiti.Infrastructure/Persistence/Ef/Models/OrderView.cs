using System;
using System.Collections.Generic;

namespace ChapGiti.Infrastructure.Persistence.Ef.Models;

public partial class OrderView
{
    public long Id { get; set; }

    public string OrderId { get; set; } = null!;

    public string RecipientKind { get; set; } = null!;

    public string? CustomerId { get; set; }

    public string? AdminEmail { get; set; }

    public long SeenThroughSequence { get; set; }

    public DateTime SeenAt { get; set; }



}
