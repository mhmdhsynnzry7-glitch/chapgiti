using System;
using System.Collections.Generic;

namespace ChapGiti.Infrastructure.Persistence.Ef.Models;

public partial class TicketMessage
{
    public string Id { get; set; } = null!;

    public string TicketId { get; set; } = null!;

    public string Actor { get; set; } = null!;

    public string Message { get; set; } = null!;

    public DateTime? ReadAt { get; set; }

    public DateTime CreatedAt { get; set; }

}
