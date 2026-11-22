import {createHash} from "node:crypto";
import {lstatSync,readFileSync,readdirSync} from "node:fs";
import path from "node:path";
import {pathToFileURL} from "node:url";
import {spawnSync} from "node:child_process";

const privateExtensions = /\.(?:sqlite\d*(?:-.*)?|db|bak|trn|mdf|ndf|ldf|pdf|pfx|p12|pem|key|zip|log|pid|har|suo|user|pdb|tsbuildinfo)$/i;
const privateDirectories = new Set([".git", ".vs", ".idea", ".qa", ".codex", ".aws", "logs", "backups", "private", ".dev", ".dev-state", "testresults", "test-results", "playwright-report", "coverage"]);
const generatedDirectories = new Set(["node_modules", ".next", "bin", "obj", "publish", "outputs", "work", ".sites-runtime"]);

export function allowedReleaseFile(file, {source = false} = {}) {
  const parts = file.replaceAll("\\", "/").split("/").filter(Boolean);
  const name = parts.at(-1)?.toLowerCase() ?? "";
  if (parts.some(part => privateDirectories.has(part.toLowerCase()))) return false;
  if (source && parts.some(part => generatedDirectories.has(part.toLowerCase()))) return false;
  if (source && (parts.some(part => part.toLowerCase() === "data") || /(?:^|\/)properties\/launchsettings\.json$/i.test(parts.join("/")))) return false;
  if (name.startsWith(".env") && name !== ".env.example") return false;
  if (name.startsWith(".dev.vars") || /^appsettings\.(?:local|production)(?:\..+)?\.json$/i.test(name)) return false;
  if (!source && name === "appsettings.development.json") return false;
  if (privateExtensions.test(name)) return false;
  if (parts.join("/").match(/(?:^|\/)deploy\/iis\/(?:backups\/|[^/]+\.(?:exe|xml)$)/i)) return false;
  if (parts.join("/").match(/(?:^|\/)\.next\/cache(?:\/|$)/i)) return false;
  return true;
}

export function filesIn(folder, relative = "") {
  return readdirSync(path.join(folder, relative), {withFileTypes: true}).sort((a, b) => a.name.localeCompare(b.name)).flatMap(entry => {
    const name = path.posix.join(relative, entry.name);
    if (lstatSync(path.join(folder, name)).isSymbolicLink()) throw new Error("Package contains an unexpected symbolic link.");
    return entry.isDirectory() ? filesIn(folder, name) : [name];
  });
}

export const sha256 = data => createHash("sha256").update(data).digest("hex");
// Matches File.ReadAllText + UTF8 + CRLF-to-LF normalization in Migrations.Apply.
export const migrationChecksum = text => sha256(Buffer.from(text.replace(/^\uFEFF/, "").replaceAll("\r\n", "\n"), "utf8"));

export function migrationInventory(folder) {
  const names = readdirSync(folder).filter(name => name.toLowerCase().endsWith(".sql")).sort();
  if (names.length !== 9 || names.some((name, index) => !/^\d{3}_.+\.sql$/i.test(name) || !name.startsWith(String(index + 1).padStart(3, "0") + "_")))
    throw new Error("Expected the complete ordered migration chain SQL001-009. Later migrations belong to EF.");
  return names.map(name => ({name, checksum: migrationChecksum(readFileSync(path.join(folder, name), "utf8")),
    fileSha256: sha256(readFileSync(path.join(folder, name)))}));
}

export function fileHashes(folder) {
  return Object.fromEntries(filesIn(folder).map(name => [name, sha256(readFileSync(path.join(folder, name)))]));
}

export function secretCandidates(values) {
  const candidates = [];
  for (const [key, value] of Object.entries(values)) {
    if (typeof value !== "string") continue;
    if (/(?:PASSWORD|SECRET|API_KEY|TOKEN|CHAP_UAT_CUSTOMER_PHONE)/i.test(key) && value.length >= 6) candidates.push(value);
    if (/(?:CONNECTION_STRING|ConnectionStrings__)/i.test(key)) {
      const password = value.match(/(?:^|;)\s*(?:Password|Pwd)\s*=\s*(?:"([^"]*)"|'([^']*)'|([^;]*))/i);
      const secret = password?.slice(1).find(item => item !== undefined)?.trim();
      if (secret?.length >= 6) candidates.push(secret);
    }
  }
  return [...new Set(candidates)];
}

