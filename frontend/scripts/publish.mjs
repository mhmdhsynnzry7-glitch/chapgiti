import {spawn} from "node:child_process";
import {cpSync,existsSync,lstatSync,readFileSync,rmSync} from "node:fs";
import path from "node:path";
import {fileURLToPath} from "node:url";

const root=fileURLToPath(new URL("../",import.meta.url));
const output=path.resolve(root,"publish");
const {inputDigest, writeBuildReceipt} = await import("../../scripts/release-source.mjs");
const repo = path.resolve(root, "..");
const sourceBeforeBuild = inputDigest(repo, "frontend");
const standalone=path.join(root,".next","standalone");
const next=path.join(root,"node_modules","next","dist","bin","next");

await new Promise((resolve,reject)=>{
  const child=spawn(process.execPath,[next,"build","--webpack"],{
    cwd:root,env:{...process.env,NODE_ENV:"production",CHAP_GITI_VERIFY_BUILD:"false",SITE_URL:"https://chapgiti.ir",DOTNET_API_URL:"http://127.0.0.1:1"},stdio:"inherit",windowsHide:true
  });
  child.once("error",reject);
  child.once("exit",code=>code===0?resolve():reject(new Error("Next.js production build failed.")));
});
if(!existsSync(path.join(standalone,"server.js")) || readFileSync(path.join(standalone,".next","BUILD_ID"),"utf8") !== readFileSync(path.join(root,".next","BUILD_ID"),"utf8"))
  throw new Error("Next.js standalone output is missing.");

// Only replace this project's generated publish directory.
if(path.dirname(output)!==path.resolve(root)||path.basename(output)!=="publish")
  throw new Error("Unexpected frontend publish directory.");
if(existsSync(output)){
  if(lstatSync(output).isSymbolicLink())
    throw new Error("Refusing to replace a linked publish directory.");
  rmSync(output,{recursive:true,force:true});
}
const allowed=file=>!path.basename(file).toLowerCase().startsWith(".env");
cpSync(standalone,output,{recursive:true,dereference:true,filter:allowed});
cpSync(path.join(root,"public"),path.join(output,"public"),{recursive:true,filter:allowed});
cpSync(path.join(root,".next","static"),path.join(output,".next","static"),{recursive:true,filter:allowed});
writeBuildReceipt(repo, "frontend", output, sourceBeforeBuild);
console.log("Frontend published: "+output);
console.log("Contains server.js, runtime dependencies, public assets and .next/static; personal env files are excluded.");
