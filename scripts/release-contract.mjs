import { existsSync, readFileSync, readdirSync, statSync } from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { allowedReleaseFile, filesIn, migrationInventory, sha256 } from "./release-files.mjs";

import { checkSealedFrontend, frameworkManifests } from "./initialize-frontend.mjs";

const efRoot = "backend/ChapGiti.Infrastructure/Persistence/Ef";
export const knownEf = ["20261117090230_Legacy009Baseline", "20261117092337_CatalogAndMagazine", "20261118113729_TicketMessageReadState",
  "20261122130922_CutTariffVersions", "20261123062444_ProposedCutTariffs"];
export const embedded = ["InitialPrintCatalog.json", "Legacy009Baseline.sql"];
export const scripts = ["dotnet-runtime.mjs", "start-node.mjs", "db-setup.mjs", "admin-setup.mjs", "check-deploy.mjs", "deployment-context.mjs", "verify-release.mjs", "release-files.mjs", "release-contract.mjs", "initialize-frontend.mjs"];
export const iis = ["install-service.ps1", "service-config.ps1", "GitiNode.xml.template", "configure-iis.ps1", "proxy-config.ps1", "configure-otp.ps1", "web.config"];
export const guides = ["WINDOWS-SERVER-INSTALL-FA.md", "DATABASE-MIGRATION-FA.md", "CHAP-EF-MIGRATION-ACTIVATION-FA.md", "SIGNALR-REALTIME-FA.md", "SQL2025-ACCEPTANCE-FA.md", "UPGRADE-CUT-TARIFFS-FA.md", "CUT-TARIFF-POLICY-FA.md"];
export const runtimeFiles = [
  "frontend/server.js", "frontend/.next/BUILD_ID", "frontend/.next/required-server-files.json", "frontend/.next/server/app-paths-manifest.json",
  "frontend/node_modules/next/package.json", "frontend/node_modules/@next/env/dist/index.js", "frontend/public/fonts/vazirmatn.woff2",
  "frontend/node_modules/@img/sharp-win32-x64/package.json", "frontend/public/fonts/OFL.txt", "frontend/public/og.png", "frontend/public/banners/print.svg",
  "backend/publish/Data/defaults.json", ...["ChapGiti.Api.exe", "ChapGiti.Api.dll", "ChapGiti.Application.dll", "ChapGiti.Domain.dll",
    "ChapGiti.Infrastructure.dll", "ChapGiti.Api.runtimeconfig.json", "ChapGiti.Api.deps.json", "Microsoft.Data.SqlClient.dll",
    "Microsoft.Data.SqlClient.SNI.dll", "Microsoft.EntityFrameworkCore.dll", "Microsoft.EntityFrameworkCore.Relational.dll",
    "Microsoft.EntityFrameworkCore.SqlServer.dll", "Microsoft.AspNetCore.SignalR.Core.dll", "Microsoft.AspNetCore.SignalR.Protocols.Json.dll",
    "Microsoft.AspNetCore.Http.Connections.dll", "coreclr.dll", "hostfxr.dll", "hostpolicy.dll"].map(name => "backend/publish/" + name),
  ...frameworkManifests.map(name => "frontend/" + name + ".template"),
  ...scripts.map(n=>"scripts/"+n), ...iis.map(n=>"deploy/iis/"+n), ...guides.map(n=>"docs/"+n),
  "package.json", ".env.example", "README.md", "SOURCE-INVENTORY.json", "MIGRATIONS-CHECKSUMS.json", "deploy/sql/acceptance-2025.sql", "deploy/sql/check-target.ps1",
  ...embedded.map(n=>"migrations/ef/"+n)
];

