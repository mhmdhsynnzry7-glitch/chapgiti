using System;
using System.Collections.Generic;

namespace ChapGiti.Infrastructure.Persistence.Ef.Models;

public partial class CustomerAccount
{
    public string Phone { get; set; } = null!;

    public string RegistrationId { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public DateTime CreatedAt { get; set; }



}
