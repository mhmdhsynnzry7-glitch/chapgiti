using System;
using System.Collections.Generic;

namespace ChapGiti.Infrastructure.Persistence.Ef.Models;

public partial class CustomerRole
{
    public string CustomerId { get; set; } = null!;

    public string Role { get; set; } = null!;

    public string AssignedBy { get; set; } = null!;

    public DateTime AssignedAt { get; set; }

}
