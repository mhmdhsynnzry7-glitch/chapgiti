using System;
using System.Collections.Generic;

namespace ChapGiti.Infrastructure.Persistence.Ef.Models;

public partial class SmsLog
{
    public string Id { get; set; } = null!;

    public string EventId { get; set; } = null!;

    public string OrderId { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? Receipt { get; set; }

    public string? Error { get; set; }

    public DateTime CreatedAt { get; set; }

}
