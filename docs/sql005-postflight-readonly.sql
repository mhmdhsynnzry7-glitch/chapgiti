-- PROPOSED POST-MIGRATION VERIFICATION ONLY. NOT EXECUTED BY THE PREFLIGHT TASK.
-- Run only after a separately authorized migration, on the revalidated target.
-- All statements are SELECT-only; no customer content or credentials are returned.

SELECT DB_NAME() database_name,
    LEFT(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(nvarchar(256),SERVERPROPERTY('ServerName'))),2),12) server_alias;

SELECT name,checksum,applied_at,
    CASE WHEN name='005_order_support.sql' AND checksum='c506c962b18edfafe2b5acf777c12d4b6cba7699f2b6dad0371b6ae5646cba0f'
      THEN 'matches reviewed 005' ELSE 'compare with frozen source inventory' END verification
FROM dbo.Schema_Migrations ORDER BY name;

SELECT OBJECT_NAME(object_id) table_name,name,TYPE_NAME(user_type_id) sql_type,
    max_length,scale,is_nullable,OBJECT_DEFINITION(default_object_id) default_definition
FROM sys.columns
WHERE (object_id=OBJECT_ID('dbo.Tickets') AND name='order_id')
   OR object_id=OBJECT_ID('dbo.Order_Notes')
   OR (object_id=OBJECT_ID('dbo.Admin_Users') AND name IN('login_email','effective_email'))
ORDER BY table_name,column_id;

SELECT name,OBJECT_NAME(parent_object_id) table_name,OBJECT_NAME(referenced_object_id) target_table,
    is_disabled,is_not_trusted,delete_referential_action_desc
FROM sys.foreign_keys
WHERE parent_object_id IN(OBJECT_ID('dbo.Tickets'),OBJECT_ID('dbo.Order_Notes'));

SELECT name,definition,is_disabled,is_not_trusted
FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.Order_Notes');

SELECT OBJECT_NAME(i.object_id) table_name,i.name,i.is_disabled,
    COL_NAME(ic.object_id,ic.column_id) column_name,ic.key_ordinal,ic.is_descending_key
FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
WHERE (i.object_id=OBJECT_ID('dbo.Tickets') AND i.name='IX_Tickets_Order')
   OR (i.object_id=OBJECT_ID('dbo.Order_Notes') AND i.name='IX_OrderNotes_Order')
   OR (i.object_id=OBJECT_ID('dbo.Admin_Users') AND i.name='UX_AdminUsers_EffectiveEmail')
ORDER BY table_name,i.name,ic.key_ordinal;

-- Compare historical counts only while application writes remain controlled.
SELECT (SELECT COUNT_BIG(*) FROM dbo.Orders) orders,
    (SELECT COUNT_BIG(*) FROM dbo.Tickets) tickets,
    (SELECT COUNT_BIG(*) FROM dbo.Ticket_Messages) ticket_messages,
    (SELECT COUNT_BIG(*) FROM dbo.Order_Notes) order_notes,
    (SELECT COUNT_BIG(*) FROM dbo.Tickets WHERE order_id IS NOT NULL) linked_tickets,
    (SELECT COUNT_BIG(*) FROM dbo.Orders WHERE NULLIF(admin_notes,N'') IS NOT NULL) orders_with_legacy_notes;

SELECT
    (SELECT COUNT_BIG(*) FROM dbo.Tickets t LEFT JOIN dbo.Orders o ON o.id=t.order_id
      WHERE t.order_id IS NOT NULL AND o.id IS NULL) orphan_ticket_order_links,
    (SELECT COUNT_BIG(*) FROM dbo.Tickets t JOIN dbo.Orders o ON o.id=t.order_id
      WHERE o.customer_id IS NULL OR o.customer_id<>t.customer_id) inconsistent_order_owners,
    (SELECT COUNT_BIG(*) FROM dbo.Order_Notes n LEFT JOIN dbo.Orders o ON o.id=n.order_id
      WHERE o.id IS NULL) orphan_order_notes;
