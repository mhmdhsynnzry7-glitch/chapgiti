import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {spawnSync} from "node:child_process";

// SQL Server engine acceptance uses only session-private temporary tables, even on a shared workstation.
assert.notEqual(process.env.NODE_ENV, "production", "Development test runner required");
const migration = readFileSync(new URL("../backend/ChapGiti.Infrastructure/Persistence/Ef/Migrations/20261123062444_ProposedCutTariffs.cs", import.meta.url), "utf8");
const seed = migration.split('migrationBuilder.Sql("""')[1].split('""");')[0]
  .replaceAll("dbo.Print_Catalog_Options", "#catalog").replaceAll("dbo.Settings", "#settings")
  .replaceAll("dbo.Cut_Tariff_Versions", "#tariffs");
assert.doesNotMatch(seed, /\bdbo\.|\b(?:DROP|UPDATE|DELETE|ALTER|TRUNCATE)\b/i);
const sql = `SET NOCOUNT ON;
IF DB_NAME() <> 'tempdb' OR CONVERT(sysname,SERVERPROPERTY('MachineName')) <> HOST_NAME()
 OR CONNECTIONPROPERTY('net_transport') <> 'Shared memory'
 THROW 51150, 'Local tempdb required.', 1;
CREATE TABLE #catalog(id varchar(36),kind varchar(40),active bit,metadata nvarchar(max));
CREATE TABLE #settings([key] varchar(80),value nvarchar(max));
CREATE TABLE #tariffs(cut_id varchar(36),version int,updated_at datetime2(3),rates nvarchar(max),is_proposed bit,PRIMARY KEY(cut_id,version));
INSERT #catalog VALUES
 ('vaziri','bookSizes',1,'{"width":17,"height":24,"factor":1.2,"isCustom":false,"unit":"cm"}'),
 ('a4','bookSizes',1,'{"width":21,"height":29.7,"factor":1.7,"isCustom":false,"unit":"cm"}'),
 ('existing','bookSizes',1,'{"width":16,"height":19,"factor":1,"isCustom":false,"unit":"cm"}'),
 ('inactive','bookSizes',0,'{"width":16,"height":19,"factor":1,"isCustom":false,"unit":"cm"}'),
 ('custom','bookSizes',1,'{"width":14.5,"height":21.5,"factor":1,"isCustom":true,"unit":"cm"}');
INSERT #settings VALUES ('rates','{"bw":100,"setup":87654}');
INSERT #tariffs VALUES ('existing',7,'2026-01-01','{"existing":1}',0);
GO
${seed}
GO
${seed}
GO
IF (SELECT COUNT(*) FROM #tariffs) <> 3 THROW 51151, 'Seed scope or idempotency failed.', 1;
IF NOT EXISTS(SELECT 1 FROM #tariffs WHERE cut_id='existing' AND version=7 AND is_proposed=0 AND rates='{"existing":1}' AND updated_at='20260101') THROW 51152,'Approved history changed.',1;
IF (SELECT COUNT(*) FROM #tariffs WHERE is_proposed=1 AND version=1 AND JSON_VALUE(rates,'$.setup')=87654)<>2 THROW 51153,'Status or setup mismatch.',1;
IF EXISTS (SELECT 1 FROM #tariffs t WHERE is_proposed=1 AND (SELECT COUNT(*) FROM OPENJSON(t.rates))<>11) THROW 51154,'Incomplete proposals.',1;
IF NOT EXISTS (SELECT 1 FROM #tariffs WHERE cut_id='vaziri' AND JSON_VALUE(rates,'$.bw')=120 AND JSON_VALUE(rates,'$.color')=1380) THROW 51155,'Stored rate or archived fallback not used.',1;
IF NOT EXISTS (SELECT 1 FROM #tariffs WHERE cut_id='a4' AND JSON_VALUE(rates,'$.bw')=170) THROW 51156,'Cut pricing mixed.',1;
IF EXISTS (SELECT 1 FROM #tariffs WHERE cut_id IN ('custom','inactive')) THROW 51157,'Unexpected proposal.',1;
PRINT '7 isolated SQL seed checks passed';`;
const result=spawnSync("sqlcmd",["-S","lpc:.","-E","-d","tempdb","-b","-l","5"],{input:sql+"\nGO\n",encoding:"utf8",windowsHide:true});
if (result.error) throw result.error;
process.stdout.write(result.stdout); process.stderr.write(result.stderr);
assert.equal(result.status,0);
