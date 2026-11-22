using System;
using System.Collections.Generic;

namespace ChapGiti.Infrastructure.Persistence.Ef.Models;

public partial class Notification
{
    public string Id { get; set; } = null!;

    public long Sequence { get; set; }

    public string RecipientKind { get; set; } = null!;

    public string? CustomerId { get; set; }

    public string? AdminEmail { get; set; }

    public string EventType { get; set; } = null!;

    public string EntityKind { get; set; } = null!;

    public string EntityId { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Body { get; set; } = null!;

    public string TargetPath { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? ReadAt { get; set; }

    public string DedupeKey { get; set; } = null!;


}
