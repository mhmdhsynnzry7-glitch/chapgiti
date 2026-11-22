const bundledEnv=new URL("../frontend/node_modules/@next/env/dist/index.js",import.meta.url);
const {default:nextEnv}=await import(bundledEnv.href);
import {spawn} from "node:child_process";
import path from "node:path";
import {existsSync} from "node:fs";
export const project=path.resolve("backend/ChapGiti.Api/ChapGiti.Api.csproj");
export function environment(development=false){
  // Both spellings are supported. An explicit process selection must win over
  // either spelling in dotenv, even when it was set to an invalid empty value.
  const explicitSql = process.env.SQLSERVER_CONNECTION_STRING ?? process.env.ConnectionStrings__SqlServer;
  nextEnv.loadEnvConfig(process.cwd(),development);
  const sql = explicitSql ?? process.env.SQLSERVER_CONNECTION_STRING ?? process.env.ConnectionStrings__SqlServer;
  if (!sql?.trim()) throw new Error("[DATABASE_CONFIG] Set SQLSERVER_CONNECTION_STRING to the intended existing catalog. See docs/CHAP-DEV-STARTUP-AND-MIGRATION-READINESS-FA.md.");
  if (explicitSql !== undefined) process.env.SQLSERVER_CONNECTION_STRING = explicitSql;
  const url=new URL(process.env.DOTNET_API_URL||"http://127.0.0.1:5100");
  if(!["http:","https:"].includes(url.protocol)||!["127.0.0.1","localhost","[::1]"].includes(url.hostname)||url.pathname!=="/"||url.username||url.password||url.search||url.hash)throw new Error("DOTNET_API_URL must be a loopback origin.");
  const mode=development?"Development":"Production";
  const env={...process.env,ASPNETCORE_ENVIRONMENT:mode,DOTNET_ENVIRONMENT:mode,ASPNETCORE_URLS:url.origin,
    Site__Url:process.env.SITE_URL||"http://127.0.0.1:3000",Site__TrustProxy:process.env.TRUST_IIS_PROXY||"false",
    // IIS forwards notification and support WebSockets directly while preserving the public Host.
    // Keep host filtering restricted to loopback and the explicitly configured site.
    AllowedHosts:process.env.AllowedHosts||["127.0.0.1","localhost",new URL(process.env.SITE_URL||"http://127.0.0.1:3000").hostname].join(";"),
    Storage__Root:path.resolve(process.env.DATA_DIR||"data"),Sms__Enabled:process.env.SMS_ENABLED||"false",
    Sms__Username:process.env.MELIPAYAMAK_USERNAME||"",Sms__Password:process.env.MELIPAYAMAK_PASSWORD||"",
    Sms__From:process.env.MELIPAYAMAK_FROM||"",Sms__BodyId:process.env.MELIPAYAMAK_BODY_ID||"",
    Otp__Enabled:process.env.OTP_ENABLED||"false",
    Otp__VerificationEnabled:process.env.OTP_VERIFICATION_ENABLED??"true",
    Otp__ApiKey:process.env.MELIPAYAMAK_CONSOLE_API_KEY||"",
    Otp__CodeLength:process.env.OTP_CODE_LENGTH||"4",
    Otp__LifetimeSeconds:process.env.OTP_LIFETIME_SECONDS||"120",
    DOTNET_API_URL:url.origin};
  env.ConnectionStrings__SqlServer=sql;
  env.SQLSERVER_CONNECTION_STRING=sql;
  return env;
}
export function frontendEnvironment(env){
  const webEnv = {...env};
  for (const key of Object.keys(webEnv)) if (/^(SQLSERVER_|ConnectionStrings__|Storage__|Sms__|Otp__|OTP_|MELIPAYAMAK_|CHAP_UAT_|DATA_DIR$)/i.test(key)) delete webEnv[key];
  return webEnv;
}
export function command(development=false){
  const published=path.resolve("backend/publish/ChapGiti.Api.exe");
  const dll=path.resolve("backend/publish/ChapGiti.Api.dll");
  if(!development&&existsSync(published))return {file:published,args:[]};
  if(!development&&existsSync(dll))return {file:"dotnet",args:[dll]};
  if(!development)throw new Error("Published .NET backend missing. Run npm run build.");
  return {file:"dotnet",args:[path.resolve("backend/ChapGiti.Api/bin/Debug/net10.0/ChapGiti.Api.dll")]};
}
export function tool(file,args,options={}){
  return new Promise((resolve,reject)=>{
    const child=spawn(file,args,{stdio:"inherit",windowsHide:true,...options});
    child.once("error",reject);child.once("exit",(code,signal)=>code===0?resolve():reject(new Error(file+" failed ("+(code??signal)+").")));
  });
}
export async function buildBackend(development=false,{privateConsole=false}={}){

  try {
    if (!development) {
      const {publishBackend} = await import("./publish-backend.mjs");
      return publishBackend();
    }
    await tool("dotnet",["build",project,...(privateConsole?["--disable-build-servers"]:[])],{windowsHide:!privateConsole});
  } catch (cause) {
    throw new Error(`[BACKEND_BUILD] ${cause.message}`, {cause});
  }
}
export async function cli(args,env,{input,development=false,privateConsole=false}={}){
  const backend=command(development);
  return new Promise((resolve,reject)=>{
    const child=spawn(backend.file,[...backend.args,...args],{env,stdio:[input===undefined?"inherit":"pipe","inherit","inherit"],windowsHide:!privateConsole});
    if(input!==undefined)child.stdin.end(input);
    child.once("error",reject);
    child.once("close",(code,signal)=>{
      if(code===0) return resolve();
      const hint = development && args[0]==="--check"
        ? " See the backend diagnostic above and docs/CHAP-DEV-STARTUP-AND-MIGRATION-READINESS-FA.md for local Development startup." : "";
      reject(new Error(`[BACKEND_CLI] ${args[0]} exited (${code??signal}).${hint}`));
    });
  });
}
export async function stop(child){
  if(!child?.pid||child.exitCode!==null)return;
  if(process.platform==="win32")await tool("taskkill.exe",["/PID",String(child.pid),"/T","/F"],{stdio:"ignore"}).catch(()=>{});
  else child.kill("SIGTERM");
}
export async function stack(development=false, {requireMainDatabase=false}={}){
  const env=environment(development);
  if(development)await buildBackend(true);
  // Starting the app must not apply pending migrations to the operational database.
  const guard = requireMainDatabase ? ["--main-development"] : [];
  await cli(["--check", ...guard],env,{development});
  const site=new URL(env.Site__Url);
  if(!development&&site.protocol==="http:"&&!["127.0.0.1","localhost","[::1]"].includes(site.hostname)&&env.ALLOW_HTTP!=="true")throw new Error("Set ALLOW_HTTP=true for the configured HTTP deployment.");
  const children=[];let shuttingDown=false;
  async function shutdown(code=0){if(shuttingDown)return;shuttingDown=true;await Promise.all(children.map(stop));process.exitCode=code;}
  const backend=command(development);
  const api=spawn(backend.file,[...backend.args, ...guard],{env,stdio:"inherit",windowsHide:true});children.push(api);
  api.once("error",error=>{console.error(error.message);void shutdown(1);});
  function unexpectedExit(component,code,signal) {
    if(shuttingDown)return;
    console.error(`[RUNTIME_EXIT] ${component} stopped unexpectedly (${signal??code}).`);
    void shutdown(code || 1);
  }
  api.once("exit",(code,signal)=>unexpectedExit("ASP.NET Core",code,signal));
  try{
    let ready=false;
    for(let attempt=0;attempt<60;attempt++){
      if(shuttingDown||api.exitCode!==null||api.signalCode)throw new Error("[RUNTIME_STARTUP] ASP.NET Core exited before /health was ready.");
      try{
        const response=await fetch(env.DOTNET_API_URL+"/health",{signal:AbortSignal.timeout(1000)});
        const status=response.ok && await response.json();
        ready=status.ok===true && status.backend==="ASP.NET Core";
      }catch{}
      if(ready)break;await new Promise(resolve=>setTimeout(resolve,500));
    }
    if(!ready)throw new Error("[RUNTIME_STARTUP] ASP.NET Core /health did not become ready. Check the backend listener and database diagnostics above.");
    const port=env.PORT||"3000";
    const standalone=["frontend/server.js","frontend/publish/server.js","frontend/.next/standalone/server.js"].map(file=>path.resolve(file)).find(file=>existsSync(file));
    const args=development?["node_modules/next/dist/bin/next","dev","--webpack","--hostname","127.0.0.1","--port",port]:[standalone];
    if(!development&&(!standalone||!existsSync(standalone)))throw new Error("Standalone Next.js frontend missing. Run npm run build.");
    const webEnv=frontendEnvironment(env);
    const frontend=spawn(process.execPath,args,{env:{...webEnv,NODE_ENV:development?"development":"production",PORT:port,HOSTNAME:"127.0.0.1"},stdio:"inherit",windowsHide:true,cwd:development?path.resolve("frontend"):process.cwd()});
    children.push(frontend);
    frontend.once("error",error=>{console.error(error.message);void shutdown(1);});
    frontend.once("exit",(code,signal)=>unexpectedExit("Next.js",code,signal));
    process.once("SIGINT",()=>void shutdown());process.once("SIGTERM",()=>void shutdown());
    console.log("React / Next.js frontend: "+env.Site__Url+" | ASP.NET Core + SQL Server: "+env.DOTNET_API_URL);
  }catch(error){await shutdown(1);throw error;}
}
