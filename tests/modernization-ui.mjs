import { checkSignedInName } from "./customer-estimate-ui.mjs";
import { publicIndexingChecks } from "./public-indexing-ui.mjs";
import { workflowModernizationChecks } from "./workflow-modernization-ui.mjs";
import { finalUatChecks } from "./final-uat-ui.mjs";
import { requestToPrintChecks } from "./request-to-print-ui.mjs";
import {catalogMagazineChecks} from "./catalog-magazine-ui.mjs";
import { notificationChecks } from "./notifications-ui.mjs";
import { realtimeChecks } from "./realtime-ui.mjs";
import { orderRevisionChecks } from "./order-revisions-ui.mjs";
﻿import { independentAuthenticationChecks } from "./independent-authentication-ui.mjs";
import { customerExperienceChecks } from "./customer-experience-ui.mjs";
import { passwordRecoveryChecks } from "./password-recovery-ui.mjs";
import { businessChecks } from "./business-ui.mjs";
import { workflowSupportChecks } from "./workflow-support-ui.mjs";
import { nameLayoutChecks, passwordLabelChecks } from "./registration-form-ui.mjs";
import { accessibilityResponsiveChecks } from "./accessibility-responsive-ui.mjs";
import { servicesCmsChecks, bannerContactChecks, publicCleanupChecks, paginationChecks } from "./cms-v2-ui.mjs";
import assert from "node:assert/strict";
import { browserPort } from "./browser-port.mjs";
import path from "node:path";
import { existsSync, mkdirSync, createWriteStream, writeFileSync, readFileSync } from "node:fs";
import { pathToFileURL } from "node:url";
import { spawn } from "node:child_process";

const root = process.cwd();
const requestWorkflow = process.argv.includes("--request-workflow");
const orderRevisions = process.argv.includes("--order-revisions");
const catalogMagazine=process.argv.includes("--catalog-magazine");
const notifications = process.argv.includes("--notifications");
const realtime = process.argv.includes("--realtime");
const otpBypass = process.argv.includes("--otp-bypass");
const cmsPhase = process.argv.find(arg => arg.startsWith("--cms-phase="))?.split("=")[1];
mkdirSync(".qa", { recursive: true });
if (requestWorkflow) mkdirSync(".qa/request-to-print", { recursive: true });
if (notifications) mkdirSync(".qa/notifications", { recursive: true });
if (realtime) mkdirSync(".qa/signalr", { recursive: true });
if (cmsPhase) mkdirSync(".qa/cms-v2", { recursive: true });
assert.ok(existsSync("frontend/.next/BUILD_ID"), "Run the non-publishing frontend verification build first.");
assert.notEqual(JSON.parse(readFileSync("frontend/.next/required-server-files.json", "utf8")).config.output, "standalone", "Use CHAP_GITI_VERIFY_BUILD=true for the local verification build.");
const runtime = ["frontend/node_modules/playwright/index.mjs", ".qa/browser-tools/node_modules/playwright-core/index.mjs"].find(existsSync);
assert.ok(runtime, "Install the frontend dev dependencies (Playwright) before running UI tests.");
const { chromium } = await import(pathToFileURL(path.resolve(runtime)).href);
const chrome = process.env.CHROME_PATH || "C:/Program Files/Google/Chrome/Application/chrome.exe";
assert.ok(existsSync(chrome), "Set CHROME_PATH to an installed Chrome/Chromium executable.");
const checks = [], children = [], logs = [], browserErrors = [];
const check = (condition, name) => { assert.ok(condition, name); checks.push(name); console.log("PASS " + name); };
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
function start(file, args, options, name) {
  const logDirectory = process.env.CHAP_BROWSER_EVIDENCE || ".qa";
  mkdirSync(logDirectory, { recursive: true });
  const output = createWriteStream(path.join(logDirectory, "modernization-" + name + ".log")); logs.push(output);
  const child = spawn(file, args, { windowsHide: true, stdio: ["ignore", "pipe", "pipe"], ...options });
  child.stdout.pipe(output); child.stderr.pipe(output); children.push(child); return child;
}
async function stop(child) {
  if (!child?.pid || child.exitCode !== null) return;
  if (process.platform === "win32") await new Promise(resolve => spawn("taskkill.exe", ["/PID", String(child.pid), "/T", "/F"], { windowsHide: true, stdio: "ignore" }).once("exit", resolve));
  else child.kill("SIGTERM");
}
async function ready(url, child) {
  const deadline = Date.now() + 60_000;
  let lastResult = "no response";
  while (Date.now() < deadline) {
    if (child.exitCode !== null || child.signalCode) {
      throw new Error("Test process exited before becoming ready: " + (child.exitCode ?? child.signalCode));
    }
    try {
      // Allow the first server render to finish while the overall readiness deadline stays bounded.
      const remaining = Math.max(1, deadline - Date.now());
      const response = await fetch(url, { signal: AbortSignal.timeout(Math.min(15_000, remaining)) });
      lastResult = "HTTP " + response.status;
      await response.body?.cancel();
      if (response.ok) return;
    } catch (error) {
      lastResult = error.cause?.code || error.cause?.message || error.message;
    }
    await sleep(250);
  }
  throw new Error("Test process did not become ready: " + url + " (" + lastResult + ")");
}

