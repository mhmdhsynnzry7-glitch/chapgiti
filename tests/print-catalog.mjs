import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { pathToFileURL } from "node:url";
import { createRequire } from "node:module";
import path from "node:path";
import ts from "../frontend/node_modules/typescript/lib/typescript.js";
import {
  catalogOptions,
  catalogDefaults,
  catalogInitialSpec,
  attachCatalogIds,
} from "../frontend/lib/print-catalog.ts";
import { publicLink } from "../frontend/lib/magazine.ts";
const require = createRequire(import.meta.url),
  url = (file) => pathToFileURL(path.resolve(file)).href;
function moduleUrl(file, replacements = {}) {
  let code = ts.transpileModule(readFileSync(file, "utf8"), {
    compilerOptions: {
      module: ts.ModuleKind.ESNext,
      target: ts.ScriptTarget.ES2022,
    },
  }).outputText;
  for (const [from, to] of Object.entries(replacements))
    code = code.replaceAll(`from "${from}"`, `from "${to}"`);
  return "data:text/javascript;base64," + Buffer.from(code).toString("base64");
}
const optionsUrl = url("frontend/lib/book-options.ts"),
  book = moduleUrl("frontend/lib/book-pricing.ts", {
    "./book-options": optionsUrl,
  });
const pricing = moduleUrl("frontend/lib/pricing.ts", {
  "./book-options": optionsUrl,
  "./book-pricing": book,
});
const { quoteBook } = await import(book),
  defaultRates = JSON.parse(readFileSync(new URL("../scripts/legacy-print-rates.json",import.meta.url),"utf8")),
  legacy = await import(optionsUrl);
const { validatePrintSpec } = await import(
  moduleUrl("frontend/lib/print-spec.ts", {
    "./book-options": optionsUrl,
    "./pricing": pricing,
    zod: url(require.resolve("zod", { paths: [path.resolve("frontend")] })),
  })
);
const items = JSON.parse(
  readFileSync(
    "backend/ChapGiti.Infrastructure/Persistence/Ef/InitialPrintCatalog.json",
    "utf8",
  ),
).map((i) => ({
  ...i,
  version: 1,
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: "2026-01-01T00:00:00Z",
}));
const options = catalogOptions(items),
  spec = catalogDefaults(items, options);
const changedDefaults = items.map((i) =>
  i.kind === "bookSizes" ? { ...i, isDefault: i.code === "vaziri" } : i,
);
const changedOptions = catalogOptions(changedDefaults);
let checks = 0;
const check = (value, label) => {
  assert.ok(value, label);
  checks++;
};
check(
  catalogInitialSpec(changedDefaults, changedOptions).size === "vaziri",
  "new orders honor the latest SQL default instead of a server-side constant",
);
const pageOverlay = catalogInitialSpec(changedDefaults, changedOptions, {
  pages: 200,
});
check(
  pageOverlay.size === "vaziri" &&
    pageOverlay.details.bwPages + pageOverlay.details.colorPages === 200,
  "legacy page-count links overlay SQL defaults without reverting catalog selections",
);
check(
  items.length === 80 && new Set(items.map((i) => i.id)).size === 80,
  "catalog transfers 80 unique deterministic identities",
);
for (const group of Object.keys(options)) {
  check(
    Object.keys(options[group]).length === Object.keys(legacy[group]).length,
    "all legacy " + group + " meanings transfer",
  );
  check(
    items.filter((i) => i.kind === group && i.isDefault && i.active).length ===
      1,
    "active default for " + group,
  );
}
check(
  validatePrintSpec(spec, options).details.bwPages === 120,
  "fresh forms use configured defaults and valid technical counts",
);
for (const [code, format] of Object.entries(options.bookSizes)) {
  const value = {
    ...spec,
    size: code,
    details: { ...spec.details, width: format.width, height: format.height },
  };
  check(
    quoteBook(value, defaultRates, options).total ===
      quoteBook(value, defaultRates).total,
    "format " + code + " preserves legacy pricing",
  );
}
for (const [code, mode] of Object.entries(options.textPrints)) {
  const value = {
    ...spec,
    color: mode.bw ? "bw" : "color",
    details: {
      ...spec.details,
      printType: code,
      bwPages: mode.bw ? 120 : 0,
      colorPages: mode.bw ? 0 : 120,
    },
  };
  check(
    quoteBook(value, defaultRates, options).total ===
      quoteBook(value, defaultRates).total,
    "print mode " + code + " preserves its original factors",
  );
}
for (const code of Object.keys(options.bookPapers)) {
  const value = {
    ...spec,
    details: { ...spec.details, bwPaper: code, colorPaper: code },
  };
  check(
    quoteBook(value, defaultRates, options).total ===
      quoteBook(value, defaultRates).total,
    "paper " + code + " preserves its original formula",
  );
}
for (const [code, binding] of Object.entries(options.bookBindings)) {
  const value = {
    ...spec,
    binding: binding.base,
    details: { ...spec.details, bindingStyle: code },
  };
  check(
    quoteBook(value, defaultRates, options).total ===
      quoteBook(value, defaultRates).total,
    "binding " + code + " preserves its original price",
  );
}
for (const group of ["coverPrints", "coverPapers", "coverCoatings"]) {
  const field = {
    coverPrints: "coverPrint",
    coverPapers: "coverPaper",
    coverCoatings: "coating",
  }[group];
  for (const code of Object.keys(options[group])) {
    const value = { ...spec, details: { ...spec.details, [field]: code } };
    check(
      quoteBook(value, defaultRates, options).total ===
        quoteBook(value, defaultRates).total,
      group + " " + code + " preserves its original factors",
    );
  }
}
for (const code of Object.keys(options.finishingServices)) {
  const value = {
    ...spec,
    binding: "hard",
    details: {
      ...spec.details,
      bindingStyle: "hard-flat",
      coating: "matte",
      extras: [code],
    },
  };
  check(
    quoteBook(value, defaultRates, options).total ===
      quoteBook(value, defaultRates).total,
    "finishing " + code + " preserves its tariff association",
  );
}
const newFormat = {
  ...items.find((i) => i.kind === "bookSizes" && i.code === "vaziri"),
  id: "00000000-1111-2222-3333-444444444444",
  code: "new-sql-size",
  label: "قطع پویای تازه",
  metadata: { ...options.bookSizes.vaziri, width: 18, height: 25 },
};
const changed = catalogOptions([...items, newFormat]),
  newSpec = {
    ...spec,
    size: newFormat.code,
    details: { ...spec.details, width: 18, height: 25 },
  };
