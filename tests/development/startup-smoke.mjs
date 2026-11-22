import assert from "node:assert/strict";
import {spawn} from "node:child_process";
import {randomBytes} from "node:crypto";
import {once} from "node:events";
import {createServer} from "node:net";
import {setTimeout as delay} from "node:timers/promises";
import {stop} from "../../scripts/dotnet-runtime.mjs";

const id = process.env.CHAP_DEV_SANDBOX_ID;
assert.match(id ?? "", /^[a-f0-9]{32}$/);
const server = "(localdb)\\ChapGitiDev_" + id;
const catalog = "ChapGiti_Dev_" + id;
assert.equal(process.env.SQLSERVER_CONNECTION_STRING,
  `Server=${server};Database=${catalog};Integrated Security=True;Encrypt=False;Pooling=False`);
assert.equal(process.env.ConnectionStrings__SqlServer, process.env.SQLSERVER_CONNECTION_STRING);
assert.equal(process.env.SMS_ENABLED, "false");
assert.equal(process.env.OTP_ENABLED, "false");
const workEnv = {...process.env, NEXT_TELEMETRY_DISABLED:"1"};
const checks = [];
function check(condition, label) {
  assert.ok(condition, label);
  checks.push(label);
  console.log("PASS " + label);
}

function start(file, args, env=workEnv, input) {
  const child=spawn(file,args,{env,windowsHide:true,stdio:["pipe","pipe","pipe"]});
  let output="";
  for (const stream of [child.stdout,child.stderr]) stream.on("data",chunk=>{ output=(output+chunk).slice(-48000); });
  child.stdin.end(input);
  return {child, done:once(child,"close").then(([code])=>code), output:()=>output};
}
async function finished(handle,timeout=90000) {
  let timer;
  try {
    return await Promise.race([handle.done,new Promise((_,reject)=>{
      timer=setTimeout(()=>reject(new Error("Startup check timed out.")),timeout);
    })]);
  } finally { clearTimeout(timer); await stop(handle.child); }
}
async function portAvailable(port) {
  const listener=createServer();
  const ready=once(listener,"listening");
  listener.listen(port,"127.0.0.1");
  await ready;
  await new Promise(resolve=>listener.close(resolve));
}
const cmd=(line)=>start("cmd.exe",["/d","/c",line]);
const script=(...args)=>start(process.execPath,args);
const api=workEnv.DOTNET_API_URL, site=workEnv.SITE_URL;
assert.equal(api,"http://127.0.0.1:5100");
assert.equal(site,"http://127.0.0.1:3000");
await Promise.all([3000,5100].map(portAvailable));
const normal=cmd("npm.cmd run dev");
check(await finished(normal)!==0 && normal.output().includes("[DATABASE_CONFIG]"), "normal npm startup refuses the retired sandbox selector");
// Exercise the generic supervisor only after the fixture ID, connection and send flags above match.
const fixtureStack=()=>script("--input-type=module","-e","import {stack} from './scripts/dotnet-runtime.mjs'; await stack(true);");
const empty=fixtureStack();
check(await finished(empty)!==0 && empty.output().includes("[MIGRATION_REQUIRED]") &&
  empty.output().includes("009_order_views.sql") && empty.output().includes("[BACKEND_CLI] --check"),
  "supervisor propagates the real missing-legacy diagnostic and a failing exit");
