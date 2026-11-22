using System;
using System.Collections.Generic;

namespace ChapGiti.Infrastructure.Persistence.Ef.Models;

public partial class File
{
    public string Id { get; set; } = null!;

    public string OrderId { get; set; } = null!;

    public string ObjectKey { get; set; } = null!;

    public string Name { get; set; } = null!;

    public int Size { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? CorrectionId { get; set; }

    public int? FileVersion { get; set; }






}
