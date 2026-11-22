import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { pathToFileURL } from "node:url";
import path from "node:path";
import ts from "../frontend/node_modules/typescript/lib/typescript.js";
const optionsUrl = pathToFileURL(path.resolve("frontend/lib/book-options.ts")).href;
function moduleUrl(file, replacements = {}) {
  let code = ts.transpileModule(readFileSync(file, "utf8"), { compilerOptions: { module: ts.ModuleKind.ESNext, target: ts.ScriptTarget.ES2022 } }).outputText;
  for (const [from, to] of Object.entries(replacements)) code = code.replaceAll(`from "${from}"`, `from "${to}"`);
  return "data:text/javascript;base64," + Buffer.from(code).toString("base64");
}
const bookUrl = moduleUrl("frontend/lib/book-pricing.ts", { "./book-options": optionsUrl });
const defaultRates=JSON.parse(readFileSync(new URL("../scripts/legacy-print-rates.json",import.meta.url),"utf8"));
const { defaultSpec, quote } = await import(moduleUrl("frontend/lib/pricing.ts", { "./book-options": optionsUrl, "./book-pricing": bookUrl }));
const { detailsFromSpec, withBookDetails } = await import(bookUrl);
const { finishingServices } = await import(optionsUrl);
const { finishingUnavailable } = await import(moduleUrl("frontend/lib/finishing-compatibility.ts", { "./book-options": optionsUrl }));
let checks = 0;
function check(condition, name) { assert.ok(condition, name); checks++; }
const details = { ...detailsFromSpec(defaultSpec), bindingStyle: "hard-flat", coating: "matte" };
for (const [key, service] of Object.entries(finishingServices)) {
  check(!finishingUnavailable(key, details), key + " is available with compatible cover");
  for (const [patch, invalid] of [
    [{ printOnly: true }, true], [{ coverPrint: "none" }, true],
    [{ coating: "gloss" }, service.matte], [{ bindingStyle: "hot-glue" }, service.hard],
    [{ coverPrint: "plastic" }, service.matte],
  ]) check(!!finishingUnavailable(key, { ...details, ...patch }) === invalid, key + " dependency matches domain matrix");
  const spec = withBookDetails({ ...defaultSpec, quantity: 1 }, { ...details, extras: [key] });
  const original = quote(spec, defaultRates);
  const changed = quote(spec, { ...defaultRates, [key]: defaultRates[key] + 123 });
  check(changed.total - original.total === 123 && changed.extras - original.extras === 123, key + " admin price feeds frontend per-copy estimate without conversion");
  const scaled = quote(withBookDetails({ ...defaultSpec, size: "vaziri", quantity: 100 }, { ...details, extras: [key] }), defaultRates);
  check(scaled.finishing[0].unit === Math.ceil(defaultRates[key]), key + " keeps the same per-copy rate across cuts");
}
console.log(`${checks} finishing compatibility and frontend pricing checks passed.`);
