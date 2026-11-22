-- Order attention is independent from Notifications.read_at. Historical records
-- start at a baseline rather than fabricating that somebody viewed them.
CREATE TABLE dbo.Order_Views (
    id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_OrderViews PRIMARY KEY,
    order_id varchar(36) NOT NULL REFERENCES dbo.Orders(id),
    recipient_kind varchar(8) NOT NULL,
    customer_id varchar(36) NULL REFERENCES dbo.Customer_Registrations(id),
    admin_email nvarchar(254) NULL REFERENCES dbo.Admin_Users(email),
    seen_through_sequence bigint NOT NULL,
    seen_at datetime2(3) NOT NULL,
    CONSTRAINT CK_OrderViews_Recipient CHECK(
        (recipient_kind='customer' AND customer_id IS NOT NULL AND admin_email IS NULL) OR
        (recipient_kind='admin' AND admin_email IS NOT NULL AND customer_id IS NULL)),
    CONSTRAINT CK_OrderViews_Sequence CHECK(seen_through_sequence>=0)
);
CREATE UNIQUE INDEX UX_OrderViews_Customer ON dbo.Order_Views(order_id,customer_id) WHERE customer_id IS NOT NULL;
CREATE UNIQUE INDEX UX_OrderViews_Admin ON dbo.Order_Views(order_id,admin_email) WHERE admin_email IS NOT NULL;
CREATE INDEX IX_Notifications_CustomerOrderAttention ON dbo.Notifications(customer_id,entity_id,sequence DESC)
    WHERE customer_id IS NOT NULL AND entity_kind='order';
CREATE INDEX IX_Notifications_AdminOrderAttention ON dbo.Notifications(admin_email,entity_id,sequence DESC)
    WHERE admin_email IS NOT NULL AND entity_kind='order';

IF NOT EXISTS(SELECT 1 FROM dbo.Settings WHERE [key]='order_seen_baseline_sequence')
    INSERT dbo.Settings([key],[value]) SELECT 'order_seen_baseline_sequence',CONVERT(nvarchar(30),COALESCE(MAX(sequence),0)) FROM dbo.Notifications;
IF EXISTS(SELECT 1 FROM dbo.Settings WHERE [key]='order_seen_baseline_sequence' AND (TRY_CONVERT(bigint,[value]) IS NULL OR TRY_CONVERT(bigint,[value])<0))
    THROW 51000,'Invalid existing order-view tracking baseline.',1;
