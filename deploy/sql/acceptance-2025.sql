-- Offline package resource. Read-only acceptance, run manually in the approved server session.
SET NOCOUNT ON;
IF DB_NAME() <> N'ChapGiti' OR CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')) <> @expectedServer
    THROW 51201, 'Unexpected server or catalog.', 1;
IF CONVERT(int,SERVERPROPERTY('ProductMajorVersion')) <> 17 OR CONVERT(int,SERVERPROPERTY('EngineEdition')) <> 4
    THROW 51202, 'SQL Server 2025 Express is required for this acceptance.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE database_id=DB_ID() AND compatibility_level=170 AND state_desc='ONLINE')
    THROW 51203, 'Expected an online database at compatibility level 170.', 1;
IF ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','VIEW DEFINITION'),0) <> 1
    THROW 51204, 'VIEW DEFINITION is required to distinguish a missing object from hidden metadata.', 1;
SELECT SERVERPROPERTY('ServerName') AS server_name, DB_NAME() AS database_name, SERVERPROPERTY('ProductVersion') AS version,
       SERVERPROPERTY('Edition') AS edition, compatibility_level, collation_name
FROM sys.databases WHERE database_id=DB_ID();
IF OBJECT_ID('dbo.Schema_Migrations','U') IS NULL AND EXISTS (SELECT 1 FROM sys.tables WHERE is_ms_shipped=0)
    THROW 51205, 'Existing application tables without a recognized legacy history. Stop for review.', 1;
IF OBJECT_ID('dbo.Schema_Migrations','U') IS NULL SELECT CAST(NULL AS varchar(150)) AS name, CAST(NULL AS char(64)) AS checksum WHERE 1=0;
ELSE EXEC(N'SELECT name,checksum FROM dbo.Schema_Migrations ORDER BY name');
IF OBJECT_ID('dbo.__EFMigrationsHistory','U') IS NULL SELECT CAST(NULL AS nvarchar(150)) AS MigrationId, CAST(NULL AS nvarchar(32)) AS ProductVersion WHERE 1=0;
ELSE EXEC(N'SELECT MigrationId,ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId');
IF @after=1
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Ticket_Messages') AND name='read_at'
                   AND TYPE_NAME(system_type_id)='datetime2' AND scale=3 AND is_nullable=1)
        THROW 51206, 'Ticket read-state metadata does not match the mapped nullable datetime2(3).', 1;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.Print_Catalog_Options') AND name='UX_PrintCatalog_Code' AND is_unique=1 AND is_disabled=0)
        THROW 51207, 'Catalog index missing.', 1;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.Articles') AND name='IX_Articles_PublicList' AND is_disabled=0)
        THROW 51208, 'Magazine index missing.', 1;
    EXEC(N'SELECT COUNT_BIG(*) AS catalog_options FROM dbo.Print_Catalog_Options WHERE code IS NOT NULL;
           SELECT COUNT_BIG(*) AS orders_with_approved_pdf FROM dbo.Orders o JOIN dbo.Files f ON f.id=o.approved_file_id AND f.order_id=o.id WHERE o.workflow_version>=1;
           SELECT COUNT_BIG(*) AS quote_revisions FROM dbo.Order_Quotes;
           SELECT COUNT_BIG(*) AS published_articles FROM dbo.Articles WHERE publication_status=''published'' AND published=1;
           SELECT COUNT_BIG(*) AS article_aliases FROM dbo.Article_Slug_Aliases;
           SELECT COUNT_BIG(*) AS unread_messages FROM dbo.Ticket_Messages m JOIN dbo.Tickets t ON m.ticket_id=t.id WHERE m.read_at IS NULL');
END;
