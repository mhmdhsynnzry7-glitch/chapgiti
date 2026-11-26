import assert from "node:assert/strict";
import {spawn} from "node:child_process";
import {createRequire} from "node:module";
import {once} from "node:events";
import {readFileSync} from "node:fs";
import {createServer} from "node:net";
import path from "node:path";
import https from "node:https";
import http from "node:http";
import {setTimeout as delay} from "node:timers/promises";
import {pdfFixture} from "../pdf-fixture.mjs";
import {forwardUpgrade} from "../loopback-upgrade.mjs";
import {checkPackagedBrowser} from "./packaged-release-browser.mjs";

const root = process.cwd();
const manifest = JSON.parse(readFileSync(path.join(root,"RELEASE-MANIFEST.json"),"utf8"));
assert.equal(manifest.packageMode,"release-only");
assert.match(process.env.ConnectionStrings__SqlServer ?? "", /\(localdb\)\\ChapGiti005_[0-9a-f]{32}/i);
assert.match(process.env.ConnectionStrings__SqlServer, /Initial Catalog=ChapGiti_Acceptance005_[0-9a-f]{32}/i);
assert.equal(process.env.Sms__Enabled,"false");
const requireFrontend = createRequire(new URL("../../frontend/package.json", import.meta.url));
const {HubConnectionBuilder, HttpTransportType, LogLevel} = requireFrontend("@microsoft/signalr");
const processes = [];
let notificationHub;
async function origin() {
  const server=createServer();
  server.listen(0,"127.0.0.1");await once(server,"listening");
  const url=new URL("http://127.0.0.1:"+server.address().port);
  await new Promise(done=>server.close(done));
  return url;
}
function start(file,args,cwd,env) {
  const child=spawn(file,args,{cwd,env,windowsHide:true,stdio:["ignore","pipe","pipe"]});
  for (const stream of [child.stdout,child.stderr]) stream.resume();
  const ended=once(child,"close");
  processes.push({child,ended});
  return child;
}
async function ready(url,child) {
  for(let count=0;count<125;count++) {
    if(child.exitCode!==null) throw Error("Packaged process exited early: "+child.exitCode);
    try {const res=await fetch(url,{signal:AbortSignal.timeout(1500)});if(res.ok)return;} catch {}
    await delay(200);
  }
  throw Error("Packaged listener did not become healthy at "+url.pathname);
}
const front=await origin(),api=await origin();
assert.notEqual(front.port,api.port);
// Use the actual release's SignalR rewrite rule for both negotiation and WebSocket upgrades.
const iis = readFileSync(path.join(root, "deploy", "iis", "web.config"), "utf8");
const rule = iis.match(/<rule name="Giti SignalR"[^>]*>\s*<match url="([^"]+)"/);
assert.ok(rule, "packaged SignalR rewrite rule");
const hubPath = new RegExp(rule[1], "i");
const isHub = request => hubPath.test(new URL(request.url, "http://127.0.0.1").pathname.slice(1));
const tls=https.createServer({key:readFileSync(0),cert:readFileSync(process.env.NODE_EXTRA_CA_CERTS)},(incoming,response)=>{
  const proxy=http.request({hostname:front.hostname,port:isHub(incoming) ? api.port : front.port,path:incoming.url,method:incoming.method,headers:incoming.headers},remote=>{
    response.writeHead(remote.statusCode,remote.headers);
    remote.pipe(response);
  });
  proxy.on("error",()=>{if(!response.headersSent)response.writeHead(502);response.end();});
  incoming.pipe(proxy);
});
tls.on("upgrade", (request, client, head) => {
  if (isHub(request)) forwardUpgrade(request, client, head, api.port);
  else client.destroy();
});
tls.listen(0,"127.0.0.1");await once(tls,"listening");
const site=new URL("https://127.0.0.1:"+tls.address().port);
const env={...process.env,ASPNETCORE_URLS:api.origin,Site__Url:site.origin,SITE_URL:site.origin,DOTNET_API_URL:api.origin,INDEXING_ENABLED:"false",NEXT_TELEMETRY_DISABLED:"1"};
const frontEnv={NODE_ENV:"production",PATH:process.env.PATH,SystemRoot:process.env.SystemRoot,TEMP:process.env.TEMP,TMP:process.env.TMP,HOSTNAME:"127.0.0.1",PORT:front.port,SITE_URL:site.origin,DOTNET_API_URL:api.origin,INDEXING_ENABLED:"false",NEXT_TELEMETRY_DISABLED:"1"};
async function roleLogin(route,credentials) {
  const response=await fetch(new URL("/api/"+route,site),{method:"POST",headers:{Origin:site.origin,"Content-Type":"application/json"},body:JSON.stringify(credentials)});
  assert.equal(response.status,200,route);
  const issued=response.headers.getSetCookie();
  assert(issued.length>=2 && issued.every(cookie=>/;\s*secure\b/i.test(cookie) && /;\s*httponly\b/i.test(cookie)));
  const cookies=issued.map(v=>v.split(";")[0]).join("; ");
  assert(cookies.length>0);
  return {Origin:site.origin,Cookie:cookies};
}
async function jsonApi(route, headers, {method="GET", body, status=200}={}) {
  const options = {method, headers:{...headers}};
  if (body !== undefined) {
    if (body instanceof FormData) options.body=body;
    else {
      options.headers["Content-Type"]="application/json";
      options.body=JSON.stringify(body);
    }
  }
  const response = await fetch(new URL("/api/"+route, site), options);
  assert.equal(response.status, status, method+" "+route);
  return response.json();
}

