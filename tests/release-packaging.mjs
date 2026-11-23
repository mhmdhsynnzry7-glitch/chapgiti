import assert from "node:assert/strict";
import {randomBytes} from "node:crypto";
import {isDeepStrictEqual} from "node:util";
import {sealFrontend, initializeFrontend, checkInitializedFrontend, checkSealedFrontend, frameworkManifests} from "../scripts/initialize-frontend.mjs";
import {cpSync,mkdtempSync,mkdirSync,writeFileSync,rmSync,realpathSync,existsSync,readFileSync} from "node:fs";
import {tmpdir} from "node:os";
import path from "node:path";
import {spawn,spawnSync} from "node:child_process";
import {allowedReleaseFile,migrationChecksum,migrationInventory,scanPackage,secretCandidates,sha256,fileHashes} from "../scripts/release-files.mjs";

import {scripts, guides, iis, requireFile, runtimeFiles, sourceMigrations, matchMetadata, embedded, checkLayout, requiredSourcePaths, verifySourceInventory} from "../scripts/release-contract.mjs";
import {verifyDirectory} from "../scripts/verify-release.mjs";
import {inputDigest, requireBuildReceipt, writeBuildReceipt} from "../scripts/release-source.mjs";
let checks = 0;
const check = (ok, name) => {assert.ok(ok, name); checks++;};
for (const file of [".env.local", "nested/.env.production", "backend/appsettings.Local.json", "backend/appsettings.Production.json",
  "data/customer.pdf", "copy/site.sqlite-wal", "database.bak", "customer.mdf", "customer.ldf", "logs/service.log", ".qa/session.json", "private.pem", "deploy/iis/NaghshNode.xml", "deploy/iis/NaghshNode.exe", "frontend/.next/cache/file.json"])
  check(!allowedReleaseFile(file), "private/generated runtime material is excluded");
for (const file of ["frontend/node_modules/next/server.js", "frontend/.next/server/app/page.js", "backend/publish/Data/007_in_app_notifications.sql",
  "backend/publish/ChapGiti.Api.exe", "frontend/public/fonts/vazirmatn.woff2", ".env.example", "deploy/iis/install-service.ps1"])
  check(allowedReleaseFile(file), "required runtime assets and templates remain available");
for (const file of ["frontend/node_modules/file.js", "frontend/.next/server.js", "backend/obj/file.json", "backend/bin/file.dll", "backend/publish/file.dll", "data/private.json", "backend/ChapGiti.Api/Properties/launchSettings.json"])
  check(!allowedReleaseFile(file, {source: true}), "generated/private source directories are excluded");
check(allowedReleaseFile("backend/ChapGiti.Api/appsettings.Development.json", {source: true}), "non-secret development template stays in source");
check(!allowedReleaseFile("backend/appsettings.Development.json"), "development settings are absent from server runtime");
check(allowedReleaseFile(".agents/skills/chap-database/SKILL.md", {source: true}), "explicit project skill remains distributable");
check(migrationChecksum("a\r\nb\r\n") === migrationChecksum("a\nb\n"), "runner checksum preserves CRLF normalization");
check(migrationChecksum("\uFEFFa\n") === migrationChecksum("a\n"), "runner checksum consumes the UTF8 BOM");
check(migrationChecksum("a\n") !== migrationChecksum("a"), "meaningful migration bytes are not trimmed");
const candidates = secretCandidates({MELIPAYAMAK_CONSOLE_API_KEY: "Mock.Api.Key.123", CHAP_UAT_CUSTOMER_PASSWORD: "Mock.Password@1",
  SQLSERVER_CONNECTION_STRING: "Server=.;Database=Sample;Password=Mock.Sql.Secret@1;", OTP_ENABLED: "true", PATH: "ordinary.runtime.path"});
