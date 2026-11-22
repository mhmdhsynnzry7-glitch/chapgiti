// Read-only extracted release verification. No application host or database is started.
import path from "node:path";
import {verifyDirectory} from "./verify-release.mjs";
const args = process.argv.slice(2);
if (args.length && (args.length !== 2 || args[0] !== "--release")) throw Error("Usage: node scripts/test-published.mjs [--release <directory>]");
console.log("PASS " + verifyDirectory(path.resolve(args[1] || ".")) + " staged release files verified offline.");
