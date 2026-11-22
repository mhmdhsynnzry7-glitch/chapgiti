import assert from "node:assert/strict";
import {spawnSync} from "node:child_process";
import {existsSync} from "node:fs";
import path from "node:path";

// Rejection cases only. No credentials or reachable remote SQL endpoints are used.
const configuration = process.env.CHAP_TEST_BUILD_CONFIGURATION || "Debug";
assert.ok(["Debug", "Release", "Verification"].includes(configuration), "Use a local test build configuration.");
const dll = path.resolve("backend/ChapGiti.Api/bin", configuration, "net10.0/ChapGiti.Api.dll");
assert.ok(existsSync(dll), "Build the selected API configuration first.");
for (const [mode,connection] of [
  ["Development", "Server=.;Database=master;Integrated Security=true"],
  ["Development", "Server=tcp:127.0.0.1,1;Database=ChapGiti;Integrated Security=true"],
  ["Development", "Server=.;Database=ChapGiti;AttachDbFilename=invalid.mdf"],
  ["Development", "Server=.;Database=ChapGiti;Failover Partner=127.0.0.1,1"],
  ["Development", "unknown-key=invalid"],
  ["Production", "Server=.;Database=ChapGiti;Integrated Security=true"],
]) {
  const check = spawnSync("dotnet",[dll,"--check","--main-development"],{
    env:{...process.env,DOTNET_ENVIRONMENT:mode,ASPNETCORE_ENVIRONMENT:mode,ConnectionStrings__SqlServer:connection},
    // This deadline includes cold CLR startup on a Windows build host.
    encoding:"utf8",windowsHide:true,timeout:30000,
  });
  assert.ifError(check.error);
  assert.equal(check.status,1);
  assert.match(check.stderr,/\[DATABASE_CONFIG\]/);
}
const stale = spawnSync(process.execPath,["scripts/dev-dotnet.mjs"],{
  env:{...process.env,CHAP_DEV_SANDBOX_ID:"obsolete"},encoding:"utf8",windowsHide:true,
});
assert.equal(stale.status,1);
assert.match(stale.stderr,/fresh PowerShell/);
console.log("7 main Development target rejection checks passed.");
