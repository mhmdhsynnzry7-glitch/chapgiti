using System;
using System.Collections.Generic;

namespace ChapGiti.Infrastructure.Persistence.Ef.Models;

public partial class AdminUser
{
    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public string? LoginEmail { get; set; }

    public string EffectiveEmail { get; set; } = null!;








}
