import {constants, copyFileSync, cpSync, existsSync, mkdirSync, mkdtempSync, readFileSync, realpathSync, renameSync, rmSync, statSync, writeFileSync} from "node:fs";
import path from "node:path";
import {spawnSync} from "node:child_process";

import {allowedReleaseFile, fileHashes, migrationInventory, scanPackage, configuredSecrets, sha256, verifyArchive} from "./release-files.mjs";
import {embedded, guides, iis, inspectCompiled, matchMetadata, scripts, sourceMigrations} from "./release-contract.mjs";
import {inputDigest, requireBuildReceipt, sourceInventory} from "./release-source.mjs";
import {verifyDirectory} from "./verify-release.mjs";

const root = process.cwd();
if (process.platform !== "win32") throw Error("The IIS release targets Windows x64.");
const args = process.argv.slice(2).filter(arg => arg !== "--release-only");
if (args.length && (args.length !== 2 || args[0] !== "--verification")) throw Error("Usage: npm run package:iis -- [--release-only] [--verification <report.json>]");
const readJson = file => JSON.parse(readFileSync(file,"utf8"));
for(const name of ["frontend/publish/server.js", "backend/publish/ChapGiti.Api.exe"])
  if(!existsSync(name)) throw Error("Missing published output: " + name);
const frontendVersions = Object.fromEntries(["next","@next/env","react","react-dom","zod"].map(name=>[name,readJson(path.join("frontend/node_modules",name,"package.json")).version]));
if (readJson("frontend/publish/node_modules/next/package.json").version !== frontendVersions.next ||
    readFileSync("frontend/publish/.next/BUILD_ID","utf8") !== readFileSync("frontend/.next/BUILD_ID","utf8"))
  throw Error("Frontend publish is stale. Rebuild standalone output.");
for(const role of ["frontend","backend"]) requireBuildReceipt(root,role,role+"/publish");
const sourceSha256 = inputDigest(root);
const evidence = readJson(args[1] || ".qa/release-contracts.json");
if(evidence.status !== "PASS" || evidence.sourceSha256 !== sourceSha256 || evidence.database !== "NOT_ACCESSED" ||
   !["workflow","packaging","iis","proxy"].every(key=>Number.isSafeInteger(evidence.checks?.[key]) && evidence.checks[key]>0))
  throw Error("Missing or stale offline verification. Run npm run test:release-contracts on the current source.");
