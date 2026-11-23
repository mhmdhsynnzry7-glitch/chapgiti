import {existsSync} from "node:fs";
import {checkInitializedFrontend} from "./initialize-frontend.mjs";
if (existsSync("RELEASE-MANIFEST.json")) {
  try { checkInitializedFrontend("frontend"); }
  catch { throw Error("Initialize the extracted frontend with node scripts/initialize-frontend.mjs before starting the service."); }
}
process.env.NODE_ENV="production";
const {stack}=await import("./dotnet-runtime.mjs");
await stack(false);
