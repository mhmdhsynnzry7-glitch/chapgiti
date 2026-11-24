import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {mkdtempSync,rmSync,realpathSync,readFileSync,existsSync,statSync} from 'node:fs';
import {tmpdir} from 'node:os';
import path from 'node:path';
import {environment} from '../scripts/dotnet-runtime.mjs';
let checks=0;const check=(value,label)=>{assert.ok(value,label);checks++;console.log('PASS '+label);};
const readonlyLog='.qa/readonly-database-check.log',before=existsSync(readonlyLog)?statSync(readonlyLog).mtimeMs:null;
for(const entry of ['scripts/test-readonly.mjs','tests/customer-accounts.mjs']){
 const rejected=spawnSync(process.execPath,[entry],{encoding:'utf8',windowsHide:true});
 check(rejected.status!==0&&rejected.stderr.includes('Configured database access is disabled'),'legacy configured-database entry point fails before loading configuration or SQL: '+entry);
}
check((existsSync(readonlyLog)?statSync(readonlyLog).mtimeMs:null)===before,'rejected legacy checks do not start a configured backend or alter its log');
const provisioning=spawnSync(process.execPath,['scripts/admin-setup.mjs'],{encoding:'utf8',windowsHide:true});
check(provisioning.status!==0&&provisioning.stderr.includes('Provide the actual administrator email explicitly'),'new-server provisioning cannot silently select the previous installation administrator');
const command=String.raw`$ErrorActionPreference='Stop'; $count=0; foreach($file in Get-ChildItem deploy/iis,deploy/sql -Filter *.ps1){$tokens=$null;$errors=$null;[void][Management.Automation.Language.Parser]::ParseFile($file.FullName,[ref]$tokens,[ref]$errors);if($errors.Count){throw ('Parser failure: '+$file.Name)};$count++}; . ./deploy/iis/service-config.ps1; $items=@(@('C:\Giti\app','C:\Giti\data','GitiNode'),@('C:\Old & Custom\app','C:\Old & Custom\data','NaghshNode')); foreach($item in $items){[xml]$xml=New-GitiServiceConfiguration -AppRoot $item[0] -DataDirectory $item[1] -NodePath 'C:\Program Files\nodejs\node.exe' -ServiceName $item[2];if($xml.service.id -ne $item[2] -or $xml.service.name -ne $item[2] -or $xml.service.workingdirectory -ne $item[0] -or $xml.service.arguments -ne ('"'+(Join-Path $item[0] 'scripts\start-node.mjs')+'"') -or $xml.service.serviceaccount.user -ne 'LocalService' -or ($xml.service.env|Where-Object name -eq DATA_DIR).value -ne $item[1]){throw 'Service XML inconsistent'}}; Write-Output $count`;
const parsed=spawnSync('powershell.exe',['-NoProfile','-NonInteractive','-Command',command],{encoding:'utf8',windowsHide:true});
check(parsed.status===0,'deployment PowerShell parses; generated GitiNode and customized legacy XML agree on identity, arguments, storage and working directory');
check(Number(parsed.stdout.trim())>=11,'all existing installation and maintenance PowerShell files were parsed without executing them');
const upgradeGuide = readFileSync('docs/UPGRADE-CUT-TARIFFS-FA.md', 'utf8');
const blocks = [...upgradeGuide.matchAll(/^```powershell[ \t]*\r?\n([\s\S]*?)^```[ \t]*\r?$/gm)].map(match => match[1].trim());
const parseGuide = String.raw`
  $ErrorActionPreference = 'Stop'
  [Console]::InputEncoding = [Text.UTF8Encoding]::new($false)
  $blocks = ConvertFrom-Json -InputObject ([Console]::In.ReadToEnd())
  foreach ($block in $blocks) {
    $tokens = $null
    $errors = $null
    [void][Management.Automation.Language.Parser]::ParseInput($block, [ref]$tokens, [ref]$errors)
    if ($errors.Count) { throw $errors[0].Message }
  }
`;
const guideSyntax = spawnSync('powershell.exe', ['-NoProfile','-NonInteractive','-Command',parseGuide],
  {input:JSON.stringify(blocks),encoding:'utf8',windowsHide:true});
