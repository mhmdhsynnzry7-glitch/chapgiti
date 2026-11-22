using System;
using System.Collections.Generic;

namespace ChapGiti.Infrastructure.Persistence.Ef.Models;

public partial class OrderCommand
{
    public string Id { get; set; } = null!;

    public string OrderId { get; set; } = null!;

    public string Actor { get; set; } = null!;

    public string Kind { get; set; } = null!;

    public string PayloadHash { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

}
