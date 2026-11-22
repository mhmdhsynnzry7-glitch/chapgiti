using System;
using System.Collections.Generic;

namespace ChapGiti.Infrastructure.Persistence.Ef.Models;

public partial class AdminUsername
{
    public string Username { get; set; } = null!;

    public string Email { get; set; } = null!;

}
