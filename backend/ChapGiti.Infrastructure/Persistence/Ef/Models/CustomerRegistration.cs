using System;
using System.Collections.Generic;

namespace ChapGiti.Infrastructure.Persistence.Ef.Models;

public partial class CustomerRegistration
{
    public string Id { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public bool PhoneVerified { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }










}
