-- PREPARED ONLY. Apply during a separately authorized, backed-up rollout.
-- Existing notes, tickets, identities and prices remain untouched.
ALTER TABLE dbo.Tickets ADD order_id varchar(36) NULL;
ALTER TABLE dbo.Tickets ADD CONSTRAINT FK_Tickets_Order FOREIGN KEY(order_id) REFERENCES dbo.Orders(id);
CREATE INDEX IX_Tickets_Order ON dbo.Tickets(order_id,updated_at DESC);

CREATE TABLE dbo.Order_Notes (
    id varchar(36) NOT NULL PRIMARY KEY,
    order_id varchar(36) NOT NULL REFERENCES dbo.Orders(id),
    -- nvarchar(n) supports at most 4000 UTF-16 code units. Match the API's
    -- 5000-unit limit using MAX plus a byte-length CHECK (including trailing spaces).
    note nvarchar(max) NOT NULL CONSTRAINT CK_OrderNotes_NoteLength CHECK(DATALENGTH(note)<=10000),
    actor nvarchar(254) NOT NULL,
    created_at datetime2(3) NOT NULL
);
CREATE INDEX IX_OrderNotes_Order ON dbo.Order_Notes(order_id,created_at DESC);
-- No historical backfill: the old mutable admin_notes has no reliable author/time.
