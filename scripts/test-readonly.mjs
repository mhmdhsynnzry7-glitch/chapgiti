import assert from "node:assert/strict";
import {spawn} from "node:child_process";
import {createWriteStream,mkdirSync} from "node:fs";
import path from "node:path";
import {command,cli,environment,stop} from "./dotnet-runtime.mjs";
import {freePort} from "./sql-test-fixture.mjs";

if(!process.argv.includes('--authorize-configured-database-readonly'))
  throw new Error('Configured database access is disabled without explicit --authorize-configured-database-readonly authorization. Use isolated memory/SQL acceptance instead.');
const env=environment(false);
await cli(["--check"],env);
console.log("PASS existing configured database and administrator are accessible; no migration or provisioning was run.");

const base="http://127.0.0.1:"+await freePort();
mkdirSync(".qa",{recursive:true});
const log=createWriteStream(path.resolve(".qa/readonly-database-check.log"));
const backend=command(false);
const server=spawn(backend.file,backend.args,{
  env:{...env,ASPNETCORE_URLS:base},
  stdio:["ignore","pipe","pipe"],windowsHide:true
});
server.stdout.pipe(log);
server.stderr.pipe(log);
try {
  const deadline=Date.now()+60000;
  let ready=false;
  while(Date.now()<deadline) {
    if(server.exitCode!==null) throw new Error("Backend exited; see .qa/readonly-database-check.log");
    try { ready=(await fetch(base+"/health",{signal:AbortSignal.timeout(1000)})).ok; } catch {}
    if(ready)break;
    await new Promise(resolve=>setTimeout(resolve,300));
  }
  assert.ok(ready,"Existing database health check did not become ready.");
  const health=await(await fetch(base+"/health")).json();
  assert.equal(health.database,"SQL Server");
  const bootstrap=await fetch(base+"/api/public/bootstrap");
  assert.equal(bootstrap.status,200);
  const content=await bootstrap.json();
  assert.ok(content.site && !Object.hasOwn(content, "rates"), "Public bootstrap omits commercial tariffs.");
  for(const endpoint of ["auth/me","customer/me","admin/overview"]) {
    const response=await fetch(base+"/api/"+endpoint);
    assert.equal(response.status,401,endpoint);
  }
  console.log("PASS SQL Server health, public content, tariff privacy and anonymous access checks.");
  console.log("Read-only checks passed. No database, customer, order, ticket, administrator or tariff was created or changed.");
} finally {
  await stop(server);
  log.end();
}
