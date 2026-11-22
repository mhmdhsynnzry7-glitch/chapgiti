import test from "node:test";
import assert from "node:assert/strict";
import path from "node:path";
import net from "node:net";
import {spawn} from "node:child_process";
import {randomUUID} from "node:crypto";
import {once} from "node:events";
import {root,helper,repoKey,preflight,preflightPlan,listeners,describe,liveHost,identity,bindable,sleep} from "../../scripts/development/processes.mjs";

const win=process.platform==="win32";
const id=randomUUID();
const dotnet=String.raw`C:\Program Files\dotnet\dotnet.exe`;
function processInfo(pid,exe,args,parent=0,born=String(pid*100)){
  return {pid,exe,name:path.win32.basename(exe),parent,born,command:[exe,...args].map(x=>'"'+x+'"').join(" ")};
}
function snapshot(processes=[],ports=[],records=[]){
  return {byId:new Map(processes.map(p=>[p.pid,p])),ports,records};
}
const api=processInfo(102,dotnet,[path.join(root,"backend/ChapGiti.Api/bin/Debug/net10.0/ChapGiti.Api.dll"),"--main-development"]);
const next=processInfo(103,process.execPath,[path.join(root,"frontend/node_modules/next/dist/bin/next"),"dev","--webpack","--hostname","127.0.0.1","--port","3000"]);
const web=processInfo(104,process.execPath,[path.join(root,"frontend/node_modules/next/dist/server/lib/start-server.js")],103);
const apiPort={port:5100,pid:api.pid},webPort={port:3000,pid:web.pid};
test("two free ports pass Development preflight",()=>assert.deepEqual(preflightPlan(snapshot()),[]));
for(const [label,processes,port] of [["backend",[api],apiPort],["frontend",[next,web],webPort]]){
  test("stale verified "+label+" is selected for cleanup",()=>{
    const [item]=preflightPlan(snapshot(processes,[port]));assert.equal(item.pid,port.pid);assert.equal(item.active,false);assert.equal(item.owner,"CHAP");
  });
}
for(const [label,exe,port] of [["Node",process.execPath,3000],["dotnet",dotnet,5100]]){
  test("unrelated "+label+" is preserved; status redacts arguments",()=>{
    const other=processInfo(110,exe,["elsewhere","Password=private-test-value","--api-key","private-key"]);
    const input=snapshot([other],[{pid:110,port}]);
    const [item]=listeners(input);assert.equal(item.owner,"unknown");
    assert.throws(()=>preflightPlan(input),/PORT_IN_USE/);
    assert.doesNotMatch(describe(item),/private-test-value|private-key|Password/);
  });
}
test("an active second start leaves both listeners intact",()=>{
  const launcher=processInfo(101,process.execPath,["scripts/dev-dotnet.mjs"]);
  const input=snapshot([launcher,{...api,parent:101},{...next,parent:101},web],[apiPort,webPort]);
  assert.ok(listeners(input).every(i=>i.active && i.owner==="CHAP"));assert.throws(()=>preflightPlan(input),/PORT_IN_USE/);
});
test("recycled parent PID is not an active launcher",()=>{
  const later=processInfo(101,process.execPath,["scripts/dev-dotnet.mjs"],0,"999999");
  assert.equal(preflightPlan(snapshot([later,{...api,parent:101}],[apiPort]))[0].active,false);
});
for(const [role,processes,port] of [["backend",[api],apiPort],["frontend",[next,web],webPort]]){
  test("preflight clears only the stale "+role+" identity and confirms port release before proceeding",async()=>{
    const history=[];
    await preflight({read:async()=>snapshot(processes,[port]),terminate:async item=>history.push(item.pid),
      released:async()=>{history.push("released");return true;}});
    assert.deepEqual(history,[port.pid,"released"]);
    await assert.rejects(preflight({read:async()=>snapshot(processes,[port]),terminate:async()=>{},released:async()=>false}),/PORT_IN_USE/);
  });
}
test("conflicts are reported before any stale process is stopped",async()=>{
  const other=processInfo(201,process.execPath,["another-project.mjs"]);
  let mutations=0;
  await assert.rejects(preflight({read:async()=>snapshot([api,other],[apiPort,{pid:201,port:3000}]),terminate:async()=>mutations++}),/PORT_IN_USE/);
  assert.equal(mutations,0);
});
test("a reused recorded child PID cannot authorize termination",()=>{
  const imposter=processInfo(api.pid,dotnet,["Other.Api.dll"],0,"999999");
  const record={id,backend:identity(api)};const snap=snapshot([imposter],[apiPort],[record]);
  assert.equal(listeners(snap)[0].owner,"unknown");assert.throws(()=>preflightPlan(snap),/PORT_IN_USE/);
});
test("a Next production listener without its Development CLI is unknown",()=>{
  assert.equal(listeners(snapshot([web],[webPort]))[0].owner,"unknown");
});
test("session host identity requires exact repository, session and birth",()=>{
  const host=processInfo(111,"C:/Windows/System32/WindowsPowerShell/v1.0/powershell.exe",["-File",helper,"-Action","host","-Session",id,"-Key",repoKey]);
  const s={id,host:identity(host)};assert.ok(liveHost(snapshot([host]),s));
  assert.ok(!liveHost(snapshot([{...host,born:"999999"}]),s));
  assert.ok(!liveHost(snapshot([host]),{...s,id:randomUUID()}));
});
test("backend database target guards are retained",async()=>{
  const child=spawn(process.execPath,["tests/development/main-database-guard.mjs"],{windowsHide:true,stdio:"pipe"});
  let output="", diagnostic="";
  child.stdout.on("data",b=>output+=b);
  child.stderr.on("data",b=>diagnostic+=b);
  const [code]=await once(child,"close");
  assert.equal(code,0,diagnostic || output);
  assert.match(output,/7 main Development target rejection checks passed/);
});

