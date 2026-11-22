import assert from "node:assert/strict";
import {readdirSync, readFileSync} from "node:fs";
import path from "node:path";
import {tool} from "./dotnet-runtime.mjs";
import {sqlFixture} from "./sql-test-fixture.mjs";

function sources(directory) {
  return readdirSync(directory, {withFileTypes: true}).flatMap(entry => {
    if (["bin", "obj"].includes(entry.name)) return [];
    const file = path.join(directory, entry.name);
    return entry.isDirectory() ? sources(file) : file.endsWith(".cs") ? [file] : [];
  });
}
for (const layer of ["Domain", "Application"]) {
  for (const file of sources("backend/ChapGiti." + layer)) {
    const code = readFileSync(file, "utf8");
    assert.ok(!/\b(HttpContext|IResult|IConfiguration|SqlStore|SqlConnection|DbContext|DbSet|EntityTypeBuilder|IHostEnvironment)\b/.test(code), file);
    assert.ok(!/\b(SELECT|INSERT|MERGE|UPDATE|DELETE)\s+.*\bdbo\./.test(code), file);
    assert.ok(!/\b(File|Directory)\.(Read|Write|Delete|Create)/.test(code), file);
  }
}
for(const file of sources("backend/ChapGiti.Api/Controllers")){
  const code=readFileSync(file,"utf8");
  assert.ok(!/ChapGiti\.Infrastructure|Microsoft\.Data\.SqlClient|Microsoft\.EntityFrameworkCore|\bDbContext\b|\bSqlConnection\b/.test(code),"Controllers depend on Application contracts only: "+file);
}
for(const file of sources("backend/ChapGiti.Infrastructure")){
  const code=readFileSync(file,"utf8");
  assert.ok(!/\bEnsureCreated(?:Async)?\s*\(/.test(code),"EnsureCreated cannot bypass the reviewed migration chain: "+file);
}
const backend = readFileSync("backend/ChapGiti.Api/ChapGiti.Api.csproj", "utf8");
assert.ok(!/frontend|npm|node_modules/.test(backend), "backend builds without the frontend toolchain");

// Infrastructure may migrate tables in the existing catalog, but it must never provision a database.
const databaseCreation = new RegExp("\\bCRE" + "ATE\\s+DATA" + "BASE\\b", "i");
function activeFiles(directory) {
  return readdirSync(directory, {withFileTypes:true}).flatMap(entry => {
    if (["bin","obj","publish"].includes(entry.name)) return [];
    const file=path.join(directory,entry.name);
    return entry.isDirectory() ? activeFiles(file) : /\.(cs|sql|mjs|ps1)$/.test(file) ? [file] : [];
  });
}
for (const directory of ["backend","scripts","deploy/iis"])
  for (const file of activeFiles(directory))
    assert.ok(!databaseCreation.test(readFileSync(file,"utf8")), "Automatic database creation is forbidden: "+file);
await assert.rejects(sqlFixture(), /disabled/);
console.log("PASS no database provisioning statements; retired mutation fixtures fail before accessing SQL Server.");

await tool("dotnet", ["run", "--project", "backend/tests/ChapGiti.ArchitectureTests", "--verbosity", "quiet"]);
console.log("PASS use-case source boundaries and independent backend project");
