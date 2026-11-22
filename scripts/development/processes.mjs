import { execFile } from "node:child_process";
import { promisify } from "node:util";
import { createHash } from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import net from "node:net";
import { fileURLToPath } from "node:url";

const run = promisify(execFile);
export const root = fs.realpathSync(path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../.."));
export const helper = path.join(root,"scripts/development/windows-session.ps1");
export const stateDir = path.join(root,".qa/dev-processes");
export const repoKey = createHash("sha256").update(root.toLowerCase()).digest("hex").slice(0,32);
export const ports = {backend:5100, frontend:3000};
const uuid = /^[a-f0-9]{8}(?:-[a-f0-9]{4}){3}-[a-f0-9]{12}$/;
const norm = text => path.win32.normalize(text || "").toLowerCase();
const argsOf = p => (p?.command || "").match(/"[^"]*"|[^\s"]+/g)?.slice(1).map(s=>s.replace(/^"|"$/g,"")) || [];
const same = (a,b) => !!a && !!b && a.pid===b.pid && !!a.born && a.born===b.born;
const isNode = p => p?.name?.toLowerCase()===path.win32.basename(process.execPath).toLowerCase() && norm(p.exe)===norm(process.execPath);
const isScript = (arg,file) => norm(arg)===norm(path.join(root,file));
const isChildOf = (child,parent) => child && parent && child.parent===parent.pid && child.born && parent.born && BigInt(child.born)>=BigInt(parent.born);
export const identity = p => p?.born ? {pid:p.pid,born:p.born} : null;
export const psArgs = (action,extra=[]) => ["-NoLogo","-NoProfile","-NonInteractive","-ExecutionPolicy","Bypass","-File",helper,"-Action",action,...extra];

export async function windows(action,extra=[]) {
  try {
    const {stdout} = await run("powershell.exe",psArgs(action,extra),{windowsHide:true,maxBuffer:4*1024*1024,timeout:25000});
    return stdout.trim();
  } catch(error) {
    // execFile errors may contain raw command output. Inspection data is private.
    throw new Error("[DEV_INSPECTION] Windows "+action+" failed (exit "+(error.code??"unknown")+").");
  }
}

export function sessions(){
  if(!fs.existsSync(stateDir))return [];
  return fs.readdirSync(stateDir).filter(name=>uuid.test(name.replace(/\.json$/,"")) && name.endsWith(".json")).map(name=>{
    try {
      const s=JSON.parse(fs.readFileSync(path.join(stateDir,name),"utf8"));
      if(name!==s.id+".json" || s.root!==root)throw Error();
      return s;
    } catch { throw new Error("[DEV_STATE] Cannot validate the local session record "+name+"."); }
  });
}
export function saveSession(state){
  fs.mkdirSync(stateDir,{recursive:true});
  const file=path.join(stateDir,state.id+".json");
  fs.writeFileSync(file+".tmp",JSON.stringify(state),{mode:0o600});
  fs.renameSync(file+".tmp",file);
}
export function forget(session){
  if(uuid.test(session.id))fs.rmSync(path.join(stateDir,session.id+".json"),{force:true});
}
export async function inspect(records=sessions(),additional=[]){
  const ids=[...additional,...records.flatMap(s=>[s.host,s.worker,s.backend,s.frontend,s.launcher].map(p=>p?.pid))].filter(n=>Number.isInteger(n)&&n>0);
  const data=JSON.parse(await windows("inspect",ids.length?["-ProcessIds",[...new Set(ids)].join(",")]:[]));
  const snapshot={records:[...records],ports:data.ports,byId:new Map(data.processes.map(p=>[p.pid,p]))};
  for(const p of data.processes){
    const args=argsOf(p), id=args[args.indexOf("-Session")+1];
    if(!uuid.test(id) || snapshot.records.some(s=>s.id===id))continue;
    const candidate={id,root,host:identity(p)};
    if(liveHost(snapshot,candidate))snapshot.records.push(candidate);
  }
  return snapshot;
}
export function liveHost(snapshot,s){
  const host=snapshot.byId.get(s.host?.pid);
  const args=argsOf(host);
  return same(host,s.host) && host.name.toLowerCase()==="powershell.exe" &&
    args.some((v,i)=>v.toLowerCase()==="-file" && norm(args[i+1])===norm(helper)) &&
    args[args.indexOf("-Session")+1]===s.id && args[args.indexOf("-Key")+1]===repoKey && args[args.indexOf("-Action")+1]==="host";
}
function launcher(snapshot,child){
  const p=snapshot.byId.get(child?.parent);
  if(!isChildOf(child,p)||!isNode(p))return null;
  const args=argsOf(p);
  if(args.length===1 && (norm(args[0])===norm("scripts/dev-dotnet.mjs")||isScript(args[0],"scripts/dev-dotnet.mjs")))return p;
  if(args.length===2 && isScript(args[0],"scripts/development/worker.mjs") && uuid.test(args[1]))return p;
  return null;
}

