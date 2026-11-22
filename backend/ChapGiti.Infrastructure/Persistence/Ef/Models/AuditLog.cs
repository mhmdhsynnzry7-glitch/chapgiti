using System;
using System.Collections.Generic;

namespace ChapGiti.Infrastructure.Persistence.Ef.Models;

public partial class AuditLog
{
    public string Id { get; set; } = null!;

    public string Actor { get; set; } = null!;

    public string Action { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}
