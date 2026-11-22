import assert from "node:assert/strict";
import http from "node:http";
import {createHash, randomBytes} from "node:crypto";
import {spawnSync} from "node:child_process";
import {once} from "node:events";

const rendered = spawnSync("powershell.exe",["-NoProfile","-NonInteractive","-ExecutionPolicy","Bypass","-Command",
  ". ./deploy/iis/proxy-config.ps1; [xml]$doc=New-GitiProxyConfiguration -TemplatePath (Resolve-Path deploy/iis/web.config); $doc.configuration.'system.webServer'.rewrite.rules.rule[1].match.url"],{encoding:"utf8",windowsHide:true});
assert.equal(rendered.status,0,rendered.stderr);
const hub = new RegExp(rendered.stdout.trim(),"i");
const origin = "https://chapgiti.ir";
const cookie = "giti_customer_access=synthetic-test-cookie";
let checks=0;
const upstream = http.createServer((req,res) => {
  res.setHeader("Content-Type","application/json");
  res.end(JSON.stringify({path:req.url,method:req.method,cookie:req.headers.cookie,origin:req.headers.origin}));
});
const next = http.createServer((_req,res)=>res.end("next"));
upstream.on("upgrade",(req,socket)=>{
  const accept=createHash("sha1").update(req.headers["sec-websocket-key"]+"258EAFA5-E914-47DA-95CA-C5AB0DC85B11").digest("base64");
  socket.end("HTTP/1.1 101 Switching Protocols\r\nConnection: Upgrade\r\nUpgrade: websocket\r\nSec-WebSocket-Accept: "+accept+"\r\nX-Fixture-Path: "+req.url+"\r\n\r\n");
});
const listen=async server=>{server.listen(0,"127.0.0.1");await once(server,"listening");return server.address().port;};
const back=await listen(upstream), front=await listen(next);
const target = url => hub.test(new URL(url,origin).pathname.slice(1)) ? back : front;
const proxy=http.createServer((req,res)=>{
  const relay=http.request({host:"127.0.0.1",port:target(req.url),path:req.url,method:req.method,headers:req.headers},incoming=>{
    res.writeHead(incoming.statusCode,incoming.headers);incoming.pipe(res);
  });
  relay.on("error",()=>res.destroy());req.pipe(relay);
});
proxy.on("upgrade",(req,socket,head)=>{
  const relay=http.request({host:"127.0.0.1",port:target(req.url),path:req.url,headers:req.headers});
  relay.on("upgrade",(incoming,remote,extra)=>{
    socket.write("HTTP/1.1 101 Switching Protocols\r\n"+Object.entries(incoming.headers).map(([key,value])=>key+": "+value).join("\r\n")+"\r\n\r\n");
    if(head.length) remote.write(head);if(extra.length) socket.write(extra);
    remote.pipe(socket);socket.pipe(remote);
    socket.on("error",()=>remote.destroy());remote.on("error",()=>socket.destroy());
  });
  relay.on("response",()=>socket.destroy());relay.on("error",()=>socket.destroy());relay.end();
});
try {
  const port=await listen(proxy),base="http://127.0.0.1:"+port;
  for (const scope of ["customer","admin"]) for(const kind of ["notifications","support"]) {
    const url="/api/realtime/"+scope+"/"+kind;
    const headers={Cookie:cookie,Origin:origin};
    const path=url+"/negotiate?negotiateVersion=1";
    const result=await (await fetch(base+path,{method:"POST",headers,signal:AbortSignal.timeout(4000)})).json();
    assert.deepEqual(result,{path,method:"POST",cookie,origin});checks++;
    const transport=url+"?id=fixture";
    const poll=await (await fetch(base+transport,{method:"DELETE",headers,signal:AbortSignal.timeout(4000)})).json();
    assert.equal(poll.path,transport);assert.equal(poll.method,"DELETE");checks++;
    await new Promise((resolve,reject)=>{
      const req=http.get(base+transport,{headers:{...headers,Connection:"Upgrade",Upgrade:"websocket","Sec-WebSocket-Key":randomBytes(16).toString('base64'),"Sec-WebSocket-Version":"13"},signal:AbortSignal.timeout(4000)});
      req.on("upgrade",(response,socket)=>{socket.destroy();try {assert.equal(response.statusCode,101);assert.equal(response.headers["x-fixture-path"],transport);resolve();} catch(e){reject(e);}});
      req.on("response",response=>{response.resume();reject(Error("Upgrade not forwarded"));});req.on("error",reject);
    });
    checks++;
  }
  for(const path of ["/admin","/api/orders"]){assert.equal(await (await fetch(base+path,{signal:AbortSignal.timeout(4000)})).text(),"next");checks++;}
} finally {
  await Promise.all([proxy,next,upstream].map(server=>new Promise(resolve=>{server.close(resolve);server.closeAllConnections();})));
}
console.log(checks+" local proxy checks passed (rendered rule, loopback HTTP/Upgrade fixtures; live IIS not exercised).");
