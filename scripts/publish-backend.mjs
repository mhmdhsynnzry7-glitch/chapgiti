import {spawnSync} from "node:child_process";
import path from "node:path";
import {fileURLToPath} from "node:url";
import {inputDigest, writeBuildReceipt} from "./release-source.mjs";

export function publishBackend() {
  const root=process.cwd(), output=path.join(root,"backend/publish");
  const before=inputDigest(root,"backend");
  const result=spawnSync("dotnet",["publish","backend/ChapGiti.Api/ChapGiti.Api.csproj","-c","Release","-r","win-x64",
    "--self-contained","true","-p:RuntimeFrameworkVersion=10.0.13","-o",output],{stdio:"inherit",windowsHide:true});
  if(result.status!==0) throw Error("Self-contained backend publish failed.",{cause:result.error});
  writeBuildReceipt(root,"backend",output,before);
}
if(process.argv[1] && path.resolve(process.argv[1])===fileURLToPath(import.meta.url)) publishBackend();
