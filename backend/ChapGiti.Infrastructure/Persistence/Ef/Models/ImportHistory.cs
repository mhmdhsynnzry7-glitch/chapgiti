using System;
using System.Collections.Generic;

namespace ChapGiti.Infrastructure.Persistence.Ef.Models;

public partial class ImportHistory
{
    public string SourceHash { get; set; } = null!;

    public DateTime ImportedAt { get; set; }
}
