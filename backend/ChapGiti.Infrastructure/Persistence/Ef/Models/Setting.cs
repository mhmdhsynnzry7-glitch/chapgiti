using System;
using System.Collections.Generic;

namespace ChapGiti.Infrastructure.Persistence.Ef.Models;

public partial class Setting
{
    public string Key { get; set; } = null!;

    public string Value { get; set; } = null!;
}
