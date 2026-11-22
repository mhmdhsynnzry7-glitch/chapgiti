import path from "node:path";
import {buildBackend,tool} from "./dotnet-runtime.mjs";

await buildBackend(false);
await tool(process.execPath,["scripts/publish.mjs"],{cwd:path.resolve("frontend")});