check(candidates.length === 3 && !candidates.includes("true"), "only meaningful configured secret values are selected");
const tempRoot = realpathSync(tmpdir()), scratch = mkdtempSync(path.join(tempRoot, "chap-release-test-"));
try {
  const stagedFrontend = path.join(scratch, "standalone");
  const [props, prerender, actions] = frameworkManifests.map(name => path.join(stagedFrontend, name));
  mkdirSync(path.dirname(props), {recursive:true});
  const preview = Object.fromEntries([["previewModeId",16],["previewModeSigningKey",32],["previewModeEncryptionKey",32]]
    .map(([name,size]) => [name,randomBytes(size).toString("hex")]));
  const actionKey = randomBytes(32).toString("base64");
  const putJson = (file, data) => writeFileSync(file, JSON.stringify(data));
  const readJson = file => JSON.parse(readFileSync(file,"utf8"));
  putJson(props, preview);
  putJson(prerender, {version:4,routes:{},preview});
  putJson(actions, {node:{bound:{}},edge:{},encryptionKey:actionKey});
  assert.throws(()=>sealFrontend(stagedFrontend), /Server Actions/); checks++;
  putJson(actions, {node:{},edge:{},encryptionKey:actionKey});
  const buildKeys=sealFrontend(stagedFrontend);
  check(buildKeys.length===4 && scanPackage(stagedFrontend,buildKeys).passed, "all build-specific framework keys are excluded");
  checkSealedFrontend(stagedFrontend); checks++;
  assert.throws(()=>checkInitializedFrontend(stagedFrontend)); checks++;
  const parallelRoot=path.join(scratch,"parallel");
  mkdirSync(path.join(parallelRoot,"scripts"),{recursive:true});
  cpSync(stagedFrontend,path.join(parallelRoot,"frontend"),{recursive:true});
  putJson(path.join(parallelRoot,"RELEASE-MANIFEST.json"),{});
  for(const name of ["initialize-frontend.mjs","start-node.mjs"])
    cpSync(path.resolve("scripts",name),path.join(parallelRoot,"scripts",name));
  const unprepared=spawnSync(process.execPath,["scripts/start-node.mjs"],{cwd:parallelRoot,encoding:"utf8",windowsHide:true});
  check(unprepared.status!==0 && unprepared.stderr.includes("Initialize the extracted frontend"),"an unprepared release stops before backend startup");
  const parallelInit=()=>new Promise((resolve,reject)=>{
    const child=spawn(process.execPath,["scripts/initialize-frontend.mjs"],{cwd:parallelRoot,stdio:"ignore",windowsHide:true});
    child.once("error",reject);child.once("close",resolve);
  });
  check((await Promise.all([parallelInit(),parallelInit(),parallelInit()])).every(code=>code===0),"concurrent initializers converge on one complete key set");
  checkInitializedFrontend(path.join(parallelRoot,"frontend")); checks++;
  const second=path.join(scratch,"second-install");
  cpSync(stagedFrontend,second,{recursive:true});
  initializeFrontend(stagedFrontend);
  const local=readJson(props);
  check(Object.keys(local).every(k=>local[k]!==preview[k]) && readJson(actions).encryptionKey!==actionKey,"destination generates its own four keys");
  check(isDeepStrictEqual(readJson(prerender).preview,local),"both Next.js preview consumers read the same keys");
  const ready=frameworkManifests.map(name=>sha256(readFileSync(path.join(stagedFrontend,name))));
  initializeFrontend(stagedFrontend);
  check(ready.every((hash,i)=>hash===sha256(readFileSync(path.join(stagedFrontend,frameworkManifests[i])))),"repeated initialization preserves installed keys");
  rmSync(prerender);
  initializeFrontend(stagedFrontend);
  check(isDeepStrictEqual(readJson(prerender).preview,local),"interrupted installation can resume with the stored preview key set");
  initializeFrontend(second);
  check(Object.keys(local).every(k=>readJson(path.join(second,frameworkManifests[0]))[k]!==local[k]),"independent installations have distinct keys");
  const alteredManifest=readJson(prerender);alteredManifest.routes={"/changed":{}};putJson(prerender,alteredManifest);
  assert.throws(()=>checkInitializedFrontend(stagedFrontend),/differs/); checks++;
  const migrations = path.join(scratch, "migrations");mkdirSync(migrations);
  for (let i=1;i<=9;i++) writeFileSync(path.join(migrations,String(i).padStart(3,"0")+"_fixture.sql"),"-- synthetic checksum fixture\n");
  const inventory=migrationInventory(migrations);
  check(inventory.length===9 && inventory[8].name.startsWith("009_"),"all nine legacy migrations are ordered");
  check(inventory[0].fileSha256===sha256(Buffer.from("-- synthetic checksum fixture\n")),"raw file hashes are included");
  const extra=path.join(migrations,"010_unapproved.sql");
  writeFileSync(extra,"-- never execute\n");
  assert.throws(()=>migrationInventory(migrations),/ordered migration chain/); checks++;
  rmSync(extra);
  const ninth=path.join(migrations,inventory.at(-1).name); rmSync(ninth);
  assert.throws(()=>migrationInventory(migrations),/ordered migration chain/); checks++;
  writeFileSync(path.join(migrations,"008_duplicate.sql"),"-- fixture");
  assert.throws(()=>migrationInventory(migrations),/ordered migration chain/); checks++;
  const scanned = path.join(scratch, "scan");mkdirSync(scanned);
  writeFileSync(path.join(scanned, "safe.json"), "{}");
  check(scanPackage(scanned, candidates).passed, "a safe package passes the scan");
  writeFileSync(path.join(scanned, "copied.txt"), candidates[0], "utf16le");
  const result = scanPackage(scanned, candidates);
  check(!result.passed && result.problems.some(p => p.reason === "configured private value"), "UTF16 accidental credential copies are blocked");
  check(!JSON.stringify(result).includes(candidates[0]), "scanner diagnostics do not disclose the secret");
  // Exercise the actual CLI before Git, secret loading, copying or ZIP creation.
  const stale = path.join(scratch, "stale-publish");
  for (const folder of ["frontend/publish/node_modules/next", "frontend/publish/.next", "frontend/.next", "backend/publish"])
    mkdirSync(path.join(stale, folder), {recursive: true});
  for (const [name, version] of Object.entries({next: "16.4.6", "@next/env": "16.4.6", react: "19.2.6", "react-dom": "19.2.6", zod: "4.6.7"})) {
    const folder = path.join(stale, "frontend/node_modules", name); mkdirSync(folder, {recursive: true});
    writeFileSync(path.join(folder, "package.json"), JSON.stringify({name, version}));
  }
  writeFileSync(path.join(stale, "frontend/publish/server.js"), "// synthetic file, never executed\n");
  writeFileSync(path.join(stale, "backend/publish/ChapGiti.Api.exe"), "synthetic file, never executed\n");
  writeFileSync(path.join(stale, "frontend/.next/BUILD_ID"), "latest-build");
  writeFileSync(path.join(stale, "frontend/publish/.next/BUILD_ID"), "previous-build");
  for (const version of ["16.3.4", "16.4.6"]) {
    writeFileSync(path.join(stale, "frontend/publish/node_modules/next/package.json"), JSON.stringify({name: "next", version}));
    const attempt = spawnSync(process.execPath, [path.resolve("scripts/package-dotnet.mjs"), "--release-only"], {cwd: stale, encoding: "utf8", windowsHide: true});
    check(attempt.status !== 0 && attempt.stderr.includes("Frontend publish is stale"), "the real packaging CLI rejects an old framework or build before copying");
    check(!existsSync(path.join(stale, "outputs")), "a rejected stale publish creates no Release or Source artifact");
  }
  const stage=path.join(scratch,"extracted");
  const put=(name,bytes)=>{const f=path.join(stage,name);mkdirSync(path.dirname(f),{recursive:true});writeFileSync(f,bytes);};
  for(const name of runtimeFiles) put(name,"fixture");
  put("frontend/.next/prerender-manifest.json.template", JSON.stringify({preview:{}}));
  put("frontend/.next/server/preview-props.json.template", "{}");
  put("frontend/.next/server/server-reference-manifest.json.template", JSON.stringify({node:{},edge:{}}));
  const ef=sourceMigrations(".");
  check(ef.ef.length>=3 && ef.legacy.length===9,"reviewed EF IDs, snapshot and frozen legacy inventory");
  const sourceDir="backend/ChapGiti.Infrastructure/Persistence";
  for(const n of ef.legacy) put("backend/publish/Data/"+n,readFileSync(path.join(sourceDir,"Migrations",n)));
  for(const n of embedded) put("migrations/ef/"+n,readFileSync(path.join(sourceDir,"Ef",n)));
  const compiled=structuredClone(ef), chain=migrationInventory(path.join(stage,"backend/publish/Data"));
  put("MIGRATIONS-CHECKSUMS.json",JSON.stringify({order:chain,compiled}));
  const host=Buffer.alloc(256);host.write("MZ");host.writeUInt32LE(128,60);host.writeUInt32LE(0x4550,128);host.writeUInt16LE(0x8664,132);
  put("backend/publish/ChapGiti.Api.exe",host);
  put("backend/publish/ChapGiti.Api.runtimeconfig.json",JSON.stringify({runtimeOptions:{tfm:"net10.0",includedFrameworks:[{name:"Microsoft.AspNetCore.App"}]}}));
  put("frontend/.next/required-server-files.json",JSON.stringify({config:{output:"standalone"},files:[".next/server/app-paths-manifest.json"]}));
  put("backend/publish/ChapGiti.Api.deps.json",JSON.stringify({runtimeTarget:{name:".NETCoreApp,Version=v10.0/win-x64"},libraries:{"Microsoft.EntityFrameworkCore.SqlServer/10.0.15":{}}}));
  for(const name of ["sharp-win32-x64-0.35.5.node","libvips-42.dll","libvips-cpp-8.18.7.dll"]) put("frontend/node_modules/@img/sharp-win32-x64/lib/"+name,"native fixture");
  put("frontend/node_modules/next/package.json",JSON.stringify({version:"fixture"}));
  put("frontend/.next/static/app.js","x");put("frontend/.next/static/app.css","x");
  const files=Object.fromEntries(requiredSourcePaths.map(name=>[name,sha256(Buffer.from(name))]));
  const source={gitRevision:"a".repeat(40),sourceSha256:sha256(Buffer.from(JSON.stringify(files)))};
  const provenance={gitRevision:source.gitRevision,files,untracked:[requiredSourcePaths[0]]};
  put("SOURCE-INVENTORY.json",JSON.stringify(provenance));
  const incomplete={...files};delete incomplete[requiredSourcePaths[0]];
  assert.throws(()=>verifySourceInventory({...provenance,files:incomplete},source),/Required source missing/);checks++;
  assert.throws(()=>verifySourceInventory({...provenance,untracked:[".env.local"]},source),/Invalid untracked/);checks++;
  const altered={...files,[requiredSourcePaths[0]]:"0".repeat(64)};
  assert.throws(()=>verifySourceInventory({...provenance,files:altered},source),/does not match/);checks++;
  assert.throws(()=>verifySourceInventory({...provenance,files:{...files,"../private.txt":"0".repeat(64)}},source),/Invalid source inventory entry/);checks++;
  const manifest={packageMode:"release-only",source,migrations:chain,efMigrations:ef.ef,frontendVersions:{next:"fixture"},files:fileHashes(stage)};
  put("RELEASE-MANIFEST.json",JSON.stringify(manifest));
  check(verifyDirectory(stage)===Object.keys(manifest.files).length,"complete staged tree verifies offline");
  for(const name of runtimeFiles){
    const f=path.join(stage,name),original=readFileSync(f);rmSync(f);
    assert.throws(()=>checkLayout(stage,manifest),/Required release file/); checks++; put(name,original);
  }
  for(const [dir,names] of [["scripts",scripts],["deploy/iis",iis],["docs",guides],["deploy/sql",["acceptance-2025.sql","check-target.ps1"]]])
    for(const name of names) check(!!requireFile(".",dir+"/"+name),"manual release source exists: "+name);
  const native=path.join(stage,"frontend/node_modules/@img/sharp-win32-x64/lib/libvips-42.dll");
  rmSync(native);assert.throws(()=>checkLayout(stage,manifest),/native Windows/);checks++;writeFileSync(native,"native fixture");
  assert.throws(()=>matchMetadata(ef,{...ef,ef:ef.ef.slice(0,-1)}),/Compiled migration/);checks++;
  assert.throws(()=>matchMetadata(ef,{...ef,resources:{...ef.resources,"InitialPrintCatalog.json":"modified"}}),/Compiled migration/);checks++;
  put("unexpected.txt","fixture");assert.throws(()=>verifyDirectory(stage),/Extra or missing/);checks++;rmSync(path.join(stage,"unexpected.txt"));
  put("frontend/server.js","changed");assert.throws(()=>verifyDirectory(stage),/integrity failed/);checks++;
  for(const mode of ["--source","--release-and-source","--publish"]){
    const result=spawnSync(process.execPath,[path.resolve("scripts/package-dotnet.mjs"),mode],{cwd:scratch,encoding:"utf8",windowsHide:true});
    check(result.status!==0 && result.stderr.includes("Usage:"),"unsupported packaging mode is rejected before staging");
  }
  const packedCode=readFileSync("scripts/package-dotnet.mjs","utf8");
  check(!/--migrate|sql005|sqlFixture|test-published|SOURCE-MANIFEST|--import-stdin|db-setup\.mjs/.test(packedCode.replace('"db:setup":"node scripts/db-setup.mjs"', '')),"packaging has no SQL, import, provisioning or source archive execution");
  const localInputs=path.join(scratch,"inputs");
  for(const n of ["app","components","hooks","lib","public","scripts"]) mkdirSync(path.join(localInputs,"frontend",n),{recursive:true});
  for(const n of ["package.json","package-lock.json","next.config.ts","postcss.config.mjs","tsconfig.json","eslint.config.mjs"])writeFileSync(path.join(localInputs,"frontend",n),"fixture");
  const output=path.join(localInputs,"frontend/publish");mkdirSync(output);writeFileSync(path.join(output,"server.js"),"old");
  mkdirSync(path.join(localInputs,"scripts"));
  for(const name of ["release-source.mjs","release-files.mjs"]) writeFileSync(path.join(localInputs,"scripts",name),"fixture");
  const before=inputDigest(localInputs,"frontend");
  writeBuildReceipt(localInputs,"frontend",output,before); requireBuildReceipt(localInputs,"frontend",output);checks++;
  writeFileSync(path.join(output,"server.js"),"swapped");assert.throws(()=>requireBuildReceipt(localInputs,"frontend",output),/Stale/);checks++;
  writeFileSync(path.join(localInputs,"frontend/app/untracked.tsx"),"new working-tree input");
  assert.throws(()=>writeBuildReceipt(localInputs,"frontend",output,before),/changed during/);checks++;
} finally {
  const resolved = realpathSync(scratch);
  assert.equal(path.dirname(resolved), tempRoot);
  assert.ok(path.basename(resolved).startsWith("chap-release-test-"));
  rmSync(resolved, {recursive: true, force: true});
}
console.log(`${checks} release packaging checks passed; no SQL, credentials or SMS were used.`);
