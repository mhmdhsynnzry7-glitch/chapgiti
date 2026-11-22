using System;
using System.Collections.Generic;

namespace ChapGiti.Infrastructure.Persistence.Ef.Models;

public partial class CustomerPasswordReset
{
    public string TokenHash { get; set; } = null!;

    public string CustomerId { get; set; } = null!;

    public long CreatedAt { get; set; }

    public long Expires { get; set; }

    public long? ConsumedAt { get; set; }

}