const port = await browserPort(), base = "http://127.0.0.1:" + port;
const cleanEnv = { ...process.env };
for (const key of Object.keys(cleanEnv)) if (/^(SQLSERVER_|ConnectionStrings__|Storage__|Sms__|Otp__|OTP_|MELIPAYAMAK_|DATA_DIR$)/i.test(key)) delete cleanEnv[key];
// Keep browser verification output separate from a developer's running Debug API.
const build = start("dotnet", ["build", "backend/tests/ChapGiti.AuthenticationTests", "--configuration", "Verification", "--verbosity", "quiet"], { cwd: root, env: cleanEnv }, "ui-backend-build");
assert.equal(await new Promise(resolve => build.once("exit", resolve)), 0, "The test backend must compile.");
let browser, page, apiOrigin;
try {
  const backend = start("dotnet", ["backend/tests/ChapGiti.AuthenticationTests/bin/Verification/net10.0/ChapGiti.AuthenticationTests.dll", "--serve-ui", ...(otpBypass ? ["--otp-bypass"] : [])], { cwd: root, env: { ...cleanEnv, CHAP_GITI_TEST_SITE_URL: base } }, "ui-backend");
  apiOrigin = await new Promise((resolve, reject) => {
    let output = "";
    const timer = setTimeout(() => reject(new Error("Memory-only API did not announce its origin.")), 30_000);
    backend.stdout.on("data", chunk => {
      output += chunk;
      const match = output.match(/TEST_API_ORIGIN=(http:\/\/127\.0\.0\.1:\d+)/);
      if (match) { clearTimeout(timer); resolve(match[1]); }
    });
    backend.once("exit", code => { clearTimeout(timer); reject(new Error("Memory API exited: " + code)); });
  });
  const control = async values => { const response = await fetch(apiOrigin + "/__test/control", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(values) }); assert.ok(response.ok); return response.json(); };
  const web = start(process.execPath, [path.resolve("frontend/node_modules/next/dist/bin/next"), "start", "--hostname", "127.0.0.1", "--port", String(port)], { cwd: path.resolve("frontend"), env: { ...cleanEnv, NODE_ENV: "production", CHAP_GITI_VERIFY_BUILD: "true", SITE_URL: base, DOTNET_API_URL: apiOrigin, INDEXING_ENABLED: process.argv.includes("--public-indexing") ? "true" : "false" } }, "ui-frontend");
  await ready(base + "/customer", web);
  browser = await chromium.launch({ headless: true, executablePath: chrome });
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
  await context.route("**/*", route => route.request().url().startsWith(base) ? route.continue() : route.abort());
  page = await context.newPage(); page.on("pageerror", error => browserErrors.push(error.message));
  const adminPhone = "09000000826", signupPhone = "09122222222", createdPhone = "09133333333", password = "Old.Test.Password@1";
  async function login(target, phone, destination = "/account", secret = password) {
    await target.goto(base + "/customer?change=1&next=" + encodeURIComponent(destination));
    await target.locator("input[name=phone]").fill(phone);
    await target.locator("input[name=password]").fill(secret);
    await target.locator("form").getByRole("button", { name: "ورود", exact: true }).click();
    await target.waitForURL(url => url.pathname === destination.split("?")[0]);
  }
  async function adminLogin(target, destination = "/admin/customers") {
    await target.goto(base + "/admin/login?next=" + encodeURIComponent(destination));
    await target.locator("input[name=username]").waitFor();
    await target.locator("input[name=password]").fill(password);
    await target.getByRole("button", { name: "ورود به مدیریت", exact: true }).click();
    await target.waitForURL(url => url.pathname === destination);
  }
  async function logout(target) {
    const status = await target.evaluate(async () => (await fetch("/api/customer/logout", { method: "POST" })).status);
    assert.equal(status, 200);
    await target.goto(base + "/customer?change=1");
  }
  async function fillRegistration(target) {
    await target.locator("input[name=phone]").fill(signupPhone);
    check(await target.locator("input[name=firstName],input[name=password]").count() === 0, "registration collects only the mobile before OTP");
  }
  async function completeRegistrationProfile(target) {
    await target.locator("input[name=firstName]").waitFor();
    await passwordLabelChecks(target, check, "initial");
    await nameLayoutChecks(target, check, "registration/profile completion");
    await target.locator("input[name=firstName]").fill("کاربر");
    await target.locator("input[name=lastName]").fill("جدید");
    let completions = 0;
    const countCompletion = request => { if (request.url().endsWith("/api/customer/registration/complete") && request.method() === "POST") completions++; };
    target.on("request", countCompletion);
    try {
      await target.locator("input[name=password]").fill("abc!12");
      await target.locator("input[name=confirmation]").fill("abc!12");
      await target.getByRole("button", { name: "تکمیل ثبت‌نام", exact: true }).click();
      check(completions === 0 && await target.locator("input[name=password]").evaluate(input => input.validity.patternMismatch) &&
        await target.locator("input[name=password]").getAttribute("aria-invalid") === "true",
        "registration still rejects a password without an uppercase letter before sending a completion request");
    } finally { target.off("request", countCompletion); }
    await target.locator("input[name=password]").fill(password);
    await target.locator("input[name=confirmation]").fill(password);
    await target.getByRole("button", { name: "تکمیل ثبت‌نام", exact: true }).click();
    await target.getByRole("heading", { name: "ثبت‌نام تکمیل شد؛ وارد شوید" }).waitFor();
  }

  async function completeRegistrationLogin(target, destination) {
    await target.locator(".auth-success").getByRole("button", { name: "ورود", exact: true }).click();
    await target.locator("input[name=password]").waitFor();
    check(await target.locator("input[name=phone]").inputValue() === signupPhone, "registration success opens the login form in place with the verified phone preserved");
    await target.locator("input[name=password]").fill(password);
    await target.locator("form").getByRole("button", { name: "ورود", exact: true }).click();
    await target.waitForURL(url => url.pathname === destination);
  }

  if(process.argv.includes("--public-indexing")) {
    await login(page,adminPhone); await adminLogin(page,"/admin/articles");
    await publicIndexingChecks({page,base,check});
  } else if(process.argv.includes("--workflow-modernization")){
    await login(page,adminPhone);await adminLogin(page,"/admin/settings");
    await workflowModernizationChecks({page,browser,base,check,control});
  } else if(process.argv.includes("--final-uat")){
    await login(page,adminPhone);
    await adminLogin(page,"/admin/print-settings");
    await finalUatChecks({page,base,check});
  } else if(catalogMagazine){
    await login(page,adminPhone);await adminLogin(page,"/admin/print-settings");
    await catalogMagazineChecks({page,base,check,control});
  } else if (orderRevisions) {
    await login(page, adminPhone); await adminLogin(page, "/admin/orders");
    await orderRevisionChecks({page,base,check,control});
  } else if (realtime) {
    await login(page, adminPhone); await adminLogin(page, "/admin");
    await realtimeChecks({browser,page,base,check,control});
  } else if (notifications) {
    await login(page, adminPhone); await adminLogin(page, "/admin");
    await notificationChecks({browser,page,base,check,control});
  } else if (requestWorkflow) {
    await login(page, adminPhone); await adminLogin(page, "/admin/orders");
    await requestToPrintChecks({browser,page,base,check,control});
  } else if (cmsPhase) {
    await login(page, adminPhone);
    await adminLogin(page, "/admin/services");
    await servicesCmsChecks({page,base,check,control});
    if(Number(cmsPhase)>=3)await bannerContactChecks({page,base,check});
    if(Number(cmsPhase)>=4)await publicCleanupChecks({page,base,check});
    if(Number(cmsPhase)>=6)await paginationChecks({page,base,check,control});
  } else if (otpBypass) {
    await control({ smsAvailable: false });
    await page.goto(base + "/customer?change=1&next=" + encodeURIComponent("/calculator"));
    await page.getByRole("button", { name: "ثبت‌نام", exact: true }).click();
    await fillRegistration(page);
    await page.getByRole("button", { name: "ادامه ثبت‌نام", exact: true }).click();
    const input = page.getByLabel("کد تأیید", { exact: true });
    await input.waitFor();
    check(await input.getAttribute("maxlength") === "4", "development bypass keeps the existing four-digit registration form");
    check((await page.locator(".auth-notice").innerText()).includes("پیامکی ارسال نمی‌شود"), "development mode explains that SMS was not sent without changing the UI steps");
    await input.fill("9876");
    await page.getByRole("button", { name: "تأیید کد و ادامه", exact: true }).click();
    await completeRegistrationProfile(page);
    check((await context.cookies()).every(cookie => !cookie.name.startsWith("giti_")), "arbitrary registration code succeeds without passwordless authentication");
    await completeRegistrationLogin(page, "/calculator");
    check(new URL(page.url()).pathname === "/calculator", "the existing password login returns to the original destination after bypass registration");
    await logout(page);
    await page.getByRole("link", { name: "رمز عبور را فراموش کرده‌ام", exact: true }).click();
    await page.getByRole("heading", { name: "بازیابی رمز عبور", exact: true }).waitFor();
    await page.locator("input[name=phone]").fill(adminPhone);
    await page.getByRole("button", { name: "دریافت کد تأیید", exact: true }).click();
    await page.getByLabel("کد تأیید", { exact: true }).fill("2222");
    await page.getByRole("button", { name: "تأیید کد و ادامه", exact: true }).click();
    await page.getByRole("heading", { name: "رمز جدید را انتخاب کن", exact: true }).waitFor();
    check(true, "arbitrary recovery code opens the unchanged new-password form with SMS unavailable");
    await page.locator("input[name=password]").fill("Local.Bypass.Password@2");
    await page.locator("input[name=confirmation]").fill("Local.Bypass.Password@2");
    await page.getByRole("button", { name: "ثبت رمز جدید", exact: true }).click();
    await page.getByRole("heading", { name: "رمز عبور تغییر کرد", exact: true }).waitFor();
    await login(page, adminPhone, "/account", "Local.Bypass.Password@2");
    check(await page.locator("h1").isVisible(), "bypass recovery completes the same password-reset and login flow");
  } else {
  await page.goto(base + "/admin/users");
  await page.waitForURL(url => url.pathname === "/admin/login");
  check(new URL(page.url()).searchParams.get("next") === "/admin/users", "anonymous admin entry preserves its return destination in the dedicated administrator login");
  await page.goto(base + "/customer?change=1&next=/calculator");
  check(await page.locator("input[name=password]").isVisible() && await page.getByRole("button", { name: "کد یک‌بارمصرف", exact: true }).count() === 0, "normal login requires a password and has no passwordless option");
  check(await page.evaluate(() => { const input = document.createElement("input"); input.type = "number"; input.value = "۱۲۰"; return input.value === "" && Number(input.value) === 0; }), "regression reproduction: the former number input discarded Persian digits and converted the empty value to zero");
  await page.locator("input[name=phone]").fill(adminPhone);
  await page.locator("input[name=password]").fill("Wrong.Password@1");
  await page.locator("form").getByRole("button", { name: "ورود", exact: true }).click();
  await page.locator("form [role=alert]").waitFor();
  check(await page.locator("input[name=password]").isVisible(), "wrong-password errors preserve the login form");

  await page.goto(base + "/customer?change=1&next=" + encodeURIComponent("/calculator"));
  await page.getByRole("button", { name: "ثبت‌نام", exact: true }).click(); await fillRegistration(page);
  await control({ smsAvailable: false });
  await page.getByRole("button", { name: "ادامه ثبت‌نام", exact: true }).click();
  await page.locator("form [role=alert]").waitFor();
  check(await page.locator("input[name=phone]").inputValue() === signupPhone && await page.locator("input[name=firstName]").count() === 0, "SMS outage retains the mobile without collecting an unverified profile");
  await control({ smsAvailable: true });
  await page.getByRole("button", { name: "ادامه ثبت‌نام", exact: true }).click();
  const otp = page.getByLabel("کد تأیید", { exact: true }); await otp.waitFor();
  check(await otp.getAttribute("maxlength") === "4" && await page.getByRole("button", { name: "ارسال مجدد کد", exact: true }).isDisabled(), "four-digit OTP and resend cooldown use the server contract");
  check((await page.locator("#otp-validity").innerText()).includes("۱۲۰") || (await page.locator("#otp-validity").innerText()).includes("۱۱۹"), "OTP UI starts with two minutes of validity");
  await otp.fill("9999"); await page.getByRole("button", { name: "تأیید کد و ادامه", exact: true }).click();
  await page.locator("form [role=alert]").waitFor();
  await otp.fill("۰۱۲۳"); await page.getByRole("button", { name: "تأیید کد و ادامه", exact: true }).click();
  await completeRegistrationProfile(page);
  check((await context.cookies()).every(cookie => !cookie.name.startsWith("giti_")), "registration confirmation creates an account without passwordless authentication");
  await completeRegistrationLogin(page, "/calculator");
  check(new URL(page.url()).pathname === "/calculator", "new verified customer signs in with password and returns to the requested feature");
  await page.getByLabel("تعداد صفحات سیاه و سفید", { exact: true }).fill("۳");
  await page.getByRole("button", { name: "محاسبه هزینه", exact: true }).click();
  await page.getByRole("alert").filter({ hasText: "مجموع صفحات" }).waitFor();
  await page.getByLabel("تعداد صفحات سیاه و سفید", { exact: true }).fill("۱۲۰");
  await page.getByLabel("تعداد جلد (تیراژ)", { exact: true }).fill("١٠٠");
  await page.getByRole("button", { name: "محاسبه هزینه", exact: true }).click();
  await page.getByRole("link", { name: "ارسال درخواست چاپ" }).waitFor();
  check(!(await page.locator(".error-message").count()), "Persian/Arabic calculator counts produce a valid backend quote with the original page boundaries");
  await page.goto(base + "/customer/profile?next=/account");
  await nameLayoutChecks(page, check, "customer profile");
  check(await page.locator("form input").count() === 2 && await page.locator("input[name=phone]").count() === 0, "the profile edit form only contains first and last name, without a mobile input");
  await page.locator("input[name=firstName]").fill("نام تازه");
  await page.locator("input[name=lastName]").fill("آزمایشی نیک‌اندیش گیلانی");
  const profileSave = page.waitForResponse(response => response.url() === base + "/api/customer/profile" && response.request().method() === "PUT");
  await page.getByRole("button", { name: "ذخیره و ادامه", exact: true }).click();
  const savedProfile = await profileSave;
  check(savedProfile.ok() && JSON.stringify(Object.keys(savedProfile.request().postDataJSON()).sort()) === JSON.stringify(["firstName", "lastName"]), "profile updates submit only first and last name without a mobile number");
  await page.waitForURL(url => url.pathname === "/account");
  check((await page.locator("h1").innerText()).includes("نام تازه") && await page.evaluate(async () => (await (await fetch("/api/customer/me")).json()).phone) === signupPhone, "customer name updates succeed while preserving the authenticated mobile number");
  await checkSignedInName(page, base, check);
  await page.goto(base + "/admin");
  await page.getByRole("heading", { name: "ورود مدیریت چاپ گیتی" }).waitFor();
  check(await page.evaluate(async () => (await fetch("/api/admin/users")).status) === 401, "customer authentication cannot authorize admin UI or backend");
  await logout(page);

  await login(page, adminPhone, "/account");
  check(await page.evaluate(async () => (await fetch("/api/admin/users")).status) === 401, "even a historical customer-admin role requires a separate administrator login");
  await adminLogin(page);
  await page.getByRole("heading", { name: "مشتریان", exact: true, level: 2 }).waitFor();
  check(new URL(page.url()).pathname === "/admin/customers", "administrator accesses the panel through independent fixed-username/password login");
  check(await page.getByRole("button", { name: "صفحه بعد", exact: true }).isEnabled(), "user list is paginated on the server");
  await page.getByRole("button", { name: "صفحه بعد", exact: true }).click();
  await page.getByText(/صفحه ۲ از/).waitFor();
  check(true, "user pagination changes the visible records");
  await page.getByRole("button", { name: "ایجاد کاربر", exact: true }).click();
  const dialog = page.getByRole("dialog"); await dialog.waitFor();
  for (const [name, value] of Object.entries({ firstName: "Created", lastName: "Customer", phone: createdPhone, password, confirmation: password })) await dialog.locator("input[name=" + name + "]").fill(value);
  await dialog.getByRole("button", { name: "ایجاد کاربر", exact: true }).click();
  await dialog.waitFor({ state: "hidden" });
  await page.getByLabel("جست‌وجوی نام یا موبایل").fill(createdPhone);
  await page.getByRole("button", { name: "جست‌وجو", exact: true }).click();
  let row = page.getByRole("row").filter({ hasText: createdPhone }); await row.waitFor();
  await row.getByRole("button", { name: "مشاهده جزئیات" }).click();
  check((await page.getByRole("dialog").innerText()).includes("تأییدنشده") && await page.getByRole("dialog").locator("input").count() === 0, "administrator-created users remain unverified and have no profile-edit action");
  await page.keyboard.press("Escape");

  const other = await browser.newContext({ viewport: { width: 1200, height: 900 } });
  const otherPage = await other.newPage(); otherPage.on("pageerror", error => browserErrors.push(error.message));
  await login(otherPage, createdPhone, "/customer/profile");
  await otherPage.getByRole("button", { name: "دریافت کد تأیید شماره", exact: true }).click();
  await otherPage.getByLabel("کد تأیید شماره", { exact: true }).fill("0123");
  await otherPage.getByRole("button", { name: "تأیید شماره", exact: true }).click();
  await otherPage.waitForFunction(async () => (await (await fetch("/api/customer/me")).json()).phoneVerified);
  check(await otherPage.locator("input[name=phone]").count() === 0, "legacy customers can verify their existing phone without a profile mobile input");
  await page.reload();
  await page.getByLabel("جست‌وجوی نام یا موبایل").fill(createdPhone);
  await page.getByRole("button", { name: "جست‌وجو", exact: true }).click();
  row = page.getByRole("row").filter({ hasText: createdPhone }); await row.waitFor();
  check(await otherPage.evaluate(async () => (await fetch("/api/admin/users")).status) === 401, "verified customer accounts still require independent administrator credentials");
  check(await row.getByRole("button", { name: "ویرایش", exact: true }).count() === 0 && await row.getByRole("button", { name: "مشاهده جزئیات", exact: true }).count() === 1, "the user list cannot edit personal data or grant customer-session administrator access");
  await other.close();

  for (const mode of ["banners"]) {
    const item = mode === "banners" ? "بنر" : "نمونه‌کار", title = mode === "banners" ? "بنر آزمون محلی" : "نمونه‌کار آزمون محلی";
    await page.goto(base + "/admin/" + mode);
    const add = page.getByRole("button", { name: "افزودن " + item, exact: true });
    await add.click(); await dialog.waitFor();
    check(await dialog.evaluate(element => getComputedStyle(element).position === "fixed" && getComputedStyle(document.querySelector('[data-slot="dialog-overlay"]')).position === "fixed"), mode + " modal and backdrop cover the viewport");
    check(await dialog.locator("textarea[name=title]").inputValue() === "", mode + " add opens an empty modal");
    await page.keyboard.press("Escape"); await dialog.waitFor({ state: "hidden" });
    await page.waitForFunction(element => element === document.activeElement, await add.elementHandle(), { timeout: 5000 });
    check(await add.evaluate(element => element === document.activeElement), mode + " modal cancellation restores focus");
    await add.click(); await dialog.locator("textarea[name=title]").fill(title);
    for (let i = 0; i < 18; i++) await page.keyboard.press("Tab");
    check(await page.evaluate(() => !!document.activeElement?.closest('[role="dialog"]')), mode + " modal traps keyboard focus");
    page.once("dialog", d => d.dismiss()); await page.keyboard.press("Escape");
    check(await dialog.isVisible() && await dialog.locator("textarea[name=title]").inputValue() === title, mode + " modal preserves unsaved changes when cancellation is declined");
    if (mode === "banners") { await dialog.getByLabel("نمایش دکمه در بنر", { exact: true }).check(); await dialog.getByLabel("متن دکمه", { exact: true }).fill("مشاهده"); }
    else check(await dialog.getByLabel("لینک دکمه", { exact: true }).count() === 0, "portfolio editing uses automatic detail links");
    const image = Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jfZkAAAAASUVORK5CYII=", "base64");
    await dialog.locator("input[type=file]").setInputFiles({ name: "preview.png", mimeType: "image/png", buffer: image });
    await control({ failNextSave: true });
    await dialog.getByRole("button", { name: "ذخیره " + item, exact: true }).click();
    await dialog.getByRole("alert").waitFor();
    check(await dialog.isVisible(), mode + " save failure keeps the modal and draft open");
    await dialog.getByRole("button", { name: "ذخیره " + item, exact: true }).click();
    await dialog.waitFor({ state: "hidden" });
    await page.getByRole("heading", { name: title, exact: true }).waitFor();
    await page.getByRole("button", { name: "ویرایش " + title, exact: true }).click();
    check(await dialog.locator("textarea[name=title]").inputValue() === title, mode + " edit reuses the form with saved values");
    await dialog.locator("textarea[name=title]").fill(title + " ویرایش");
    await page.setViewportSize({ width: 390, height: 844 });
    check(await dialog.evaluate(element => { const bounds = element.getBoundingClientRect(); return element.scrollWidth <= element.clientWidth + 1 && bounds.left >= 0 && bounds.right <= innerWidth && bounds.top >= 0 && bounds.bottom <= innerHeight + 1; }), mode + " modal fits inside a mobile viewport");
    await page.screenshot({ path: ".qa/modernization-" + mode + "-mobile.png", fullPage: true });
    await dialog.getByRole("button", { name: "ذخیره " + item, exact: true }).click();
    await dialog.waitFor({ state: "hidden" });
    await page.setViewportSize({ width: 1440, height: 1000 });
    check(await page.getByRole("heading", { name: title + " ویرایش", exact: true }).isVisible(), mode + " save updates the list and closes the modal");
  }

  await logout(page);
  await page.getByRole("link", { name: "رمز عبور را فراموش کرده‌ام", exact: true }).click();
  await page.getByRole("heading", { name: "بازیابی رمز عبور", exact: true }).waitFor();
  await page.locator("input[name=phone]").fill(adminPhone);
  await page.getByRole("button", { name: "دریافت کد تأیید", exact: true }).click();
  await page.getByLabel("کد تأیید", { exact: true }).fill("0123");
  await page.getByRole("button", { name: "تأیید کد و ادامه", exact: true }).click();
  await page.getByRole("heading", { name: "رمز جدید را انتخاب کن", exact: true }).waitFor();
  check(await page.evaluate(() => localStorage.length === 0 && sessionStorage.length === 0 && !location.href.includes("resetToken")), "recovery grants and authentication tokens stay out of browser storage and URLs");
  await page.locator("input[name=password]").fill("New.Browser.Password@2");
  await page.locator("input[name=confirmation]").fill("Wrong.Browser.Password@2");
  await page.getByRole("button", { name: "ثبت رمز جدید", exact: true }).click();
  await page.locator("form [role=alert]").waitFor();
  await page.locator("input[name=confirmation]").fill("New.Browser.Password@2");
  await page.getByRole("button", { name: "ثبت رمز جدید", exact: true }).click();
  await page.getByRole("heading", { name: "رمز عبور تغییر کرد", exact: true }).waitFor();
  await login(page, adminPhone, "/account", "New.Browser.Password@2");
  check(await page.evaluate(async () => (await fetch("/api/auth/me")).status) === 200, "customer password recovery preserves independent administrator authentication");
  await logout(page);
  await page.evaluate(async () => fetch("/api/auth/logout", { method: "POST" }));
  await adminLogin(page);
  check(await page.getByRole("heading", { name: "مشتریان", exact: true, level: 2 }).isVisible(), "the existing fixed admin identity retains independent panel access");
  await page.goto(base + "/");
  check((await page.locator(".customer-header-link").getAttribute("href")).startsWith("/customer"), "an administrator-only session does not appear as an authenticated customer in public navigation");
  await logout(page);
  await login(page, adminPhone, "/account", "New.Browser.Password@2");
  // Finish the initial client orders request before jumping the backend clock.
  // Advancing during hydration can expire the page being torn down while it renews.
  await page.locator(".customer-orders[aria-busy=false]").waitFor();
  const before = (await context.cookies()).find(c => c.name === "giti_customer_refresh")?.value;
  await control({ advanceSeconds: 901 });
  await page.goto(base + "/account");
  await page.waitForURL(url => url.pathname === "/account");
  const after = (await context.cookies()).find(c => c.name === "giti_customer_refresh")?.value;
  check(before !== after && !!after, "expired access renews through the same-origin route and rotates the HttpOnly refresh cookie");
  await customerExperienceChecks({ browser, base, control, check, browserErrors, adminPage: page });
  await businessChecks({ page, base, control, check });
  await workflowSupportChecks({ page, base, check,control });
  await accessibilityResponsiveChecks({ browser, page, base, check });
  await independentAuthenticationChecks({ browser, base, control, check, browserErrors });
  await passwordRecoveryChecks({ browser, base, control, check, browserErrors });
  }
  check(browserErrors.length === 0, "critical browser journeys have no runtime JavaScript errors");
  writeFileSync(process.argv.includes("--workflow-modernization") ? (process.env.CHAP_BROWSER_EVIDENCE || ".qa")+"/workflow-modernization-result.json" : process.argv.includes("--final-uat") ? ".qa/final-uat-browser.json" : catalogMagazine ? ".qa/onion-ef-2026-11-17/browser-result.json" : orderRevisions ? ".qa/order-workflow-2026-11-16/browser-result.json" : realtime ? ".qa/signalr/browser-result.json" : notifications ? ".qa/notifications/browser-result.json" : requestWorkflow ? ".qa/request-to-print/browser-result.json" : cmsPhase ? ".qa/cms-v2/phase"+cmsPhase+"-ui-result.json" : otpBypass ? ".qa/otp-bypass-ui-result.json" : ".qa/modernization-ui-result.json", JSON.stringify({ checks, browserErrors, database: "memory only", realSms: false, published: false, otpVerificationEnabled: !otpBypass }, null, 2));
  console.log(checks.length + " browser checks passed; SQL Server and real SMS were never used.");
} catch (error) {
  if (page) await page.screenshot({ path: ".qa/modernization-ui-failure.png", fullPage: true }).catch(() => {});
  throw error;
} finally {
  await browser?.close();
  for (const child of children.reverse()) await stop(child);
  for (const output of logs) output.end();
}