// These entry points can disappear without a compiler error (routes, assets and migration tooling).
export const requiredSourcePaths = [
  "frontend/app/(public)/order/page.tsx", "frontend/app/(public)/account/orders/[id]/page.tsx",
  "frontend/app/(public)/blog/[slug]/page.tsx", "frontend/app/(public)/tickets/[id]/page.tsx",
  "frontend/app/admin/[[...section]]/page.tsx", "frontend/app/admin/login/page.tsx",
  "frontend/components/notification-center.tsx", "frontend/components/ticket-thread.tsx",
  "frontend/public/fonts/vazirmatn.woff2", "frontend/public/og.png", "frontend/scripts/publish.mjs",
  "backend/ChapGiti.Api/Program.cs", "backend/ChapGiti.Api/Controllers/OrdersController.cs",
  "backend/ChapGiti.Api/Controllers/TicketsController.cs", "backend/ChapGiti.Api/Realtime/RealtimeHubs.cs",
  efRoot + "/Migrations/ChapGitiDbContextModelSnapshot.cs", ...embedded.map(name => efRoot + "/" + name),
  ...scripts.map(name => "scripts/" + name), ...iis.map(name => "deploy/iis/" + name), ...guides.map(name => "docs/" + name)
];

export function requireSourcePaths(files) {
  for (const name of requiredSourcePaths) {
    if (!Object.hasOwn(files, name)) throw Error("Required source missing from inventory: " + name);
  }
}

export function verifySourceInventory(inventory, source) {
  const {files, untracked} = inventory;
  if (!files || typeof files !== "object" || Array.isArray(files) || !Array.isArray(untracked)) throw Error("Invalid source inventory.");
  requireSourcePaths(files);
  for (const [name, hash] of Object.entries(files)) {
    if (!/^[a-f0-9]{64}$/.test(hash) || path.posix.isAbsolute(name) || /[:\\\\]/.test(name) ||
        name.split("/").some(part => !part || part === "." || part === "..") || !allowedReleaseFile(name, {source:true}))
      throw Error("Invalid source inventory entry.");
  }
  if (new Set(untracked).size !== untracked.length || !untracked.every(name => Object.hasOwn(files, name))) throw Error("Invalid untracked source list.");
  if (inventory.gitRevision !== source?.gitRevision || sha256(Buffer.from(JSON.stringify(files))) !== source?.sourceSha256)
    throw Error("Source inventory does not match the release manifest.");
}

export function requireFile(root, name) {
  const file = path.join(root,name);
  if (!existsSync(file) || !statSync(file).isFile() || statSync(file).size === 0) throw Error("Required release file missing or empty: " + name);
  return file;
}
export function sourceMigrations(root) {
  const dir = path.join(root,efRoot,"Migrations");
  const names = readdirSync(dir).filter(n=>/^\d{14}_\w+\.cs$/.test(n)).sort();
  const ids = names.map(n=>n.slice(0,-3));
  if (!knownEf.every((id,index)=>ids[index]===id)) throw Error("EF baseline or approved migration is missing.");
  const designers = readdirSync(dir).filter(n=>n.endsWith(".Designer.cs")).sort();
  if (JSON.stringify(designers) !== JSON.stringify(ids.map(id=>id+".Designer.cs"))) throw Error("EF migration/designer mismatch.");
  for(const id of ids) {
    if(!readFileSync(path.join(dir,id+".Designer.cs"),"utf8").includes('[Migration("'+id+'")]')) throw Error("EF migration ID mismatch: "+id);
  }
  requireFile(dir,"ChapGitiDbContextModelSnapshot.cs");
  return {legacy:migrationInventory(path.join(root,"backend/ChapGiti.Infrastructure/Persistence/Migrations")).map(m=>m.name), ef:ids,
    resources:Object.fromEntries(embedded.map(n=>[n,sha256(readFileSync(requireFile(root,efRoot+"/"+n)))]))};
}

export function inspectCompiled(executable, args = []) {
  // Pass only OS loader variables. A pre-feature binary must fail closed before opening any catalog.
  const env = Object.fromEntries(Object.entries(process.env).filter(([key])=>/^(systemroot|windir|path|temp|tmp|comspec|userprofile|localappdata|programfiles|programfiles\(x86\)|dotnet_root)$/i.test(key)));
  Object.assign(env, {DOTNET_ENVIRONMENT:"Production", ASPNETCORE_ENVIRONMENT:"Production", ConnectionStrings__SqlServer:"", Sms__Enabled:"false", Otp__Enabled:"false", DOTNET_NOLOGO:"1"});
  const child = spawnSync(executable,[...args,"--release-metadata"],{env, encoding:"utf8", windowsHide:true, timeout:30000});
  if(child.status!==0) throw Error("Offline compiled migration inventory failed. Rebuild the current backend.");
  return JSON.parse(child.stdout);
}
export function matchMetadata(expected, actual) {
  if (JSON.stringify(actual.legacy)!==JSON.stringify(expected.legacy) || JSON.stringify(actual.ef)!==JSON.stringify(expected.ef) ||
      Object.keys(actual.resources??{}).length!==embedded.length || embedded.some(n=>!expected.resources[n] || actual.resources[n]!==expected.resources[n]))
    throw Error("Compiled migration IDs or embedded resources differ from reviewed sources.");
}

