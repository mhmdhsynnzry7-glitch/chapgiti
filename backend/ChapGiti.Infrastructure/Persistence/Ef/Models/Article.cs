using System;
using System.Collections.Generic;

namespace ChapGiti.Infrastructure.Persistence.Ef.Models;

public partial class Article
{
    public string Id { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Excerpt { get; set; } = null!;

    public string Content { get; set; } = null!;

    public string Category { get; set; } = null!;

    public bool Published { get; set; }

    public string SeoTitle { get; set; } = null!;

    public string SeoDescription { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
