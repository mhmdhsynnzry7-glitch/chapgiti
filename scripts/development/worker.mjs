import { createInterface } from "node:readline";
import { once } from "node:events";
import { spawn } from "node:child_process";
import path from "node:path";
import { environment, command, buildBackend, cli, frontendEnvironment } from "../dotnet-runtime.mjs";
import { root, inspect, identity, saveSession, sleep, bindable } from "./processes.mjs";

const id=process.argv[2];
const lines=createInterface({input:process.stdin});
const gate=await Promise.race([once(lines,"line").then(([line])=>line),once(lines,"close").then(()=>null)]);
lines.close();
if(gate!=="owned")process.exit(1);

let stopping=false;
for(const signal of ["SIGINT","SIGTERM","SIGBREAK"])process.on(signal,()=>{stopping=true;});
const state={root,id};
async function record(role,pid){
  const metadata=await inspect([], [pid,process.ppid]);
  const p=metadata.byId.get(pid);
  if(!p?.born)throw new Error("[DEV_INSPECTION] Unable to identify "+role+" PID "+pid+".");
  state[role]=identity(p);
  if(!state.host){
    const host=metadata.byId.get(process.ppid);
    state.host=identity(host);
    state.launcher=identity(metadata.byId.get(host?.parent));
    if(!state.host)throw new Error("[DEV_INSPECTION] Unable to identify the Development job host.");
  }
  saveSession(state);
}
function watch(child,role){
  const tag=role==="backend"?"BACKEND_EXIT":"FRONTEND_EXIT";
  function exit(reason){
    if(!stopping)process.stderr.write("["+tag+"] "+role+" exited ("+reason+"). See the original diagnostic above.\n");
    process.exit(stopping?0:1); // The host owns the whole job and drains its remaining children.
  }
  child.once("error",error=>exit(error.message));
  child.once("exit",(code,signal)=>exit(signal??code));
  return child;
}
try {
  await record("worker",process.pid);
  if(stopping)process.exit(0);
  const env=environment(true);
  if(env.DOTNET_API_URL!=="http://127.0.0.1:5100" || (env.PORT && env.PORT!=="3000"))
    throw new Error("[DEV_PORT_CONFIG] Main Development requires 127.0.0.1:5100 and port 3000.");
  await buildBackend(true,{privateConsole:true});
  if(stopping)process.exit(0);
  await cli(["--check","--main-development"],env,{development:true,privateConsole:true});
  if(stopping)process.exit(0);
  const api=command(true);
  const backend=watch(spawn(api.file,[...api.args,"--main-development"],{env,stdio:"inherit",windowsHide:false}),"backend");
  await record("backend",backend.pid);
  let ready=false;
  for(let attempt=0;!stopping && attempt<60;attempt++){
    try{
      const response=await fetch(env.DOTNET_API_URL+"/health",{signal:AbortSignal.timeout(1000)});
      const body=response.ok && await response.json();
      if(body.ok===true && body.backend==="ASP.NET Core"){ready=true;break;}
    }catch{}
    await sleep(500);
  }
  if(stopping)process.exit(0);
  if(!ready)throw new Error("[BACKEND_EXIT] ASP.NET Core /health did not become ready.");
  const snapshot=await inspect([state]);
  if(!snapshot.ports.some(p=>p.port===5100 && p.pid===backend.pid))
    throw new Error("[PORT_IN_USE] The health response did not come from the launched backend.");
  if(!(await bindable(3000)))throw new Error("[PORT_IN_USE] Frontend cannot bind 127.0.0.1:3000. Use npm.cmd run dev:status.");
  if(stopping)process.exit(0);
  const frontend=watch(spawn(process.execPath,[path.join(root,"frontend/node_modules/next/dist/bin/next"),"dev","--webpack","--hostname","127.0.0.1","--port","3000"],{
    cwd:path.join(root,"frontend"),env:{...frontendEnvironment(env),NODE_ENV:"development",PORT:"3000",HOSTNAME:"127.0.0.1"},stdio:"inherit",windowsHide:false
  }),"frontend");
  await record("frontend",frontend.pid);
  console.log("React / Next.js: http://127.0.0.1:3000 | ASP.NET Core + SQL Server: http://127.0.0.1:5100");
}catch(error){
  if(!stopping)console.error(error.message);
  process.exit(stopping?0:1);
}
