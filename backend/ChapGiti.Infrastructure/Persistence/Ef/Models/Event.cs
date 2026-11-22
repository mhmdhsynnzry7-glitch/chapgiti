using System;
using System.Collections.Generic;

namespace ChapGiti.Infrastructure.Persistence.Ef.Models;

public partial class Event
{
    public string Id { get; set; } = null!;

    public string OrderId { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string Note { get; set; } = null!;

    public string Actor { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

}
