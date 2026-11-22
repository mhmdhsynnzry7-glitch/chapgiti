using System;
using System.Collections.Generic;

namespace ChapGiti.Infrastructure.Persistence.Ef.Models;

public partial class BannerAsset
{
    public string Id { get; set; } = null!;

    public string Mime { get; set; } = null!;

    public int Size { get; set; }

    public DateTime CreatedAt { get; set; }
}