check(
  validatePrintSpec(newSpec, changed).size === newFormat.code,
  "a supported new SQL code requires no frontend constant change",
);
assert.throws(() => validatePrintSpec(newSpec));
checks++;
const selected = attachCatalogIds(newSpec, [...items, newFormat]);
check(
  Object.keys(selected.catalogIds).length === 7 &&
    selected.catalogIds["bookSizes:new-sql-size"] === newFormat.id,
  "requests send only selected stable catalog identities",
);
const disabled = catalogOptions([...items, { ...newFormat, active: false }]);
assert.throws(() => validatePrintSpec(newSpec, disabled));
checks++;
const bounds = catalogOptions(
  items.map((i) =>
    i.kind === "bookSizes" && i.code === "custom"
      ? {
          ...i,
          metadata: {
            ...i.metadata,
            minWidth: 12,
            maxWidth: 20,
            minHeight: 12,
            maxHeight: 24,
          },
        }
      : i,
  ),
);
assert.throws(() =>
  validatePrintSpec(
    {
      ...spec,
      size: "custom",
      details: { ...spec.details, width: 21, height: 22 },
    },
    bounds,
  ),
);
checks++;
for (const target of [
  "javascript:alert(1)",
  "//external.invalid/path",
  "/\\external.invalid/path",
  "https://user:password@example.test",
  "data:text/html,unsafe",
]) {
  check(
    !publicLink(target),
    "unsafe article target is inert: " + target.split(":")[0],
  );
}
check(
  publicLink("/blog") && publicLink("https://example.test/path"),
  "safe internal and HTTPS article links remain functional",
);
const drafts = JSON.parse(readFileSync("docs/editorial/print-guides-fa.json", "utf8"));
check(drafts.length === 4 && new Set(drafts.map(a=>a.slug)).size === 4, "four distinct editorial slugs");
for (const article of drafts) {
  check(article.status === "draft" && !article.author && /^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(article.slug), "unpublished CMS draft with a stable slug");
  check(article.title.length >= 5 && article.excerpt.length >= 10 && article.category && article.content.includes("## جمع‌بندی"), "draft has metadata, sections and a conclusion");
  check(article.editorialSources.every(url=>publicLink(url) && url.startsWith("https://")) && article.content.includes(article.editorialSources.at(-1)), "reviewed technical references also appear in the article");
}
console.log(
  checks +
    " SQL catalog parity/dynamic selection/content safety frontend checks passed; no network, SQL or SMS used.",
);