// Two synthetic listeners and a Next-like grandchild exercise the actual Windows job.
// No application ports, configured database, services or external processes are used.
for(const scenario of ["stop","backend-fails","frontend-fails","parent-disappears","host-crashes","pending-migration"]){
  test("Windows job cleans its entire tree: "+scenario,{skip:!win,timeout:35000},async()=>{
    const token=randomUUID(),fixture=path.join(root,"tests/development/job-fixture.mjs");
    const keeper=net.createServer();keeper.listen(0,"127.0.0.1");await once(keeper,"listening");
    const keeperPort=keeper.address().port;
    const script="Add-Type -Path (Join-Path $env:CHAP_TEST_ROOT 'scripts/development/WindowsDevJob.cs');exit [WindowsDevJob]::Run($env:CHAP_TEST_NODE,$env:CHAP_TEST_WORKER,$env:CHAP_TEST_ID,('Local\\ChapGiti-test-'+$env:CHAP_TEST_ID),('Local\\ChapGiti-test-stop-'+$env:CHAP_TEST_ID))";
    const host=spawn("powershell.exe",["-NoProfile","-NonInteractive","-Command",script],{windowsHide:true,stdio:"pipe",env:{...process.env,CHAP_TEST_ROOT:root,CHAP_TEST_NODE:process.execPath,CHAP_TEST_WORKER:fixture,CHAP_TEST_ID:token,CHAP_TEST_SCENARIO:scenario}});
    const ended=once(host,"close");let output="";host.stdout.on("data",b=>output+=b);host.stderr.on("data",b=>output+=b);
    try{
      if(scenario==="pending-migration"){
        const [code]=await ended;
        assert.equal(code,1);assert.match(output,/\[MIGRATION_REQUIRED\] Pending EF migration/);
        assert.match(output,/\[BACKEND_CLI\] --check exited \(1\)/);assert.doesNotMatch(output,/FIXTURE_READY/);
        return;
      }
      let ready;for(let i=0;i<180 && host.exitCode===null;i++){ready=output.match(/FIXTURE_READY (\d+) (\d+)/);if(ready)break;await sleep(70);}
      assert.ok(ready,output);
      if(scenario==="stop")host.stdin.end("stop\n");
      if(scenario==="parent-disappears")host.stdin.end();
      if(scenario==="host-crashes")host.kill();
      const [code,signal]=await ended;
      if(scenario==="host-crashes")assert.ok(signal || code!==0);
      else assert.equal(code,scenario.endsWith("fails")?7:0,output);
      for(const port of ready.slice(1).map(Number))assert.equal(await bindable(port),true);
      assert.equal(await bindable(keeperPort),false,"unrelated listener stays alive");
    }finally{if(!host.stdin.destroyed)host.stdin.end();await new Promise(resolve=>keeper.close(resolve));}
  });
}
