using Microsoft.EntityFrameworkCore;
using File = ChapGiti.Infrastructure.Persistence.Ef.Models.File;
using ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Ef;

// Actual immutable SQL001-009 CHECK definitions, verified in the isolated SQL contract.
internal static class LegacySchemaChecks
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<AuthSession>().ToTable("Auth_Sessions", table =>
        {
            table.HasCheckConstraint("CK_AuthPrincipal", "([customer_id] IS NOT NULL AND [legacy_admin_email] IS NULL OR [customer_id] IS NULL AND [legacy_admin_email] IS NOT NULL)");
        });
        model.Entity<CustomerOtpChallenge>().ToTable("Customer_Otp_Challenges", table =>
        {
            table.HasCheckConstraint("CK_OtpAttempts", "([attempts]>=(0) AND [attempts]<=(5))");
            table.HasCheckConstraint("CK_OtpDelivery", "([delivery_status]='failed' OR [delivery_status]='sent' OR [delivery_status]='pending')");
            table.HasCheckConstraint("CK_OtpPurpose", "([purpose]='verify-phone' OR [purpose]='register' OR [purpose]='reset' OR [purpose]='login')");
        });
        model.Entity<CustomerRole>().ToTable("Customer_Roles", table =>
        {
            table.HasCheckConstraint("CK_CustomerRole", "([role]='admin')");
        });
        model.Entity<File>().ToTable("Files", table =>
        {
            table.HasCheckConstraint("CK_Files_Version", "([file_version] IS NULL OR [file_version]>(0))");
        });
        model.Entity<Notification>().ToTable("Notifications", table =>
        {
            table.HasCheckConstraint("CK_Notifications_Entity", "(([entity_kind]='ticket' OR [entity_kind]='order') AND TRY_CAST([entity_id] AS [uniqueidentifier]) IS NOT NULL)");
            table.HasCheckConstraint("CK_Notifications_ReadTime", "([read_at] IS NULL OR [read_at]>=[created_at])");
            table.HasCheckConstraint("CK_Notifications_Recipient", "([recipient_kind]='customer' AND [customer_id] IS NOT NULL AND [admin_email] IS NULL OR [recipient_kind]='admin' AND [admin_email] IS NOT NULL AND [customer_id] IS NULL)");
            table.HasCheckConstraint("CK_Notifications_Target", "([target_path]=(case when [recipient_kind]='customer' AND [entity_kind]='order' then N'/account/orders/' when [recipient_kind]='admin' AND [entity_kind]='order' then N'/admin/orders/' when [recipient_kind]='customer' AND [entity_kind]='ticket' then N'/tickets/' when [recipient_kind]='admin' AND [entity_kind]='ticket' then N'/admin/tickets/'  end+[entity_id]))");
        });
        model.Entity<OrderCorrection>().ToTable("Order_Corrections", table =>
        {
            table.HasCheckConstraint("CK_OrderCorrections_Approval", "([state]='approved' OR [approved_file_id] IS NULL)");
            table.HasCheckConstraint("CK_OrderCorrections_Number", "([revision]>(0) AND [version]>(0))");
            table.HasCheckConstraint("CK_OrderCorrections_Rejection", "([state]<>'rejected' OR [review_reason] IS NOT NULL AND len(ltrim(rtrim([review_reason])))>=(2))");
            table.HasCheckConstraint("CK_OrderCorrections_Request", "(len(ltrim(rtrim([request_text])))>=(2))");
            table.HasCheckConstraint("CK_OrderCorrections_RequestActor", "([is_legacy]=(1) OR [requested_by_admin] IS NOT NULL AND [requested_at] IS NOT NULL)");
            table.HasCheckConstraint("CK_OrderCorrections_RequiredPdf", "([requires_new_pdf]=(0) OR NOT ([state]='rejected' OR [state]='approved' OR [state]='submitted') OR [file_id] IS NOT NULL)");
            table.HasCheckConstraint("CK_OrderCorrections_Review", "(NOT ([state]='rejected' OR [state]='approved') OR [reviewed_at] IS NOT NULL AND [reviewed_by_admin] IS NOT NULL)");
            table.HasCheckConstraint("CK_OrderCorrections_State", "(([state]='submitted' OR [state]='requested') AND [is_active]=(1) OR ([state]='superseded' OR [state]='rejected' OR [state]='approved') AND [is_active]=(0))");
            table.HasCheckConstraint("CK_OrderCorrections_Submission", "(NOT ([state]='rejected' OR [state]='approved' OR [state]='submitted') OR [submitted_at] IS NOT NULL AND [submitted_by_customer] IS NOT NULL AND [submission_text] IS NOT NULL AND len(ltrim(rtrim([submission_text])))>=(2))");
        });
        model.Entity<OrderNote>().ToTable("Order_Notes", table =>
        {
            table.HasCheckConstraint("CK_OrderNotes_NoteLength", "(datalength([note])<=(10000))");
        });
        model.Entity<OrderQuote>().ToTable("Order_Quotes", table =>
        {
            table.HasCheckConstraint("CK_OrderQuotes_Acceptance", "([state]='accepted' AND [accepted_at] IS NOT NULL AND [accepted_by_customer] IS NOT NULL AND [decided_at] IS NOT NULL OR [state]<>'accepted' AND [accepted_at] IS NULL AND [accepted_by_customer] IS NULL)");
            table.HasCheckConstraint("CK_OrderQuotes_Amount", "([amount_toman]>=(0) AND [amount_toman]<=(100000000000.))");
            table.HasCheckConstraint("CK_OrderQuotes_Revision", "([revision]>(0) AND [version]>(0))");
            table.HasCheckConstraint("CK_OrderQuotes_Scope", "(len([scope_text])>(0))");
            table.HasCheckConstraint("CK_OrderQuotes_State", "([state]='superseded' OR [state]='declined' OR [state]='revision_requested' OR [state]='accepted' OR [state]='sent')");
        });
        model.Entity<OrderView>().ToTable("Order_Views", table =>
        {
            table.HasCheckConstraint("CK_OrderViews_Recipient", "([recipient_kind]='customer' AND [customer_id] IS NOT NULL AND [admin_email] IS NULL OR [recipient_kind]='admin' AND [admin_email] IS NOT NULL AND [customer_id] IS NULL)");
            table.HasCheckConstraint("CK_OrderViews_Sequence", "([seen_through_sequence]>=(0))");
        });
        model.Entity<Order>().ToTable("Orders", table =>
        {
            table.HasCheckConstraint("CK_OrderEstimate", "([estimate]>=(0))");
            table.HasCheckConstraint("CK_Orders_Workflow", "([workflow_version]=(2) OR [workflow_version]=(1))");
            table.HasCheckConstraint("CK_OrderSpecJson", "(isjson([spec])=(1))");
        });
        model.Entity<TicketMessage>().ToTable("Ticket_Messages", table =>
        {
            table.HasCheckConstraint("CK_TicketActor", "([actor]='admin' OR [actor]='customer')");
        });
        model.Entity<Ticket>().ToTable("Tickets", table =>
        {
            table.HasCheckConstraint("CK_TicketStatus", "([status]='closed' OR [status]='awaiting_customer' OR [status]='open')");
        });
    }
}
