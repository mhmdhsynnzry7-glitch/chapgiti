using System;
using System.Collections.Generic;

namespace ChapGiti.Infrastructure.Persistence.Ef.Models;

public partial class CustomerOtpChallenge
{
    public string Id { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string Purpose { get; set; } = null!;

    public string? CustomerId { get; set; }

    public string? CodeHash { get; set; }

    public long CreatedAt { get; set; }

    public long Expires { get; set; }

    public int Attempts { get; set; }

    public long? ConsumedAt { get; set; }

    public string DeliveryStatus { get; set; } = null!;


}
