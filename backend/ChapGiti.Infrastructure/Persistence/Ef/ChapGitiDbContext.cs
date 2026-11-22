using System;
using System.Collections.Generic;
using ChapGiti.Infrastructure.Persistence.Ef.Models;
using Microsoft.EntityFrameworkCore;
using File = ChapGiti.Infrastructure.Persistence.Ef.Models.File;

namespace ChapGiti.Infrastructure.Persistence.Ef;

public partial class ChapGitiDbContext : DbContext
{
    public ChapGitiDbContext(DbContextOptions<ChapGitiDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AdminSession> AdminSessions { get; set; }

    public virtual DbSet<AdminUser> AdminUsers { get; set; }

    public virtual DbSet<AdminUsername> AdminUsernames { get; set; }

    public virtual DbSet<Article> Articles { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<AuthRefreshCredential> AuthRefreshCredentials { get; set; }

    public virtual DbSet<AuthSession> AuthSessions { get; set; }

    public virtual DbSet<BannerAsset> BannerAssets { get; set; }

    public virtual DbSet<CustomerAccount> CustomerAccounts { get; set; }

    public virtual DbSet<CustomerOtpChallenge> CustomerOtpChallenges { get; set; }

    public virtual DbSet<CustomerPasswordReset> CustomerPasswordResets { get; set; }

    public virtual DbSet<CustomerRegistration> CustomerRegistrations { get; set; }

    public virtual DbSet<CustomerRole> CustomerRoles { get; set; }

    public virtual DbSet<CustomerSession> CustomerSessions { get; set; }

    public virtual DbSet<Event> Events { get; set; }

    public virtual DbSet<File> Files { get; set; }

    public virtual DbSet<ImportHistory> ImportHistories { get; set; }

    public virtual DbSet<Message> Messages { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderCommand> OrderCommands { get; set; }

    public virtual DbSet<OrderCorrection> OrderCorrections { get; set; }

    public virtual DbSet<OrderNote> OrderNotes { get; set; }

    public virtual DbSet<OrderQuote> OrderQuotes { get; set; }

    public virtual DbSet<OrderView> OrderViews { get; set; }

    public virtual DbSet<PendingCustomerRegistration> PendingCustomerRegistrations { get; set; }

    public virtual DbSet<RateLimit> RateLimits { get; set; }

    public virtual DbSet<SchemaMigration> SchemaMigrations { get; set; }

    public virtual DbSet<Setting> Settings { get; set; }

    public virtual DbSet<SmsLog> SmsLogs { get; set; }

    public virtual DbSet<Ticket> Tickets { get; set; }

    public virtual DbSet<TicketMessage> TicketMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ChapGitiDbContext).Assembly);

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