export function classify(snapshot,role,pid){
  const p=snapshot.byId.get(pid), args=argsOf(p);
  const unknown={role,owner:"unknown",pid,process:p};
  if(!p?.born || !p.exe)return unknown;
  let direct=p;
  if(role==="backend"){
    if(p.name.toLowerCase()!=="dotnet.exe" || args.length!==2 || !isScript(args[0],"backend/ChapGiti.Api/bin/Debug/net10.0/ChapGiti.Api.dll") || args[1]!=="--main-development")return unknown;
  }else{
    if(!isNode(p)||args.length!==1 || !isScript(args[0],"frontend/node_modules/next/dist/server/lib/start-server.js"))return unknown;
    direct=snapshot.byId.get(p.parent);
    if(!isChildOf(p,direct)||!isNode(direct))return unknown;
    const [cli,...flags]=argsOf(direct);
    if((!isScript(cli,"frontend/node_modules/next/dist/bin/next") && norm(cli)!==norm("node_modules/next/dist/bin/next")) ||
      flags.join(" ")!=="dev --webpack --hostname 127.0.0.1 --port 3000")return unknown;
  }
  const session=snapshot.records.find(s=>same(s[role],direct));
  if(!session && snapshot.records.some(s=>s[role]?.pid===direct.pid))return unknown;
  const manager=launcher(snapshot,direct);
  const parent=snapshot.byId.get(direct.parent);
  // A manually launched Debug process with a live parent is intentional as well.
  return {role,owner:"CHAP",pid,process:p,direct,manager,session,
    active:!!(session && liveHost(snapshot,session)) || !!manager || !!isChildOf(direct,parent)};
}
export function listeners(snapshot){
  return snapshot.ports.map(entry=>classify(snapshot,entry.port===5100?"backend":"frontend",entry.pid));
}
export function describe(item){
  const p=item.process;
  // Unknown command lines can contain positional credentials. Only print verified arguments.
  const command=item.owner==="CHAP"?[p.exe,...argsOf(p)].join(" "):(p?.name??"unavailable")+" [arguments withheld]";
  return item.role+" 127.0.0.1:"+ports[item.role]+" PID="+item.pid+" Process="+(p?.name??"unavailable")+
    " Path="+(p?.exe??"unavailable")+" Owner="+item.owner+" Command="+command;
}
export async function bindable(port){
  return new Promise(resolve=>{
    const s=net.createServer();
    s.once("error",()=>resolve(false));
    s.listen({host:"127.0.0.1",port,exclusive:true},()=>s.close(()=>resolve(true)));
  });
}
export function preflightPlan(snapshot){
  const items=listeners(snapshot);
  const blockers=items.filter(i=>i.owner!=="CHAP" || i.active);
  if(blockers.length)throw new Error("[PORT_IN_USE] "+blockers.map(describe).join("\n")+"\nExisting processes were preserved. Use npm.cmd run dev:status, or dev:stop for this checkout.");
  if(snapshot.records.some(s=>liveHost(snapshot,s)))throw new Error("[DEV_SESSION_ACTIVE] Development is starting or stopping. Use npm.cmd run dev:status.");
  return items;
}
export const sleep = ms=>new Promise(resolve=>setTimeout(resolve,ms));

