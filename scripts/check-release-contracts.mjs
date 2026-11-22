import {spawnSync} from "node:child_process";
import {mkdirSync, renameSync, writeFileSync} from "node:fs";
import {inputDigest} from "./release-source.mjs";
import {inspectCompiled, matchMetadata, sourceMigrations} from "./release-contract.mjs";

const env = {...process.env, SMS_ENABLED:"false", OTP_ENABLED:"false", DOTNET_NOLOGO:"1", NEXT_TELEMETRY_DISABLED:"1"};
for(const key of Object.keys(env)) if(/^(SQLSERVER_|ConnectionStrings__|CHAP_UAT_|MELIPAYAMAK_|Sms__|Otp__)/i.test(key)) delete env[key];
mkdirSync(".qa",{recursive:true});
const report=".qa/release-contracts.json";
function record(value) {
  const temp=report+"."+process.pid+".tmp";
  writeFileSync(temp,JSON.stringify(value,null,2)+"\n");
  renameSync(temp,report);
}
record({status:"RUNNING"});
const before = inputDigest(".");
function gate(executable,args,resultPattern) {
  const child = spawnSync(executable,args,{env,encoding:"utf8",windowsHide:true,maxBuffer:8*1024*1024});
  const summary = child.stdout?.match(resultPattern);
  if(child.status!==0 || !summary) throw Error("Offline release gate failed: "+args.join(" ")+"\n"+(child.stderr||child.stdout||child.error?.message||"").slice(-2000));
  console.log(summary[0]);
  return Number(summary[1]);
}
const checks = {
  packaging:gate(process.execPath,["tests/release-packaging.mjs"],/(\d+) release packaging checks passed/),
  iis:gate(process.execPath,["tests/deployment-release.mjs"],/(\d+) deployment configuration checks passed/),
  proxy:gate(process.execPath,["tests/release-proxy.mjs"],/(\d+) local proxy checks passed/),
  workflow:gate("dotnet",["run","--project","backend/tests/ChapGiti.AuthenticationTests","--configuration","Verification","--","--release-contracts"],/(\d+) in-memory release workflow checks passed; 0 suites failed/)
};
const compiled=inspectCompiled("dotnet",["backend/ChapGiti.Api/bin/Verification/net10.0/ChapGiti.Api.dll"]);
matchMetadata(sourceMigrations("."),compiled);
if(inputDigest(".")!==before) throw Error("Source changed during verification. Repeat the release gate.");
record({status:"PASS",database:"NOT_ACCESSED",sourceSha256:before,at:new Date().toISOString(),checks});
console.log("Compiled IDs and resource hashes match current sources. No database or archive was opened.");