async function orderJourney(customer, admin, other) {
  const spec = {pages:120,quantity:100,size:"raghi",paper:"white",color:"bw",binding:"soft"};
  const catalog = await jsonApi("admin/print-catalog",admin);
  const cut = catalog.items.find(row=>row.kind==="bookSizes" && row.code===spec.size && row.active);
  assert(cut,"missing seeded book size");
  const tariffPath = "admin/print-tariffs/"+cut.id;
  const tariff = await jsonApi(tariffPath,admin);
  if (tariff.isProposed) {
    await jsonApi(tariffPath,admin,{method:"PUT",body:{version:tariff.version,rates:tariff.rates,approveProposal:true}});
  }
  const estimate = await jsonApi("quote",customer,{method:"POST",body:spec});
  assert(estimate.total>0);
  const submission = new FormData();
  submission.set("payload",JSON.stringify({idempotencyKey:crypto.randomUUID(),termsAccepted:true,title:"سفارش آزمایشی بسته",service:"book-printing",spec}));
  submission.set("file",new Blob([pdfFixture("packaged order")],{type:"application/pdf"}),"order.pdf");
  const order = await jsonApi("orders",customer,{method:"POST",body:submission,status:201});
  assert.equal(order.estimate,estimate.total);
  const tracked = await jsonApi("track",customer,{method:"POST",body:{code:order.code}});
  assert.equal(tracked.order.id,order.id);
  assert(tracked.events.length>0);
  await jsonApi("track",other,{method:"POST",body:{code:order.code},status:404});
  console.log("PASS packaged estimate, initial order PDF, stored price, tracking and customer isolation.");
}

try {
  const backendDir=path.join(root,"backend","publish");
  const backend=start(path.join(backendDir,"ChapGiti.Api.exe"),[],backendDir,env);
  await ready(new URL("/health",api),backend);
  console.log("PASS packaged self-contained backend health on loopback.");
  const nextDir=path.join(root,"frontend");
  const next=start(process.execPath,[path.join(nextDir,"server.js")],nextDir,frontEnv);
  await ready(new URL("/",site),next);
  console.log("Packaged runtime "+JSON.stringify({backend:{pid:backend.pid,path:path.join(backendDir,"ChapGiti.Api.exe"),port:api.port},frontend:{pid:next.pid,path:path.join(nextDir,"server.js"),port:front.port},tlsPort:site.port}));
  for(const pathname of ["/","/services","/blog","/customer","/admin/login","/robots.txt"]) {
    const response=await fetch(new URL(pathname,site));assert.equal(response.status,200,pathname);
    const content=await response.text();
    assert(content.length>20);
    if (pathname!=="/robots.txt") assert.match(content,/<html[^>]*\blang=["']fa/i,pathname);
  }
  console.log("PASS installed standalone serves public, customer, administrator and robots routes.");
  const password="Isolated.Sql@2026";
  const customer=await roleLogin("customer/login",{phone:"09100000001",password});
  const admin=await roleLogin("auth/login",{username:"admin",password});
  const other=await roleLogin("customer/login",{phone:"09100000002",password});
  await orderJourney(customer,admin,other);

  // Use the same TLS origin and packaged rewrite rule as the browser.
  notificationHub = new HubConnectionBuilder()
    .withUrl(new URL("/api/realtime/admin/notifications",site).href,{headers:admin,transport:HttpTransportType.WebSockets})
    .configureLogging(LogLevel.None)
    .build();
  const changed=[];
  notificationHub.on("Changed",event=>changed.push(event));
  await notificationHub.start();
  await jsonApi("admin/notifications/unread-count",admin);
  const bytes=pdfFixture("release zip smoke");
  const id=await checkPackagedBrowser(site,password,bytes);
  const detail=new URL("/api/admin/tickets/"+id,site);
  const thread=await (await fetch(detail,{headers:admin})).json();
  assert.equal(thread.messages.length,1);const [attachment]=thread.messages[0].attachments;
  const url=new URL(detail.pathname+"/attachments/"+attachment.id,site);
  assert.equal((await fetch(url)).status,401);
  const download=await fetch(url,{headers:admin});assert.equal(download.status,200);assert(bytes.equals(Buffer.from(await download.arrayBuffer())));
  await jsonApi("tickets/"+id+"/attachments/"+attachment.id,other,{status:404});
  await jsonApi("admin/tickets/"+id,customer,{status:401});
  for(let i=0;!changed.length && i<100;i++) await delay(50);
  assert(changed.some(event=>event.kind==="notifications" && /^[a-f0-9]{64}$/.test(event.eventId)),"ticket notification did not arrive over WebSocket");
  const notices=await jsonApi("admin/notifications?pageSize=20",admin);
  assert(notices.items.some(item=>Object.values(item).some(value=>typeof value==="string" && value.includes(id))));
  assert((await jsonApi("admin/notifications/unread-count",admin)).unreadCount>0);
  console.log("PASS packaged WebSocket notification, persisted administrator inbox and unread count.");
  console.log("PASS real packaged proxy, independent logins, multipart create and protected byte-identical administrator download.");
} finally {
  if (notificationHub) await notificationHub.stop();
  tls.close();tls.closeAllConnections();
  for(const {child,ended} of processes.reverse()){if(child.exitCode===null) child.kill();await ended;}
}
