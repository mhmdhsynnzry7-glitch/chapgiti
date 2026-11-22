using System;
using System.Collections.Generic;

namespace ChapGiti.Infrastructure.Persistence.Ef.Models;

public partial class Ticket
{
    public string Id { get; set; } = null!;

    public string CustomerId { get; set; } = null!;

    public string Subject { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string? OrderId { get; set; }



}