check(blocks.length > 0 && guideSyntax.status === 0, 'every PowerShell block in the upgrade guide parses without execution');
check(new Set(blocks).size === blocks.length, 'the manual upgrade sequence contains no duplicated PowerShell blocks');
const shaGuard = "$expectedCommit -notmatch '^[0-9a-fA-F]{40}$'";
check(blocks.filter(block => block.includes(shaGuard) && block.includes('$manifest.source.gitRevision -ne $expectedCommit')).length === 1,
  'the manual upgrade verifies the supplied full SHA against the release manifest');
const rendered=spawnSync('powershell.exe',['-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File','tests/iis-routing.ps1'],{encoding:'utf8',windowsHide:true});
assert.equal(rendered.status,0,rendered.stderr);
for(const line of rendered.stdout.trim().split(/\r?\n/)){check(line.startsWith('PASS '),line.slice(5));}
const entry=readFileSync('deploy/iis/configure-iis.ps1','utf8');
check(entry.includes('New-GitiProxyConfiguration -TemplatePath')&&entry.includes('/preserveHostHeader:true'),'installer renders the tested template and preserves the original Host');
const hubs=readFileSync('backend/ChapGiti.Api/Http/ApiRegistration.cs','utf8'), client=readFileSync('frontend/lib/realtime-client.ts','utf8');
for(const [hub,kind] of [['NotificationsHub','notifications'],['SupportHub','support']])check(hubs.includes('MapHub<'+hub+'>("/api/realtime/" + scope + "/'+kind+'").RequireAuthorization')&&client.includes('"'+kind+'"'),'authorized backend/client route agrees: '+kind);
const sqlScript=readFileSync('deploy/sql/acceptance-2025.sql','utf8');
check(!/\b(?:INSERT|UPDATE|DELETE|DROP|TRUNCATE|ALTER|MERGE|CREATE)\b/i.test(sqlScript),'target acceptance queries are read-only');
for(const token of ["SERVERPROPERTY('ServerName')","'ProductMajorVersion'","'EngineEdition'","compatibility_level=170","dbo.Schema_Migrations","dbo.__EFMigrationsHistory","dbo.Print_Catalog_Options","dbo.Articles","dbo.Order_Quotes","dbo.Ticket_Messages","dbo.Cut_Tariff_Versions","is_proposed","CK_CutTariff_Rates","CK_CutTariff_Version","IX_TicketMessages_Unread","OPENJSON(t.rates)"]){
  check(sqlScript.includes(token),'guard or post-migration projection present: '+token);
}
const deployEnv=readFileSync('deploy/iis/production.env.example','utf8');
for(const line of ['SITE_URL=https://chapgiti.ir','DATA_DIR=C:/Giti/data','TRUST_IIS_PROXY=true','DOTNET_API_URL=http://127.0.0.1:5100','SQLSERVER_CONNECTION_STRING=','MELIPAYAMAK_CONSOLE_API_KEY=']){
  check(deployEnv.split(/\r?\n/).includes(line),'Giti production configuration template: '+line.split('=')[0]);
}
const previousCwd=process.cwd(),previousEnv={...process.env},tempRoot=realpathSync(tmpdir()),scratch=mkdtempSync(path.join(tempRoot,'giti-release-config-'));
try{
 process.chdir(scratch);process.env.SQLSERVER_CONNECTION_STRING="Server=127.0.0.1,1;Database=ContractOnly;Integrated Security=true";delete process.env.AllowedHosts;process.env.SITE_URL='https://giti-installation.invalid';process.env.DOTNET_API_URL='http://127.0.0.1:5100';
 const env=environment(false);
 check(env.AllowedHosts.split(';').includes('giti-installation.invalid')&&!env.AllowedHosts.includes('*'),'runtime permits the configured public Host for direct SignalR without a wildcard');
 check(env.AllowedHosts.includes('127.0.0.1')&&env.AllowedHosts.includes('localhost'),'runtime keeps internal loopback health/proxy hosts available');
 process.env.AllowedHosts='explicit-installation.invalid';
 check(environment(false).AllowedHosts==='explicit-installation.invalid','an explicit existing AllowedHosts configuration is preserved');
}finally{process.chdir(previousCwd);for(const key of Object.keys(process.env))if(!(key in previousEnv))delete process.env[key];Object.assign(process.env,previousEnv);assert.equal(path.dirname(realpathSync(scratch)),tempRoot);rmSync(scratch,{recursive:true,force:true});}
console.log(`${checks} deployment configuration checks passed; no service, IIS, database or SMS was accessed.`);