export function checkLayout(root, manifest) {
  for (const name of runtimeFiles) requireFile(root,name);
  const json = name => JSON.parse(readFileSync(path.join(root,name),"utf8"));
  verifySourceInventory(json("SOURCE-INVENTORY.json"), manifest.source);
  const checked = json("MIGRATIONS-CHECKSUMS.json");
  const legacy = migrationInventory(path.join(root,"backend/publish/Data"));
  if (JSON.stringify(legacy)!==JSON.stringify(checked.order) || JSON.stringify(legacy)!==JSON.stringify(manifest.migrations)) throw Error("Legacy migration checksum mismatch.");
  const meta = checked.compiled;
  if (!meta || !knownEf.every((id,index)=>meta.ef?.[index]===id) || !meta.ef.every((id,i)=>/^\d{14}_\w+$/.test(id) && (!i || id>meta.ef[i-1]))) throw Error("Invalid EF migration sequence.");
  matchMetadata({legacy:legacy.map(row=>row.name),ef:manifest.efMigrations,resources:Object.fromEntries(embedded.map(n=>[n,sha256(readFileSync(path.join(root,"migrations/ef",n)))]))},meta);
  const host = readFileSync(path.join(root,"backend/publish/ChapGiti.Api.exe"));
  const pe = host.length>=64 ? host.readUInt32LE(60) : 0;
  if(host.toString("ascii",0,2)!=="MZ" || pe<64 || pe+6>host.length || host.readUInt32LE(pe)!==0x4550 || host.readUInt16LE(pe+4)!==0x8664) throw Error("Expected a native Windows x64 app host.");
  const runtime=json("backend/publish/ChapGiti.Api.runtimeconfig.json").runtimeOptions;
  if(runtime.tfm!=="net10.0" || runtime.framework || !runtime.includedFrameworks?.some(f=>f.name==="Microsoft.AspNetCore.App")) throw Error("Expected a self-contained .NET 10 runtime.");
  const assets=filesIn(path.join(root,"frontend/.next/static"));
  if (![".js",".css"].every(ext=>assets.some(n=>n.endsWith(ext) && statSync(path.join(root,"frontend/.next/static",n)).size>0))) throw Error("Missing Next.js standalone static assets.");
  const next=json("frontend/.next/required-server-files.json");
  if(next.config.output!=="standalone" || json("frontend/node_modules/next/package.json").version!==manifest.frontendVersions.next) throw Error("Standalone framework mismatch.");
  checkSealedFrontend(path.join(root, "frontend"));
  if (!Array.isArray(next.files) || !next.files.length) throw Error("Missing Next.js traced server file list.");
  for (const file of next.files) {
    const name=file.replaceAll("\\","/");
    if (!name.startsWith(".next/") || name.split("/").includes("..")) throw Error("Invalid Next.js traced path.");
    requireFile(root,"frontend/"+name+(frameworkManifests.includes(name) ? ".template" : ""));
  }
  const native=filesIn(path.join(root,"frontend/node_modules/@img/sharp-win32-x64/lib"));
  for(const pattern of [/^sharp-win32-x64(?:-[\d.]+)?\.node$/, /^libvips-\d+\.dll$/, /^libvips-cpp-[\d.]+\.dll$/])
    if(!native.some(n=>pattern.test(n) && statSync(path.join(root,"frontend/node_modules/@img/sharp-win32-x64/lib",n)).size>0)) throw Error("Missing native Windows image runtime.");
  const deps=json("backend/publish/ChapGiti.Api.deps.json");
  if(!deps.runtimeTarget?.name.endsWith("/win-x64") || !Object.keys(deps.libraries??{}).some(n=>n.startsWith("Microsoft.EntityFrameworkCore.SqlServer/"))) throw Error("Missing self-contained SQL Server provider metadata.");
}
