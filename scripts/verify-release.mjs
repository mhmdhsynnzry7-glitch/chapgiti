import {readFileSync} from "node:fs";
import path from "node:path";
import {fileURLToPath} from "node:url";
import {allowedReleaseFile, filesIn, sha256} from "./release-files.mjs";
import {checkLayout} from "./release-contract.mjs";

export function verifyDirectory(root) {
  const manifestName = "RELEASE-MANIFEST.json";
  const manifest = JSON.parse(readFileSync(path.join(root,manifestName),"utf8"));
  if (manifest.packageMode !== "release-only") throw Error("Unsupported release manifest.");
  const actual = filesIn(root).filter(name => name !== manifestName);
  const expected = Object.keys(manifest.files).sort();
  if (JSON.stringify(actual.sort()) !== JSON.stringify(expected)) throw Error("Extra or missing files compared to release manifest.");
  for(const name of actual) {
    if(!allowedReleaseFile(name) || sha256(readFileSync(path.join(root,name)))!==manifest.files[name]) throw Error("Release integrity failed: "+name);
  }
  checkLayout(root,manifest);
  return actual.length;
}
if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url))
  console.log("PASS "+ verifyDirectory(process.cwd()) +" release file hashes and offline migration resources.");
