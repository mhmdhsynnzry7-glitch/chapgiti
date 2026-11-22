using System;
using System.Collections.Generic;

namespace ChapGiti.Infrastructure.Persistence.Ef.Models;

public partial class RateLimit
{
    public string Key { get; set; } = null!;

    public int Count { get; set; }

    public long Expires { get; set; }
}