const cli = "import {cli,environment} from './scripts/dotnet-runtime.mjs'; await cli(['--check'],environment(true),{development:true});";
for (const [sql,expected] of [
  ["","[DATABASE_CONFIG]"],["NotAConnectionProperty=1","[DATABASE_CONFIG]"],
  ["Server=tcp:127.0.0.1,1;Database=Unreachable;Integrated Security=True;Connect Timeout=1;ConnectRetryCount=0","[DATABASE_CONNECTION]"]
]) {
  const failed=start(process.execPath,["--input-type=module","-e",cli],{...workEnv,SQLSERVER_CONNECTION_STRING:sql});
  check(await finished(failed)!==0 && failed.output().includes(expected), "explicit invalid/unreachable configuration fails closed ("+expected+")");
}
const aliasEnv={...workEnv}; delete aliasEnv.SQLSERVER_CONNECTION_STRING;
const alias=start(process.execPath,["--input-type=module","-e",
  "import {environment} from './scripts/dotnet-runtime.mjs'; const expected=process.env.ConnectionStrings__SqlServer; for(let i=0;i<2;i++) if(environment(true).ConnectionStrings__SqlServer!==expected) process.exit(1);"],aliasEnv);
check(await finished(alias)===0,"explicit ASP.NET catalog overrides dotenv across repeated environment loads");
check(await finished(cmd("npm.cmd run db:setup"))===0,"explicit source maintenance applies SQL and EF");
const noAdmin=script("--input-type=module","-e",cli);
check(await finished(noAdmin)!==0 && noAdmin.output().includes("No administrator is configured"),"initialized schema distinguishes missing administrator");
const password="Q!"+randomBytes(24).toString("hex");
const admin=start(process.execPath,["scripts/admin-setup.mjs","sandbox@example.test","--password-stdin"],workEnv,password+"\n");
check(await finished(admin)===0,"initialize only the new synthetic administrator");
const active=fixtureStack();
try {
  let health;
  for (let attempt=0;attempt<180;attempt++) {
    assert.equal(active.child.exitCode,null,"development stack exited during readiness");
    try {
      const r=await fetch(api+"/health",{signal:AbortSignal.timeout(1800)});
      if(r.ok) health=await r.json();
      if(health?.ok && active.output().includes("Ready in")) break;
    } catch {}
    await delay(500);
  }
  check(health?.ok && health.backend==="ASP.NET Core" && health.database==="SQL Server","actual SQL-backed /health succeeds");
  for (const [url,status] of [[site+"/",200],[site+"/admin/login",200],[api+"/api/customer/me",401],[api+"/api/admin/print-catalog",401]]) {
    const response=await fetch(url,{signal:AbortSignal.timeout(45000)});
    check(response.status===status, new URL(url).pathname+" returns "+status);
  }
  const result=await (await fetch(api+"/api/public/print-catalog")).json();
  check(result.items?.length===80 && new Set(result.items.map(x=>x.kind)).size===8,"running API loads the isolated catalog");
  const login=await fetch(api+"/api/auth/login",{method:"POST",headers:{Origin:site,"Content-Type":"application/json"},
    body:JSON.stringify({username:"admin",password})});
  check(login.ok && login.headers.has("set-cookie"),"synthetic administrator can log in through the running backend");
  const tree=start("powershell.exe",["-NoProfile","-Command",
    `$apiPid=(Get-NetTCPConnection -LocalPort 5100 -State Listen -ErrorAction Stop | Select-Object -First 1).OwningProcess; $p=Get-CimInstance Win32_Process -Filter "ProcessId=$apiPid"; if ($p.Name -ne 'dotnet.exe' -or $p.CommandLine -notmatch 'ChapGiti\\.Api\\.dll') {exit 2}; $pidToStop=$p.ProcessId; while($p -and $p.ProcessId -ne ${active.child.pid}) {$p=Get-CimInstance Win32_Process -Filter "ProcessId=$($p.ParentProcessId)"}; if(!$p){exit 3}; Stop-Process -Id $pidToStop -Force`]);
  check(await finished(tree)===0,"stop only the fixture backend identified inside the owning npm process tree");
  check(await finished(active,20000)!==0 && active.output().includes("[RUNTIME_EXIT] ASP.NET Core"),"unexpected backend exit shuts the frontend and fails the supervisor");
  await Promise.all([3000,5100].map(portAvailable));
  check(true,"both loopback ports released after supervisor failure");
} finally { await stop(active.child); }
console.log(`${checks.length} Development startup checks passed.`);
