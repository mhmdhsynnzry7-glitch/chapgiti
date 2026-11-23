import assert from 'node:assert/strict';
import {readFileSync,readdirSync} from 'node:fs';
const root='backend/ChapGiti.Infrastructure/Persistence/';
const migration=readFileSync(root+'Migrations/008_order_corrections.sql','utf8');
const views=readFileSync(root+'Migrations/009_order_views.sql','utf8');
let checks=0;const check=(ok,label)=>{assert.ok(ok,label);checks++;console.log('PASS '+label);};
const files=readdirSync(root+'Migrations').filter(f=>/^\d{3}_.+\.sql$/.test(f)).sort();
check(files.at(-2)==='008_order_corrections.sql'&&files.at(-1)==='009_order_views.sql','new migrations follow the existing 001–007 sequence');
check(/CREATE TABLE dbo\.Order_Corrections/.test(migration)&&/UNIQUE\(order_id,revision\)/.test(migration),'correction history has unique per-order revision numbers');
check(/WHERE is_active=1/.test(migration)&&/state IN\('requested','submitted'\)/.test(migration),'only one correction can remain pending for an order');
for(const column of ['base_file_id','file_id','approved_file_id'])check(new RegExp('FOREIGN KEY\\(order_id,'+column+'\\) REFERENCES dbo.Files\\(order_id,id\\)').test(migration),'cross-order '+column+' references are constrained');
check(/FOREIGN KEY\(order_id,correction_id\) REFERENCES dbo\.Order_Corrections\(order_id,id\)/.test(migration),'attachment correction references preserve order association');
check(/FOREIGN KEY\(id,approved_file_id\) REFERENCES dbo\.Files\(order_id,id\)/.test(migration),'production attachment must belong to the same order');
check(/file_version IS NULL OR file_version>0/.test(migration)&&/UX_Files_OrderVersion/.test(migration),'new versions are positive and unique while unknown historical versions remain null');
check(/submitted_by_customer IS NOT NULL AND submission_text IS NOT NULL/.test(migration)&&/reviewed_by_admin IS NOT NULL/.test(migration),'submitted/reviewed corrections require recorded actors and evidence');
check(/review_reason IS NOT NULL AND LEN/.test(migration),'SQL null semantics cannot bypass the rejection-reason constraint');
check(/requires_new_pdf=0 OR state NOT IN/.test(migration)&&/OR file_id IS NOT NULL/.test(migration),'required correction documents are constrained at submission');
check(/is_legacy=1 OR/.test(migration)&&/requires_new_pdf,is_legacy,base_file_id/.test(migration),'legacy pending requests do not acquire fabricated authors or timestamps');
check(/DROP INDEX UX_OrderQuotes_Accepted/.test(migration)&&/IX_OrderQuotes_AcceptedHistory/.test(migration)&&!/UPDATE dbo\.Order_Quotes/.test(migration),'new quote acceptance can preserve earlier accepted revisions unchanged');
check(!/\b(?:DELETE|TRUNCATE|DROP TABLE)\b/i.test(migration+views),'new migrations do not remove historical business records or tables');
check(/recipient_kind='customer' AND customer_id IS NOT NULL AND admin_email IS NULL/.test(views)&&/recipient_kind='admin' AND admin_email IS NOT NULL AND customer_id IS NULL/.test(views),'view state requires exactly one correctly typed recipient');
check(/UX_OrderViews_Customer/.test(views)&&/UX_OrderViews_Admin/.test(views),'view state has independent customer/admin unique indexes');
check(/seen_through_sequence>=0/.test(views)&&/MAX\(sequence\)/.test(views)&&/order_seen_baseline_sequence/.test(views),'activation baseline avoids manufactured historical views');
check(!/UPDATE\s+dbo\.Notifications/i.test(views)&&!/read_at/.test(views.replace(/^--.*$/gm,'')),'order-view migration never reuses or rewrites notification read state');
const runner=readFileSync(root+'Migrations.cs','utf8');
check(/SHA256/.test(runner)&&/Transaction/.test(runner)&&/sp_getapplock/.test(runner),'existing migration runner retains checksums, transaction and locking');
const repository=readFileSync(root+'Repositories/OrderViewRepository.cs','utf8');
check(/GROUP BY n.entity_id/.test(repository)&&/o.customer_id=@p0/.test(repository),'unseen counts are distinct owned orders, not accumulated notification rows');
check(/UPDLOCK,HOLDLOCK/.test(repository)&&/seen_through_sequence.*>=through/s.test(repository),'view acknowledgement is locked and monotonic');
const tariffId = '20261122130922_CutTariffVersions';
const efMigrations = root + 'Ef/Migrations/';
const tariffMigration = readFileSync(efMigrations + tariffId + '.cs', 'utf8');
const [up, down] = tariffMigration.split('protected override void Down');
check(down && !/Drop(?:Table|Column|Index)|(?:Insert|Delete|Update)Data|migrationBuilder\.Sql\(/.test(up),
  'activating cut tariffs never deletes tables or rewrites existing order prices, rates or ticket data');
check(up.includes('x => new { x.cut_id, x.version }') && up.includes('principalTable: "Print_Catalog_Options"') && up.includes('ReferentialAction.Restrict'),
  'cut rate versions have a unique composite primary key and preserve the referenced catalog option');
check(up.includes('ISJSON([rates]) = 1') && up.includes('[version] >= 1') && up.includes('datetime2(3)'),
  'each tariff revision requires a positive version, valid JSON and a precision-preserving timestamp');
check(up.includes('table: "Ticket_Messages"') && up.includes('new[] { "actor", "ticket_id" }') && up.includes('filter: "[read_at] IS NULL"'),
  'unread support counts are served by a filtered index over actor and ticket');
check(/DropTable\(\s*name: "Cut_Tariff_Versions"/.test(down) && /DropIndex\(\s*name: "IX_TicketMessages_Unread"/.test(down) && !/DropColumn|DeleteData/.test(down),
  'rolling back the migration affects only its new tariff history and support index');
check(readFileSync(efMigrations + tariffId + '.Designer.cs','utf8').includes('[Migration("' + tariffId + '")]') && readFileSync(efMigrations + 'ChapGitiDbContextModelSnapshot.cs','utf8').includes('IX_TicketMessages_Unread'),
  'EF migration identity, designer and snapshot describe the same change');
const proposalId = '20261123062444_ProposedCutTariffs';
const [seed, rollback] = readFileSync(efMigrations + proposalId + '.cs', 'utf8').split('protected override void Down');
check(seed.includes('name: "is_proposed"') && seed.includes('defaultValue: false'), 'previous administrator versions retain their approved status');
check(seed.includes("d.isCustom = 0") && seed.includes("o.active = 1") && seed.includes("NOT EXISTS (SELECT 1 FROM dbo.Cut_Tariff_Versions"), 'only missing fixed-cut histories receive a proposal');
check(!/\b(?:DROP|TRUNCATE|DELETE|UPDATE|MERGE)\b/i.test(seed), 'proposal activation preserves prior orders, settings and revisions');
check(rollback.includes('WHERE is_proposed = 1') && rollback.indexOf('THROW') < rollback.indexOf('DropColumn'), 'downgrade cannot accidentally activate proposed prices');
console.log(`${checks} migration/source contract checks passed; this source-only suite does not access SQL Server. Run the separately authorized isolated SQL acceptance for engine verification.`);
