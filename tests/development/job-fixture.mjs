import net from "node:net";
import {spawn} from "node:child_process";
import {once} from "node:events";
import {createInterface} from "node:readline";
import {fileURLToPath} from "node:url";
const script=fileURLToPath(import.meta.url);
if(process.argv[2]==="leaf"){
  const socket=net.createServer();socket.listen(0,"127.0.0.1");await once(socket,"listening");
  process.send(socket.address().port);process.on("message",()=>process.exit(7));
  process.on("SIGINT",()=>socket.close(()=>process.exit(0)));
}else if(process.argv[2]==="frontend"){
  const leaf=spawn(process.execPath,[script,"leaf"],{stdio:["ignore","inherit","inherit","ipc"]});
  leaf.on("message",port=>process.send(port));process.on("message",()=>process.exit(7));
}else{
  const lines=createInterface({input:process.stdin});
  const [gate]=await once(lines,"line");lines.close();if(gate!=="owned")process.exit(1);
  if(process.env.CHAP_TEST_SCENARIO==="pending-migration"){
    // Model the read-only --check process failing before either service can start.
    const guard=spawn(process.execPath,["-e","console.error('[MIGRATION_REQUIRED] Pending EF migration (fixture)');process.exitCode=1"],{stdio:"inherit"});
    const [status]=await once(guard,"close");
    console.error("[BACKEND_CLI] --check exited ("+status+").");
    process.exit(status);
  }
  const children=["leaf","frontend"].map(role=>spawn(process.execPath,[script,role],{stdio:["ignore","inherit","inherit","ipc"]}));
  for(const c of children)c.on("exit",code=>process.exit(code??1));
  const ports=await Promise.all(children.map(c=>once(c,"message").then(([p])=>p)));
  console.log("FIXTURE_READY "+ports.join(" "));
  const which=process.env.CHAP_TEST_SCENARIO;
  if(which.endsWith("fails"))setTimeout(()=>children[which==="backend-fails"?0:1].send("fail"),400);
}
