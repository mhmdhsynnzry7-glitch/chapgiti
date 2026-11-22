-- Additive source only. Requires a separately authorized, backed-up rollout.
-- Keep Admin_Users.email as the immutable historical key referenced by existing rows.
-- No account, credential, role, session or customer data is rewritten or removed.
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
ALTER TABLE dbo.Admin_Users ADD login_email nvarchar(254) NULL;
-- Separate compilation steps: the migration runner executes one batch without GO.
EXEC(N'ALTER TABLE dbo.Admin_Users ADD effective_email AS ISNULL(login_email,email) PERSISTED');
EXEC(N'CREATE UNIQUE INDEX UX_AdminUsers_EffectiveEmail ON dbo.Admin_Users(effective_email)');
