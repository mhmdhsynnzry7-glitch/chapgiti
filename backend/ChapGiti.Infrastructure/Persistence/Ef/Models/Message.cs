using System;
using System.Collections.Generic;

namespace ChapGiti.Infrastructure.Persistence.Ef.Models;

public partial class Message
{
    public string Id { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string Subject { get; set; } = null!;

    public string Message1 { get; set; } = null!;

    public bool Read { get; set; }

    public DateTime CreatedAt { get; set; }
}
