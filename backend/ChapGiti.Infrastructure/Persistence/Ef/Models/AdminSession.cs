using System;
using System.Collections.Generic;

namespace ChapGiti.Infrastructure.Persistence.Ef.Models;

public partial class AdminSession
{
    public string TokenHash { get; set; } = null!;

    public string Email { get; set; } = null!;

    public long Expires { get; set; }

}
