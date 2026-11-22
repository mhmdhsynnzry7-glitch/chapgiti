import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {pathToFileURL} from "node:url";
import {createRequire} from "node:module";
import path from "node:path";
import ts from "../frontend/node_modules/typescript/lib/typescript.js";

const require = createRequire(import.meta.url);
function moduleUrl(file, replacements = {}) {
  let source = ts.transpileModule(readFileSync(file, "utf8"), {compilerOptions: {
    module: ts.ModuleKind.ESNext, target: ts.ScriptTarget.ES2022,
  }}).outputText;
  for (const [from, to] of Object.entries(replacements)) source = source.replaceAll(`from "${from}"`, `from "${to}"`);
  return "data:text/javascript;base64," + Buffer.from(source).toString("base64");
}
const options = pathToFileURL(path.resolve("frontend/lib/book-options.ts")).href;
const book = moduleUrl("frontend/lib/book-pricing.ts", {"./book-options": options});
const pricing = moduleUrl("frontend/lib/pricing.ts", {"./book-options": options, "./book-pricing": book});
const zod = name => pathToFileURL(require.resolve(name, {paths: [path.resolve("frontend")]})).href;
const {printSpecSchema, validatePrintSpec} = await import(moduleUrl("frontend/lib/print-spec.ts", {
  "./pricing": pricing, "./book-options": options, "zod": zod("zod"),
}));
const {defaultSpec} = await import(pricing);
const {withBookDetails, detailsFromSpec} = await import(book);
const {finishingServices} = await import(options);
const {connectorResponse} = await import(moduleUrl("frontend/lib/connector-errors.mts", {"zod/v4": zod("zod/v4")}));

let checks = 0;
const check = (ok, message) => {assert.ok(ok, message); checks++;};
const basic = {...defaultSpec, pages: 120, quantity: 100};
const complete = withBookDetails(basic, detailsFromSpec(basic));
check(printSpecSchema.safeParse(complete).success, "complete valid print specifications remain accepted after the security upgrade");
for (const [pages, quantity] of [["120", "100"], ["۱۲۰", "۱۰۰"], ["١٢٠", "١٠٠"]]) {
  const result = validatePrintSpec({...basic, pages, quantity});
  check(result.pages === 120 && result.quantity === 100, "English, Persian and Arabic digits retain the same numeric contract");
}
for (const pages of [3, 2001, 4.5, "1e2", "۱۲٫۵", "-1", "NaN"]) {
  assert.throws(() => validatePrintSpec({...basic, pages}), /مجموع صفحات هر جلد باید یک عدد صحیح بین ۴ و ۲۰۰۰ باشد/); checks++;
}
for (const quantity of [0, 10001, 1.5, "-1"]) {
  assert.throws(() => validatePrintSpec({...basic, quantity}), /تیراژ باید یک عدد صحیح بین ۱ و ۱۰٬۰۰۰ باشد/); checks++;
}
for (const [pages, quantity] of [[4, 1], [2000, 10000]])
  check(validatePrintSpec({...basic, pages, quantity}).pages === pages, "existing inclusive quantity/page bounds remain valid");
assert.throws(() => validatePrintSpec({...complete, details: {...complete.details, bwPages: 119, colorPages: 0}}), /جمع صفحات رنگی و سیاه و سفید معتبر نیست/); checks++;
const extra = Object.keys(finishingServices)[0];
assert.throws(() => validatePrintSpec({...complete, details: {...complete.details, extras: [extra, extra]}}), /خدمات تکمیلی تکراری است/); checks++;
const clean = validatePrintSpec({...complete, password: "synthetic private field", phone: "synthetic phone", registrationToken: "synthetic token"});
check(!("password" in clean) && !("phone" in clean) && !("registrationToken" in clean), "the print-spec allowlist still removes private unrelated fields");

// GHSA-mrhx-c7xc-m3w5: a constrained oversized collection must fail before
// traversing elements. A sparse array avoids allocating a large fixture.
let visited = 0;
const oversized = new Array(100_000);
Object.defineProperty(oversized, 0, {get() {visited++; throw new Error("Oversized elements must not be traversed");}});
const rejected = printSpecSchema.safeParse({...complete, details: {...complete.details, extras: oversized}});
check(!rejected.success && rejected.error.issues.some(issue => issue.code === "too_big" && issue.path.includes("extras")), "oversized finishing collections return a bounded validation error");
check(visited === 0, "the patched validator rejects an oversized collection before reading its elements");

const response = connectorResponse({status: "success", result: {content: [{type: "text", text: "synthetic response"}]}});
check(response.status === 200 && (await response.json()).status === "success", "the existing zod/v4 response contract still accepts confirmed JSON");
const failed = connectorResponse({status: "provider_error", message: "synthetic failure"});
check(failed.status === 200 && (await failed.json()).status === "provider_error", "structured upstream failures retain their existing response semantics");
const malformed = connectorResponse({status: "success", result: {content: "invalid"}});
check(malformed.status === 502 && malformed.headers.get("Cache-Control") === "private, no-store", "malformed upstream responses remain unconfirmed and uncached");
console.log(`${checks} print-spec security and schema compatibility checks passed; no SQL, network or SMS used.`);
