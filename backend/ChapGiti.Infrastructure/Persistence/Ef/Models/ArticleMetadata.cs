namespace ChapGiti.Infrastructure.Persistence.Ef.Models;

public partial class Article
{
    public string PublicationStatus {get;set;}="draft";
    public string? CoverAssetId {get;set;}
    public string CoverAlt {get;set;}="";
    public string? PublicAuthorName {get;set;}
    public DateTime? PublishedAt {get;set;}
    public int Version {get;set;}=1;
    public string ContentFormat {get;set;}="markdown-v1";
}
public sealed class ArticleSlugAlias
{
    public string Slug {get;set;}=null!;
    public string ArticleId {get;set;}=null!;
    public DateTime CreatedAt {get;set;}
}