const expected = sourceMigrations(root);
const migrations = migrationInventory("backend/ChapGiti.Infrastructure/Persistence/Migrations");
function git(...args) {
  const child=spawnSync("git",args,{encoding:"utf8",windowsHide:true});
  if(child.status!==0) throw Error("Cannot identify Git source.");
  return child.stdout.trim();
}
const revision=git("rev-parse","HEAD"), branch=git("branch","--show-current"), dirty=!!git("status","--porcelain","--untracked-files=normal");
const now=new Date();
const clock=Object.fromEntries(new Intl.DateTimeFormat("en-CA",{timeZone:"Asia/Tehran",year:"numeric",month:"2-digit",day:"2-digit",hour:"2-digit",minute:"2-digit",second:"2-digit",hourCycle:"h23"}).formatToParts(now).map(p=>[p.type,p.value]));
const stamp=clock.year+"-"+clock.month+"-"+clock.day+"T"+clock.hour+"-"+clock.minute+"-"+clock.second+"-"+String(now.getMilliseconds()).padStart(3,"0")+"-"+revision.slice(0,12)+(dirty?"-dirty":"");
const output=path.resolve("outputs"), basename="chap-giti-win-x64-release-"+stamp, destination=path.join(output,basename+".zip");
if(existsSync(destination) || existsSync(destination+".sha256")) throw Error("Release already exists.");
const qa=realpathSync(mkdirSync(".qa",{recursive:true}) || ".qa");
const working=mkdtempSync(path.join(qa,"release-staging-")), stage=path.join(working,"app");
const write = (name, data) => writeFileSync(path.join(stage,name),JSON.stringify(data,null,2)+"\n");
function copy(from,to=from) {
  mkdirSync(path.dirname(path.join(stage,to)),{recursive:true});
  copyFileSync(from,path.join(stage,to));
}
try {
  for(const [from,to] of [["frontend/publish","frontend"],["backend/publish","backend/publish"]])
    cpSync(from,path.join(stage,to),{recursive:true,dereference:true,filter:file=>allowedReleaseFile(path.relative(root,file))});
  for(const [dir,names] of [["scripts",scripts],["deploy/iis",iis],["docs",guides]])
    for(const name of names) copy(dir+"/"+name);
  copy("deploy/iis/production.env.example",".env.example");
  copy("deploy/sql/acceptance-2025.sql");
  copy("deploy/sql/check-target.ps1");
  for(const name of embedded) copy("backend/ChapGiti.Infrastructure/Persistence/Ef/"+name,"migrations/ef/"+name);
  writeFileSync(path.join(stage,"README.md"),"# چاپ گیتی\n\n[نصب دستی ویندوز](docs/WINDOWS-SERVER-INSTALL-FA.md) · [پذیرش SQL Server و migration](docs/DATABASE-MIGRATION-FA.md)\n");
  write("package.json",{name:"chap-giti-windows-release",private:true,type:"module",engines:{node:">=24.15.0 <25"},scripts:{
    start:"node scripts/start-node.mjs","verify:release":"node scripts/verify-release.mjs","db:setup":"node scripts/db-setup.mjs","admin:setup":"node scripts/admin-setup.mjs"}});
  const compiled=inspectCompiled(path.join(stage,"backend/publish/ChapGiti.Api.exe"));
  matchMetadata(expected,compiled);
  write("MIGRATIONS-CHECKSUMS.json",{algorithm:"SHA256(UTF8(File.ReadAllText, CRLF to LF))",order:migrations,compiled});
  write("SOURCE-INVENTORY.json",sourceInventory(root,revision));
  const scan=scanPackage(stage,await configuredSecrets(root));
  if(!scan.passed) throw Error("Private material detected in staging: "+scan.problems.map(p=>p.file).join(", "));
  const files=fileHashes(stage);
  write("RELEASE-MANIFEST.json",{version:stamp,createdAtUtc:new Date().toISOString(),packageMode:"release-only",displayTimeZone:"Asia/Tehran",source:{gitRevision:revision,branch,dirtyWorkingTree:dirty,sourceSha256},
    runtime:"win-x64 self-contained",frontendVersions,migrations,efMigrations:compiled.ef,latestMigration:compiled.ef.at(-1),files,
    installation:{site:"https://chapgiti.ir",appRoot:"C:\\Giti\\app",dataDirectory:"C:\\Giti\\data",proxyRoot:"C:\\inetpub\\giti-proxy",siteName:"Giti",serviceName:"GitiNode",frontendPort:3000,backendPort:5100,database:"ChapGiti"},
    verification:{scope:"offline package contracts",checks:evidence.checks,realSql:"NOT_RUN",liveIis:"NOT_RUN",sqlServer2025:"NOT_TESTED"}});
  const count=verifyDirectory(stage);
  if(inputDigest(root)!==sourceSha256) throw Error("Source changed during packaging.");
  const zip=path.join(working,"release.zip");
  const q = value=>"'"+value.replaceAll("'","''")+"'";
  const command="$ErrorActionPreference='Stop'; Add-Type -AssemblyName System.IO.Compression.FileSystem; [IO.Compression.ZipFile]::CreateFromDirectory("+q(stage)+","+q(zip)+",[IO.Compression.CompressionLevel]::Optimal,$false)";
  if(spawnSync("powershell.exe",["-NoProfile","-NonInteractive","-Command",command],{windowsHide:true,stdio:"inherit"}).status!==0) throw Error("ZIP compression failed.");
  verifyArchive(zip,"RELEASE-MANIFEST.json");
  const hash=sha256(readFileSync(zip));
  mkdirSync(output,{recursive:true});
  copyFileSync(zip,destination,constants.COPYFILE_EXCL);
  writeFileSync(destination+".sha256",hash+"  "+path.basename(destination)+"\n",{flag:"wx"});
  const archive={type:"release",path:destination,sha256:hash,bytes:statSync(destination).size,files:count+1,integrityVerified:true};
  const pointer=path.join(output,"latest-dotnet-package.json");
  const pending=pointer+"."+stamp+".tmp";
  writeFileSync(pending,JSON.stringify({version:stamp,gitRevision:revision,dirtyWorkingTree:dirty,packageMode:"release-only",archives:[archive],sha256File:destination+".sha256",sourceDirectory:null,latestMigration:compiled.ef.at(-1),migrations},null,2)+"\n",{flag:"wx"});
  renameSync(pending,pointer);
  console.log(JSON.stringify(archive));
} finally {
  const actual=realpathSync(working);
  if(path.dirname(actual)!==qa || !path.basename(actual).startsWith("release-staging-")) throw Error("Unexpected staging directory.");
  rmSync(actual,{recursive:true,force:true});
}
