using System;
using System.Collections.Generic;

namespace ChapGiti.Infrastructure.Persistence.Ef.Models;

public partial class AuthSession
{
    public string Id { get; set; } = null!;

    public string? CustomerId { get; set; }

    public string? LegacyAdminEmail { get; set; }

    public string AccessHash { get; set; } = null!;

    public long AccessExpires { get; set; }

    public long CreatedAt { get; set; }

    public long Expires { get; set; }

    public long? RevokedAt { get; set; }



}
