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
    IF NOT EXISTS (SELECT 1 FROM sys.columns c JOIN sys.default_constraints d ON d.object_id=c.default_object_id
                   WHERE c.object_id=OBJECT_ID('dbo.Cut_Tariff_Versions') AND c.name='is_proposed'
                     AND TYPE_NAME(c.system_type_id)='bit' AND c.is_nullable=0
                     AND LOWER(REPLACE(REPLACE(REPLACE(d.definition,'(',''),')',''),' ','')) IN ('0','convert[bit],0','convertbit,0'))
        THROW 51209, 'Tariff proposal flag or default missing.', 1;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id=OBJECT_ID('dbo.Cut_Tariff_Versions') AND i.is_primary_key=1
                   AND INDEX_COL('dbo.Cut_Tariff_Versions', i.index_id, 1)='cut_id'
                   AND INDEX_COL('dbo.Cut_Tariff_Versions', i.index_id, 2)='version')
        THROW 51210, 'Tariff version primary key missing.', 1;
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys k JOIN sys.foreign_key_columns c ON c.constraint_object_id=k.object_id
                   WHERE k.parent_object_id=OBJECT_ID('dbo.Cut_Tariff_Versions') AND k.referenced_object_id=OBJECT_ID('dbo.Print_Catalog_Options')
                     AND COL_NAME(c.parent_object_id,c.parent_column_id)='cut_id' AND COL_NAME(c.referenced_object_id,c.referenced_column_id)='id'
                     AND k.is_disabled=0 AND k.is_not_trusted=0 AND k.delete_referential_action=0)
        THROW 51211, 'Tariff catalog foreign key missing or untrusted.', 1;
    IF (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.Cut_Tariff_Versions')
        AND name IN ('CK_CutTariff_Rates','CK_CutTariff_Version') AND is_disabled=0 AND is_not_trusted=0) <> 2
        THROW 51212, 'Tariff constraints missing or untrusted.', 1;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id=OBJECT_ID('dbo.Ticket_Messages')
                   AND i.name='IX_TicketMessages_Unread' AND i.is_disabled=0 AND i.has_filter=1
                   AND INDEX_COL('dbo.Ticket_Messages',i.index_id,1)='actor' AND INDEX_COL('dbo.Ticket_Messages',i.index_id,2)='ticket_id'
                   AND i.filter_definition LIKE '%read_at%IS NULL%')
        THROW 51213, 'Unread ticket message index missing or incompatible.', 1;
    EXEC(N'IF EXISTS (SELECT 1 FROM dbo.Print_Catalog_Options o WHERE o.kind=''bookSizes'' AND o.active=1
                 AND JSON_VALUE(o.metadata,''$.isCustom'')=''false'' AND JSON_VALUE(o.metadata,''$.unit'')=''cm''
                 AND NOT EXISTS (SELECT 1 FROM dbo.Cut_Tariff_Versions t WHERE t.cut_id=o.id))
               THROW 51214, ''An eligible fixed cut has no tariff history.'', 1;
           IF EXISTS (SELECT 1 FROM dbo.Cut_Tariff_Versions t WHERE t.is_proposed=1 AND
               (t.version<>1 OR (SELECT COUNT(*) FROM OPENJSON(t.rates))<>11 OR
               (SELECT COUNT(DISTINCT [key]) FROM OPENJSON(t.rates) WHERE [type]=2 AND [key] IN
                 (''bw'',''color'',''cream'',''coated'',''soft'',''hard'',''spiral'',''coverPrint'',''coverPaper'',''lamination'',''setup''))<>11))
               THROW 51215, ''Invalid proposed tariff revision or rate set.'', 1;
           SELECT COUNT_BIG(*) AS proposed_tariff_revisions FROM dbo.Cut_Tariff_Versions WHERE is_proposed=1;');
    EXEC(N'SELECT COUNT_BIG(*) AS catalog_options FROM dbo.Print_Catalog_Options WHERE code IS NOT NULL;
           SELECT COUNT_BIG(*) AS orders_with_approved_pdf FROM dbo.Orders o JOIN dbo.Files f ON f.id=o.approved_file_id AND f.order_id=o.id WHERE o.workflow_version>=1;
           SELECT COUNT_BIG(*) AS quote_revisions FROM dbo.Order_Quotes;
           SELECT COUNT_BIG(*) AS published_articles FROM dbo.Articles WHERE publication_status=''published'' AND published=1;
           SELECT COUNT_BIG(*) AS article_aliases FROM dbo.Article_Slug_Aliases;
           SELECT COUNT_BIG(*) AS unread_messages FROM dbo.Ticket_Messages m JOIN dbo.Tickets t ON m.ticket_id=t.id WHERE m.read_at IS NULL');
END;
