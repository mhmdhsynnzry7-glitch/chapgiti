import { randomUUID } from "node:crypto";
import { spawn } from "node:child_process";
import { root, repoKey, psArgs, inspect, printStatus, preflight, stopSession, forget } from "./development/processes.mjs";

async function launch(){
  const id=randomUUID();
  let host, stopping=false;
  function interrupt(){
    stopping=true;
    if(host && !host.stdin.destroyed)host.stdin.end("stop\n");
  }
  process.once("SIGINT",interrupt);
  process.once("SIGTERM",interrupt);
  process.once("SIGBREAK",interrupt);
  try {
    await preflight();
    if(stopping)return;
    host=spawn("powershell.exe",psArgs("host",["-Session",id,"-Key",repoKey,"-NodePath",process.execPath]),{
      cwd:root,windowsHide:true,stdio:["pipe","pipe","pipe"]
    });
    host.stdin.on("error",error=>{if(!["EPIPE","EOF","ECONNRESET"].includes(error.code))console.error("[DEV_HOST] "+error.code);});
    host.stdout.pipe(process.stdout,{end:false});host.stderr.pipe(process.stderr,{end:false});
    const code=await new Promise((resolve,reject)=>{host.once("error",reject);host.once("close",(code,signal)=>resolve(code??(stopping?0:1)));});
    if(code!==0)process.exitCode=code;
  }finally{
    if(host)forget({id});
    for(const signal of ["SIGINT","SIGTERM","SIGBREAK"])process.removeListener(signal,interrupt);
  }
}
try {
  const action=process.argv[2]||"start";
  if(action==="start" && process.env.CHAP_DEV_SANDBOX_ID)
    throw new Error("[DATABASE_CONFIG] This terminal selected a test sandbox. Open a fresh PowerShell in the project root and run npm.cmd run dev for ChapGiti.");
  if(process.platform!=="win32"){
    if(action!=="start")throw new Error("Development process inspection requires Windows.");
    const {stack}=await import("./dotnet-runtime.mjs");
    await stack(true,{requireMainDatabase:true});
  }else if(action==="status")printStatus(await inspect());
  else if(action==="stop")await stopSession();
  else if(action==="start")await launch();
  else throw new Error("Expected start, status or stop.");
}catch(error){console.error(error.message);process.exitCode=1;}
