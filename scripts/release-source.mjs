import { existsSync, lstatSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { requireSourcePaths } from "./release-contract.mjs";
import { fileURLToPath } from "node:url";
import { allowedReleaseFile, configuredSecrets, scanFiles, sha256 } from "./release-files.mjs";

const top = [".gitignore", ".npmrc", ".env.example", "AGENTS.md", "README.md", "package.json", "package-lock.json", ".config/dotnet-tools.json"];
const projects = ["Api", "Application", "Domain", "Infrastructure"].map(name => "backend/ChapGiti." + name);
const front = ["app", "components", "hooks", "lib", "public", "scripts"].map(name => "frontend/" + name);
const frontConfig = ["package.json", "package-lock.json", "next.config.ts", "postcss.config.mjs", "tsconfig.json", "eslint.config.mjs"];
const skills = ["backend", "frontend", "database", "testing"].map(area => ".agents/skills/chap-" + area + "/SKILL.md");
const optionalBuildConfig = ["global.json", "NuGet.Config", "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props",
  "backend/Directory.Build.targets", "backend/Directory.Packages.props", "backend/NuGet.Config", "backend/global.json"];

function inputs(root, role) {
  const backend = [...projects, "backend/.editorconfig", "backend/Directory.Build.props", "backend/ChapGiti.slnx"];
  const frontend = [...front, ...frontConfig.map(name => "frontend/" + name)];
  const config = optionalBuildConfig.filter(name => existsSync(path.join(root, name)));
  const recipe = ["scripts/release-files.mjs","scripts/release-source.mjs"];
  if (role === "backend") return [...backend, ...config, ...recipe, "scripts/publish-backend.mjs"];
  if (role === "frontend") return [...frontend, ...recipe];
  return [...top, ...skills, ...backend, ...frontend, ...config, "backend/tests", "backend/README.md", "frontend/AGENTS.md", "frontend/README.md", "frontend/.env.example",
    "scripts", "tests", "deploy/iis", "deploy/sql", "docs"];
}

export function sourceFiles(root = ".", role = "all") {
  const result = {};
  function visit(relative) {
    if (!allowedReleaseFile(relative, {source:true}) || /^(?:docs\/(?:qa|screenshots)|tests\/test-results)(?:\/|$)/i.test(relative)) return;
    const file = path.join(root, relative);
    const info = lstatSync(file);
    if (info.isSymbolicLink()) throw Error("Linked source input: " + relative);
    if (info.isDirectory()) {
      for (const entry of readdirSync(file).sort()) visit(relative + "/" + entry);
    } else if (info.isFile()) result[relative] = sha256(readFileSync(file));
  }
  for (const input of inputs(root, role).sort()) visit(input);
  if (role === "all") requireSourcePaths(result);
  return result;
}

export function sourceInventory(root, gitRevision) {
  const files = sourceFiles(root);
  function paths(args) {
    const output = execFileSync("git", ["ls-files", "-z", ...args], {cwd:root, encoding:"utf8", windowsHide:true});
    return new Set(output.split("\0").filter(Boolean));
  }
  const tracked = paths(["--cached"]);
  const other = paths(["--others", "--exclude-standard"]);
  const untracked = Object.keys(files).filter(name => !tracked.has(name)).sort();
  for (const name of untracked) {
    if (!other.has(name)) throw Error("Ignored release source requires review: " + name);
  }
  return {gitRevision, files, untracked};
}

export const inputDigest = (root, role) => sha256(Buffer.from(JSON.stringify(sourceFiles(root, role))));
function outputDigest(output) {
  const list = {};
  function visit(dir) {
    for (const entry of readdirSync(path.join(output,dir),{withFileTypes:true}).sort((a,b)=>a.name.localeCompare(b.name,"en"))) {
      const name=path.posix.join(dir,entry.name);
      if (name === "CHAP-BUILD.json" || !allowedReleaseFile(name)) continue;
      if (entry.isSymbolicLink()) throw Error("Linked publish output: "+name);
      if (entry.isDirectory()) visit(name);
      else list[name]=sha256(readFileSync(path.join(output,name)));
    }
  }
  visit("");
  return sha256(Buffer.from(JSON.stringify(list)));
}
export function writeBuildReceipt(root, role, output, before) {
  const current = inputDigest(root, role);
  if (current !== before) throw Error("Source changed during " + role + " publish. Repeat the build.");
  writeFileSync(path.join(output, "CHAP-BUILD.json"), JSON.stringify({role, sourceSha256:current,outputSha256:outputDigest(output)}) + "\n");
}
export function requireBuildReceipt(root, role, output) {
  const receipt = JSON.parse(readFileSync(path.join(output, "CHAP-BUILD.json"), "utf8"));
  if (receipt.role !== role || receipt.sourceSha256 !== inputDigest(root, role) || receipt.outputSha256 !== outputDigest(output)) throw Error("Stale " + role + " output. Publish it again from current sources.");
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const files = sourceFiles();
  if (process.argv[2] === "--git-pathspec" && process.argv.length === 3) {
    const scan=scanFiles(".",Object.keys(files),await configuredSecrets("."),{source:true});
    if(!scan.passed) throw Error("Private material in candidate Git paths: "+scan.problems.map(p=>p.file).join(", "));
    mkdirSync(".qa",{recursive:true});
    writeFileSync(".qa/release-source-paths.nul",Object.keys(files).sort().join("\0")+"\0");
    console.log("Wrote "+Object.keys(files).length+" literal source paths to .qa/release-source-paths.nul.");
  } else if (process.argv.length === 2) {
    console.log(JSON.stringify({sourceSha256:sha256(Buffer.from(JSON.stringify(files))), files}, null, 2));
  } else throw Error("Usage: node scripts/release-source.mjs [--git-pathspec]");
}
