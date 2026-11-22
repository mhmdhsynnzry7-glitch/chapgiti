using System;
using System.Collections.Generic;

namespace ChapGiti.Infrastructure.Persistence.Ef.Models;

public partial class SchemaMigration
{
    public string Name { get; set; } = null!;

    public string Checksum { get; set; } = null!;

    public DateTime AppliedAt { get; set; }
}
