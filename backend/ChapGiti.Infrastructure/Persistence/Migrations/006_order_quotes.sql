-- Existing rows remain legacy. Only future inserts use the owner-approval workflow.
ALTER TABLE dbo.Orders ADD workflow_version tinyint NOT NULL CONSTRAINT DF_Orders_Workflow_Legacy DEFAULT 1 WITH VALUES;
ALTER TABLE dbo.Orders DROP CONSTRAINT DF_Orders_Workflow_Legacy;
EXEC(N'ALTER TABLE dbo.Orders ADD CONSTRAINT DF_Orders_Workflow DEFAULT 2 FOR workflow_version;');
EXEC(N'ALTER TABLE dbo.Orders ADD CONSTRAINT CK_Orders_Workflow CHECK(workflow_version IN(1,2));');
ALTER TABLE dbo.Orders ADD information_request nvarchar(900) NULL, information_requires_file bit NOT NULL CONSTRAINT DF_Orders_InfoFile DEFAULT 0;
ALTER TABLE dbo.Orders ALTER COLUMN status varchar(40) NOT NULL;
ALTER TABLE dbo.Events ALTER COLUMN status varchar(40) NOT NULL;
ALTER TABLE dbo.Sms_Logs ALTER COLUMN status varchar(40) NOT NULL;

CREATE TABLE dbo.Order_Quotes (
    id varchar(36) NOT NULL PRIMARY KEY,
    order_id varchar(36) NOT NULL REFERENCES dbo.Orders(id),
    revision int NOT NULL,
    amount_toman bigint NOT NULL,
    scope_text nvarchar(3000) NOT NULL,
    delivery_terms nvarchar(2000) NOT NULL,
    exclusions_text nvarchar(2000) NOT NULL,
    created_by_admin nvarchar(254) NOT NULL REFERENCES dbo.Admin_Users(email),
    sent_at datetime2(3) NOT NULL,
    state varchar(24) NOT NULL,
    accepted_at datetime2(3) NULL,
    accepted_by_customer varchar(36) NULL REFERENCES dbo.Customer_Registrations(id),
    decided_at datetime2(3) NULL,
    decision_reason nvarchar(900) NOT NULL DEFAULT '',
    version int NOT NULL DEFAULT 1,
    CONSTRAINT UQ_OrderQuotes_Revision UNIQUE(order_id,revision),
    CONSTRAINT CK_OrderQuotes_Revision CHECK(revision>0 AND version>0),
    CONSTRAINT CK_OrderQuotes_Amount CHECK(amount_toman>=0 AND amount_toman<=100000000000),
    CONSTRAINT CK_OrderQuotes_Scope CHECK(LEN(scope_text)>0),
    CONSTRAINT CK_OrderQuotes_State CHECK(state IN('sent','accepted','revision_requested','declined','superseded')),
    CONSTRAINT CK_OrderQuotes_Acceptance CHECK(
        (state='accepted' AND accepted_at IS NOT NULL AND accepted_by_customer IS NOT NULL AND decided_at IS NOT NULL)
        OR (state<>'accepted' AND accepted_at IS NULL AND accepted_by_customer IS NULL))
);
CREATE UNIQUE INDEX UX_OrderQuotes_Active ON dbo.Order_Quotes(order_id) WHERE state='sent';
CREATE UNIQUE INDEX UX_OrderQuotes_Accepted ON dbo.Order_Quotes(order_id) WHERE state='accepted';
CREATE INDEX IX_OrderQuotes_History ON dbo.Order_Quotes(order_id,revision DESC);

-- Actor/payload-bound receipts make retrying committed commands safe.
CREATE TABLE dbo.Order_Commands (
    id varchar(36) NOT NULL PRIMARY KEY,
    order_id varchar(36) NOT NULL REFERENCES dbo.Orders(id),
    actor nvarchar(280) NOT NULL,
    kind varchar(32) NOT NULL,
    payload_hash char(64) NOT NULL,
    created_at datetime2(3) NOT NULL
);
CREATE INDEX IX_OrderCommands_Order ON dbo.Order_Commands(order_id,created_at);
