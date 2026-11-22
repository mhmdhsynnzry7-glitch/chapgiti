using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Ef.Configurations;

// Reverse-engineered from the validated immutable SQL001–009 contract.
internal sealed class OrderQuoteConfiguration : IEntityTypeConfiguration<OrderQuote>
{
    public void Configure(EntityTypeBuilder<OrderQuote> entity)
    {
        entity.HasKey(e => e.Id);

        entity.ToTable("Order_Quotes");

        entity.HasIndex(e => new { e.OrderId, e.Revision }, "IX_OrderQuotes_AcceptedHistory")
            .IsDescending(false, true)
            .HasFilter("([state]='accepted')");

        entity.HasIndex(e => new { e.OrderId, e.Revision }, "IX_OrderQuotes_History").IsDescending(false, true);

        entity.HasIndex(e => new { e.OrderId, e.Revision }, "UQ_OrderQuotes_Revision").IsUnique();

        entity.HasIndex(e => e.OrderId, "UX_OrderQuotes_Active")
            .IsUnique()
            .HasFilter("([state]='sent')");

        entity.Property(e => e.Id)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("id");
        entity.Property(e => e.AcceptedAt)
            .HasPrecision(3)
            .HasColumnName("accepted_at");
        entity.Property(e => e.AcceptedByCustomer)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("accepted_by_customer");
        entity.Property(e => e.AmountToman).HasColumnName("amount_toman");
        entity.Property(e => e.CreatedByAdmin)
            .HasMaxLength(254)
            .HasColumnName("created_by_admin");
        entity.Property(e => e.DecidedAt)
            .HasPrecision(3)
            .HasColumnName("decided_at");
        entity.Property(e => e.DecisionReason)
            .HasMaxLength(900)
            .HasDefaultValue("")
            .HasColumnName("decision_reason");
        entity.Property(e => e.DeliveryTerms)
            .HasMaxLength(2000)
            .HasColumnName("delivery_terms");
        entity.Property(e => e.ExclusionsText)
            .HasMaxLength(2000)
            .HasColumnName("exclusions_text");
        entity.Property(e => e.OrderId)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("order_id");
        entity.Property(e => e.Revision).HasColumnName("revision");
        entity.Property(e => e.ScopeText)
            .HasMaxLength(3000)
            .HasColumnName("scope_text");
        entity.Property(e => e.SentAt)
            .HasPrecision(3)
            .HasColumnName("sent_at");
        entity.Property(e => e.State)
            .HasMaxLength(24)
            .IsUnicode(false)
            .HasColumnName("state");
        entity.Property(e => e.Version)
            .HasDefaultValue(1)
            .HasColumnName("version");

        entity.HasOne<CustomerRegistration>().WithMany()
            .HasForeignKey(d => d.AcceptedByCustomer)
            ;

        entity.HasOne<AdminUser>().WithMany()
            .HasForeignKey(d => d.CreatedByAdmin)
            .OnDelete(DeleteBehavior.ClientSetNull)
            ;

        entity.HasOne<Order>().WithMany()
            .HasForeignKey(d => d.OrderId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            ;
    }
}
