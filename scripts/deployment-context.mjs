// The extracted release keeps its runtime dependencies inside frontend/.
const {default: nextEnv} = await import(new URL("../frontend/node_modules/@next/env/dist/index.js", import.meta.url).href);
import { writeFileSync } from "node:fs";
nextEnv.loadEnvConfig(process.cwd());
if (!process.argv[2]) throw new Error("Provide a destination for deployment context.");
const port = process.env.PORT || "3000";
if (!/^\d{2,5}$/.test(port) || Number(port) > 65535) throw new Error("Invalid PORT.");
writeFileSync(process.argv[2], JSON.stringify({ port, siteUrl: process.env.SITE_URL || "" }));
