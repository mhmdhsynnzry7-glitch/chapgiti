import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import {
  createWriteStream,
  existsSync,
  mkdirSync,
  writeFileSync,
} from "node:fs";
import path from "node:path";
import { pathToFileURL } from "node:url";

const [api, base, artifactRoot] = process.argv.slice(2);
for (const origin of [api, base])
  assert.equal(new URL(origin).hostname, "127.0.0.1");
assert.match(artifactRoot, /^\.qa[\\/]ef-activation-[a-f0-9]{32}$/);
mkdirSync(artifactRoot, { recursive: true });
const environment = { ...process.env };
for (const key of Object.keys(environment))
  if (
    /^(SQLSERVER_|ConnectionStrings__|Storage__|Sms__|Otp__|OTP_|MELIPAYAMAK_|CHAP_UAT_|DATA_DIR$)/i.test(
      key,
    )
  )
    delete environment[key];
const output = createWriteStream(artifactRoot + "/next-browser.log");
const web = spawn(
  process.execPath,
  [
    path.resolve("frontend/node_modules/next/dist/bin/next"),
    "start",
    "--hostname",
    "127.0.0.1",
    "--port",
    new URL(base).port,
  ],
  {
    cwd: path.resolve("frontend"),
    windowsHide: true,
    stdio: ["ignore", "pipe", "pipe"],
    env: {
      ...environment,
      NODE_ENV: "production",
      CHAP_GITI_VERIFY_BUILD: "true",
      SITE_URL: base,
      DOTNET_API_URL: api,
      INDEXING_ENABLED: "false",
    },
  },
);
web.stdout.pipe(output);
web.stderr.pipe(output);
const runtime = [
  "frontend/node_modules/playwright/index.mjs",
  ".qa/browser-tools/node_modules/playwright-core/index.mjs",
].find(existsSync);
const { chromium } = await import(pathToFileURL(path.resolve(runtime)).href);
const checks = [],
  browserErrors = [];
function check(value, label) {
  assert.ok(value, label);
  checks.push(label);
  console.log("PASS " + label);
}
let browser;
try {
  let ready = false;
  for (let attempt = 0; attempt < 100; attempt++) {
    if (web.exitCode !== null)
      throw new Error("Isolated Next process exited before readiness.");
    try {
      ready = (
        await fetch(base + "/customer", { signal: AbortSignal.timeout(1500) })
      ).ok;
    } catch {}
    if (ready) break;
    await new Promise((resolve) => setTimeout(resolve, 200));
  }
  assert.ok(ready, "Isolated frontend readiness");
  browser = await chromium.launch({
    headless: true,
    executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe",
  });
  const context = await browser.newContext();
  await context.route("**/*", (route) =>
    route.request().url().startsWith(base) ? route.continue() : route.abort(),
  );
  const page = await context.newPage();
  page.setDefaultTimeout(12000);
  page.on("pageerror", (error) => browserErrors.push(error.message));
  await page.goto(base + "/calculator");
  await page.locator("input[name=phone]").fill("09100000001");
  await page.locator("input[name=password]").fill("Isolated.Sql@2026");
  await page.locator("form").getByRole("button",{name:"ورود",exact:true}).click();
  await page.waitForURL(url=>url.pathname==="/calculator");
  const selector = page.getByLabel("قطع کتاب", { exact: true });
  await selector.waitFor();
  check(
    (await selector.innerText()).includes("Activation SQL format"),
    "current SQL default and edited label reach the customer selector through the real Next proxy",
  );
  check(
    (await page
      .getByLabel("عرض کتاب (سانتی‌متر)", { exact: true })
      .inputValue()) === "15" &&
      (await page
        .getByLabel("طول کتاب (سانتی‌متر)", { exact: true })
        .inputValue()) === "22",
    "customer standard dimensions match the edited real SQL metadata",
  );

  await page.goto(base + "/admin/login");
  // This credential belongs only to OrderWorkflowFixtures; it is never loaded from .env.local.
  await page.locator("input[name=password]").fill("Isolated.Sql@2026");
  await page.locator("form").getByRole("button", { name: /ورود/ }).click();
  await page.waitForURL((url) => url.pathname === "/admin");
  async function request(method, route, body, expected = 200) {
    const response = await page.request.fetch(base + "/api/" + route, {
      method,
      headers: { Origin: base },
      ...(body ? { data: body } : {}),
    });
    assert.equal(response.status(), expected, method + " " + route);
    return response.json();
  }
  const body = {
    slug: "activation-browser-article",
    title: "راهنمای آزمایشی فعال‌سازی مجله",
    excerpt: "این خلاصه فقط برای پذیرش مستقل SQL و مرورگر است.",
    content:
      "## محتوای ایمن\n\nاین نوشتهٔ ساختگی فقط در دیتابیس مستقل آزمایشی ذخیره می‌شود.",
    category: "آزمون",
    status: "draft",
    coverAssetId: "",
    coverAlt: "",
    authorName: "",
    seoTitle: "",
    seoDescription: "",
  };
  const created = await request("POST", "admin/articles", body, 201);
  await request("GET", "public/articles/" + body.slug, undefined, 404);
  check(true, "real SQL draft stays private before publication");
  let article = await request("GET", "admin/articles/" + created.id);
  await request("PUT", "admin/articles/" + created.id, {
    ...article,
    status: "published",
  });
  await page.goto(base + "/blog/" + body.slug);
  await page
    .getByRole("heading", { level: 1, name: body.title, exact: true })
    .waitFor();
  check(
    true,
    "published real SQL article renders in the actual Next server-rendered page",
  );
  article = await request("GET", "admin/articles/" + created.id);
  await request("PUT", "admin/articles/" + created.id, {
    ...article,
    status: "archived",
  });
  await request("GET", "public/articles/" + body.slug, undefined, 404);
  const hidden = await page.goto(base + "/blog/" + body.slug);
  // Next's streamed notFound boundary may retain HTTP 200 after response headers are sent.
  // Assert the real privacy contract in addition to the API's strict 404 above.
  await page.waitForFunction(
    () =>
      !document.querySelector(".magazine-article") &&
      [...document.querySelectorAll('meta[name="robots"]')].some((meta) =>
        meta.content.includes("noindex"),
      ),
  );
  console.log("STREAMED_NOT_FOUND_HTTP_STATUS=" + hidden.status());
  check(
    [200, 404].includes(hidden.status()) &&
      (await page
        .getByRole("heading", { level: 1, name: body.title, exact: true })
        .count()) === 0 &&
      !(await page.locator("main").innerText()).includes("این نوشتهٔ ساختگی"),
    "archiving immediately removes the SQL article from public SSR without stale cache",
  );
  check(
    browserErrors.length === 0,
    "SQL-backed customer catalog and magazine pages have no browser runtime errors",
  );
  writeFileSync(
    artifactRoot + "/sql-browser-result.json",
    JSON.stringify(
      { checks, browserErrors, operationalAccess: false, realSms: false },
      null,
      2,
    ),
  );
} finally {
  await browser?.close();
  if (web.exitCode === null)
    await new Promise((resolve) =>
      spawn("taskkill.exe", ["/PID", String(web.pid), "/T", "/F"], {
        windowsHide: true,
        stdio: "ignore",
      }).once("exit", resolve),
    );
  output.end();
}