export async function configuredSecrets(root) {
  const {default: nextEnv} = await import(pathToFileURL(path.resolve(root,"frontend/node_modules/@next/env/dist/index.js")).href);
  const logger = {info(){},error(){throw Error("Cannot check local configuration for accidental secret copies.");}};
  const environment = {...process.env};
  try {
    return [...new Set([root,path.join(root,"frontend")].flatMap(dir=>secretCandidates(nextEnv.loadEnvConfig(dir,true,logger,true).combinedEnv)))];
  } finally {
    for(const key of Object.keys(process.env)) if(!(key in environment)) delete process.env[key];
    Object.assign(process.env,environment);
  }
}
export function scanPackage(folder, secrets = [], options = {}) {
  return scanFiles(folder, filesIn(folder), secrets, options);
}
export function scanFiles(folder, names, secrets, {source = false} = {}) {
  const problems = [];
  const patterns = [...new Set(secrets.flatMap(value => [value, JSON.stringify(value).slice(1, -1), encodeURIComponent(value)]))]
    .flatMap(value => [Buffer.from(value, "utf8"), Buffer.from(value, "utf16le")]);
  for (const name of names) {
    if (!allowedReleaseFile(name, {source})) problems.push({file: name, reason: "private file"});
    const bytes = readFileSync(path.join(folder, name));
    if (patterns.some(pattern => bytes.includes(pattern))) problems.push({file: name, reason: "configured private value"});
    if (/-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----\r?\n[A-Za-z0-9+/=]{20,}/.test(bytes.toString("latin1")))
      problems.push({file: name, reason: "private key material"});
  }
  return {filesScanned: names.length, passed: problems.length === 0, problems};
}

export function verifyArchive(zip, manifest) {
  const quote = value => "'" + value.replaceAll("'", "''") + "'";
  // Windows PowerShell's .NET Framework reader exposes nested FullName paths
  // with backslashes even when the ZIP central directory stores standard '/'.
  const command = `$ErrorActionPreference='Stop'; Add-Type -AssemblyName System.IO.Compression.FileSystem; ` +
    `$archive=[IO.Compression.ZipFile]::OpenRead(${quote(zip)}); try { ` +
    `$entries=[Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal); ` +
    `foreach($entry in $archive.Entries){$name=$entry.FullName.Replace([char]92,[char]47); if(-not $name.EndsWith('/')){$entries.Add($name,$entry)}}; ` +
    `$reader=[IO.StreamReader]::new($entries[${quote(manifest)}].Open()); try{$data=$reader.ReadToEnd()|ConvertFrom-Json}finally{$reader.Dispose()}; ` +
    `if($entries.Count -ne @($data.files.PSObject.Properties).Count+1){throw 'Archive file count differs from manifest.'}; ` +
    `foreach($item in $data.files.PSObject.Properties){if($item.Name.StartsWith('/') -or $item.Name.Contains([char]92) -or $item.Name.Split('/') -contains '..'){throw 'Unsafe archive entry.'}; ` +
    `$part=$entries[$item.Name]; if(-not $part){throw 'Archive entry missing.'}; $stream=$part.Open(); $hash=[Security.Cryptography.SHA256]::Create(); ` +
    `try{$actual=[BitConverter]::ToString($hash.ComputeHash($stream)).Replace('-','').ToLowerInvariant(); if($actual -ne $item.Value){throw 'Archive content checksum mismatch.'}}finally{$stream.Dispose();$hash.Dispose()}} ` +
    `}finally{$archive.Dispose()}`;
  const result = spawnSync("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", command], {encoding: "utf8", windowsHide: true});
  if (result.status !== 0) throw new Error("Archive manifest verification failed.");
  return true;
}
