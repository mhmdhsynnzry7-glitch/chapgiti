using System;
using System.Collections.Generic;

namespace ChapGiti.Infrastructure.Persistence.Ef.Models;

public partial class CustomerSession
{
    public string TokenHash { get; set; } = null!;

    public string RegistrationId { get; set; } = null!;

    public long Expires { get; set; }

}
