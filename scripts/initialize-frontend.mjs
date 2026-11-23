import { randomBytes, randomUUID } from "node:crypto";
import { existsSync, linkSync, readFileSync, unlinkSync, writeFileSync } from "node:fs";
import path from "node:path";
import { isDeepStrictEqual } from "node:util";
import { fileURLToPath } from "node:url";

const props = ".next/server/preview-props.json";
const prerender = ".next/prerender-manifest.json";
const actions = ".next/server/server-reference-manifest.json";
export const frameworkManifests = [props, prerender, actions];
const previewFields = { previewModeId: 16, previewModeSigningKey: 32, previewModeEncryptionKey: 32 };

function read(file) { return JSON.parse(readFileSync(file, "utf8")); }
function encode(value) { return JSON.stringify(value, null, 2) + "\n"; }
function slots(name, data) { return name === prerender ? data.preview : data; }
function fields(name) { return name === actions ? ["encryptionKey"] : Object.keys(previewFields); }
function keysOf(name, data) {
  const keys = {};
  for (const field of fields(name)) {
    const value = slots(name, data)?.[field];
    const valid = typeof value === "string" && (name === actions
      ? /^[A-Za-z0-9+/]{43}=$/.test(value) && Buffer.from(value, "base64").length === 32
      : new RegExp("^[0-9a-f]{" + previewFields[field] * 2 + "}$").test(value));
    if (!valid) throw Error("Invalid Next.js installation material in " + name);
    keys[field] = value;
  }
  return keys;
}
function withoutKeys(name, data) {
  const cleaned = structuredClone(data);
  for (const field of fields(name)) delete slots(name, cleaned)[field];
  return cleaned;
}

// The archive transports the framework metadata. Only the destination creates its private keys.
export function sealFrontend(root) {
  const originals = frameworkManifests.map(name => read(path.join(root, name)));
  const keys = originals.map((data, i) => keysOf(frameworkManifests[i], data));
  if (!isDeepStrictEqual(keys[0], keys[1])) throw Error("Inconsistent Next.js preview metadata.");
  // Pre-rendered bound Server Actions can depend on the original encryption key.
  if ([originals[2].node, originals[2].edge].some(actions => !actions || Object.keys(actions).length))
    throw Error("Review Server Action key provisioning before packaging builds containing Server Actions.");
  for (const [i, name] of frameworkManifests.entries()) {
    const file = path.join(root, name);
    writeFileSync(file + ".template", encode(withoutKeys(name, originals[i])), {flag:"wx"});
    unlinkSync(file);
  }
  return [...Object.values(keys[0]), ...Object.values(keys[2])];
}

function template(root, name) {
  const file = path.join(root, name);
  const data = read(file + ".template");
  if (!slots(name, data) || fields(name).some(key => Object.hasOwn(slots(name, data), key)))
    throw Error("Unexpected private field in Next.js installation template: " + name);
  return data;
}
export function checkSealedFrontend(root) {
  for (const name of frameworkManifests) {
    if (existsSync(path.join(root, name))) throw Error("Private runtime manifest found in release: " + name);
    template(root, name);
  }
}
function validate(root, name) {
  const data = read(path.join(root, name));
  const keys = keysOf(name, data);
  if (!isDeepStrictEqual(withoutKeys(name, data), template(root, name)))
    throw Error("Installed Next.js metadata differs from the release: " + name);
  return keys;
}
export function checkInitializedFrontend(root) {
  if (!isDeepStrictEqual(validate(root, props), validate(root, prerender)))
    throw Error("Installed preview keys do not agree.");
  validate(root, actions);
}
function install(root, name, newKeys) {
  const dest = path.join(root, name);
  if (!existsSync(dest)) {
    const data = template(root, name);
    Object.assign(slots(name, data), newKeys());
    const temporary = dest + "." + randomUUID() + ".tmp";
    writeFileSync(temporary, encode(data), {flag:"wx", mode:0o600});
    try {
      // A concurrent installer either creates the whole file first or reads the winner.
      try { linkSync(temporary, dest); } catch (error) { if (error.code !== "EEXIST") throw error; }
    } finally { unlinkSync(temporary); }
  }
  return validate(root, name);
}
export function initializeFrontend(root) {
  const preview = install(root, props, () => Object.fromEntries(Object.entries(previewFields)
    .map(([name, bytes]) => [name, randomBytes(bytes).toString("hex")])));
  install(root, prerender, () => preview);
  install(root, actions, () => ({encryptionKey:randomBytes(32).toString("base64")}));
  checkInitializedFrontend(root);
}
if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const [option, ...extra] = process.argv.slice(2);
  if ((option && option !== "--check") || extra.length) throw Error("Usage: node scripts/initialize-frontend.mjs [--check]");
  const releaseRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
  if (!existsSync(path.join(releaseRoot, "RELEASE-MANIFEST.json"))) throw Error("Run this command from an extracted release.");
  const front = path.join(releaseRoot, "frontend");
  if (option === "--check") checkInitializedFrontend(front); else initializeFrontend(front);
  console.log("PASS private Next.js installation metadata is ready.");
}
