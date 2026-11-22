import {existsSync as releaseFileExists} from "node:fs";
import {environment,buildBackend,cli} from "./dotnet-runtime.mjs";
const development=process.env.NODE_ENV!=="production"&&!releaseFileExists("RELEASE-MANIFEST.json");
if(development)await buildBackend(true);
await cli(["--migrate"],environment(development),{development});
