-- Additive in-app projection. No historical backfill, device tokens or push transports.
CREATE TABLE dbo.Notifications (
    id varchar(36) NOT NULL CONSTRAINT PK_Notifications PRIMARY KEY,
    sequence bigint IDENTITY(1,1) NOT NULL,
    recipient_kind varchar(8) NOT NULL,
    customer_id varchar(36) NULL CONSTRAINT FK_Notifications_Customer REFERENCES dbo.Customer_Registrations(id),
    admin_email nvarchar(254) NULL CONSTRAINT FK_Notifications_Admin REFERENCES dbo.Admin_Users(email),
    event_type varchar(48) NOT NULL,
    entity_kind varchar(8) NOT NULL,
    entity_id varchar(36) NOT NULL,
    title nvarchar(120) NOT NULL,
    body nvarchar(500) NOT NULL,
    target_path nvarchar(200) NOT NULL,
    created_at datetime2(3) NOT NULL,
    read_at datetime2(3) NULL,
    dedupe_key varchar(100) NOT NULL,
    CONSTRAINT CK_Notifications_Recipient CHECK (
        (recipient_kind='customer' AND customer_id IS NOT NULL AND admin_email IS NULL) OR
        (recipient_kind='admin' AND admin_email IS NOT NULL AND customer_id IS NULL)),
    CONSTRAINT CK_Notifications_Entity CHECK (entity_kind IN ('order','ticket') AND TRY_CONVERT(uniqueidentifier,entity_id) IS NOT NULL),
    CONSTRAINT CK_Notifications_Target CHECK (target_path = CASE
        WHEN recipient_kind='customer' AND entity_kind='order' THEN N'/account/orders/'
        WHEN recipient_kind='admin' AND entity_kind='order' THEN N'/admin/orders/'
        WHEN recipient_kind='customer' AND entity_kind='ticket' THEN N'/tickets/'
        WHEN recipient_kind='admin' AND entity_kind='ticket' THEN N'/admin/tickets/' END + entity_id),
    CONSTRAINT CK_Notifications_ReadTime CHECK (read_at IS NULL OR read_at>=created_at)
);
CREATE UNIQUE INDEX UX_Notifications_CustomerEvent ON dbo.Notifications(customer_id,dedupe_key) WHERE customer_id IS NOT NULL;
CREATE UNIQUE INDEX UX_Notifications_AdminEvent ON dbo.Notifications(admin_email,dedupe_key) WHERE admin_email IS NOT NULL;
CREATE INDEX IX_Notifications_CustomerTime ON dbo.Notifications(customer_id,sequence DESC) INCLUDE (read_at) WHERE customer_id IS NOT NULL;
CREATE INDEX IX_Notifications_AdminTime ON dbo.Notifications(admin_email,sequence DESC) INCLUDE (read_at) WHERE admin_email IS NOT NULL;
CREATE INDEX IX_Notifications_CustomerUnread ON dbo.Notifications(customer_id,sequence DESC) WHERE customer_id IS NOT NULL AND read_at IS NULL;
CREATE INDEX IX_Notifications_AdminUnread ON dbo.Notifications(admin_email,sequence DESC) WHERE admin_email IS NOT NULL AND read_at IS NULL;
