namespace ChapGiti.Infrastructure.Persistence.Ef.Models;

internal sealed class TicketAttachment
{
    public required string Id { get; set; }
    public required string MessageId { get; set; }
    public required string Name { get; set; }
    public required string Mime { get; set; }
    public required string Digest { get; set; }
    public int Size { get; set; }
    public int Position { get; set; }
}