export async function terminateVerified(item,explicit=false){
  const fresh=await inspect(undefined,[item.pid]);
  const actual=classify(fresh,item.role,item.pid);
  if(actual.owner!=="CHAP" || !same(item.process,actual.process) || (!explicit && actual.active))return;
  await windows("terminate",["-Target",String(item.pid),"-Birth",item.process.born]);
}
export async function waitForRelease(timeout=10000){
  const until=Date.now()+timeout;
  do {
    if((await Promise.all(Object.values(ports).map(bindable))).every(Boolean))return true;
    await sleep(140);
  }while(Date.now()<until);
  return false;
}
export async function preflight({read=inspect, terminate=terminateVerified, released=waitForRelease, remove=forget}={}){
  const snapshot=await read();
  const stale=preflightPlan(snapshot);
  for(const item of stale){
    console.log("[DEV_STALE] "+describe(item));
    await terminate(item);
  }
  if(!(await released(stale.length?6000:0)))throw new Error("[PORT_IN_USE] Cannot bind both 127.0.0.1:5100 and :3000. Use npm.cmd run dev:status.");
  for(const record of snapshot.records)if(!liveHost(snapshot,record))remove(record);
}

export function printStatus(snapshot){
  console.log("CHAP GITI Development Status");
  const current=listeners(snapshot);
  for(const [role,port] of Object.entries(ports)){
    const matches=current.filter(i=>i.role===role);
    if(!matches.length)console.log(role+" 127.0.0.1:"+port+" stopped (no listener)");
    for(const item of matches)console.log((item.owner==="CHAP"?"running ":"occupied-by-other-process ")+describe(item));
  }
  for(const record of snapshot.records)if(liveHost(snapshot,record))console.log("Development session "+record.id+" host PID="+record.host.pid);
}
export async function stopSession(){
  const snapshot=await inspect(), current=listeners(snapshot);
  const managed=snapshot.records.filter(s=>liveHost(snapshot,s));
  for(const s of managed){
    console.log("[DEV_STOP] Requesting graceful shutdown of "+s.id);
    try { await windows("signal",["-Session",s.id,"-Key",repoKey]); }
    catch(error){
      // A host discovered during Add-Type may not have created its stop event yet.
      if(liveHost(await inspect([s]),s))await windows("signal",["-Session",s.id,"-Key",repoKey]);
    }
  }
  for(const item of current){
    if(item.owner==="unknown"){console.error("[PORT_IN_USE] Preserved "+describe(item));continue;}
    if(!managed.some(s=>s.id===item.session?.id || same(s.worker,item.manager) || isChildOf(item.manager,snapshot.byId.get(s.host.pid)))){
      console.log("[DEV_STOP] Verified pre-existing listener "+describe(item));
      await terminateVerified(item,true);
    }
  }
  const deadline=Date.now()+16000;
  let final;
  do{
    final=await inspect();
    if(!managed.some(s=>liveHost(final,s)) && !listeners(final).some(i=>i.owner==="CHAP"))break;
    await sleep(200);
  }while(Date.now()<deadline);
  printStatus(final);
  if(managed.some(s=>liveHost(final,s)) || !(await waitForRelease(0)))throw new Error("[DEV_STOP] The two ports have not both been released, or the session is still stopping.");
  for(const s of snapshot.records)if(!liveHost(final,s))forget(s);
  console.log("Port 5100: free\nPort 3000: free\nDevelopment environment stopped successfully.");
}
