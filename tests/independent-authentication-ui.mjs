import assert from "node:assert/strict";
import { passwordLabelChecks } from "./registration-form-ui.mjs";
import { PASSWORD_GUIDANCE } from "../frontend/lib/password-policy.ts";

export async function independentAuthenticationChecks({ browser, base, control, check, browserErrors }) {
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
  await context.route("**/*", route => route.request().url().startsWith(base) ? route.continue() : route.abort());
  const page = await context.newPage(); page.on("pageerror", error => browserErrors.push(error.message));
  const status = path => page.evaluate(async path => (await fetch(path)).status, path);
  async function customerLogin() {
    await page.goto(base + "/customer?change=1&next=/account");
    await page.locator("input[name=phone]").fill("09000000826");
    await page.locator("input[name=password]").fill("New.Browser.Password@2");
    await page.locator("form").getByRole("button", { name: "ورود", exact: true }).click();
    await page.waitForURL(url => url.pathname === "/account");
    await page.locator(".customer-orders[aria-busy=false]").waitFor();
  }
  async function adminLogin(password = "Old.Test.Password@1") {
    await page.goto(base + "/admin/login?next=/admin/profile");
    await page.locator("input[name=username]").waitFor();
    await page.locator("input[name=password]").fill(password);
    await page.getByRole("button", { name: "ورود به مدیریت", exact: true }).click();
    await page.waitForURL(url => url.pathname === "/admin/profile");
  }
  try {
    await page.goto(base + "/admin/login?next=/admin/profile");
    const username = page.locator("input[name=username]");
    check(await username.inputValue() === "admin" && await username.getAttribute("readonly") !== null && await page.locator("input[name=email]").count() === 0, "administrator login has an immutable admin username and no email input");
    await adminLogin();
    check(await status("/api/customer/me") === 401, "administrator login does not silently create a customer session");
    check(await page.getByRole("link", { name: "پروفایل مدیر", exact: true }).isVisible(), "administrator profile is reachable through panel navigation");
    await customerLogin();
    check(await status("/api/auth/me") === 200, "customer login preserves the independent administrator session");
    await page.goto(base + "/admin/administrators");
    check(await page.getByRole("form", { name: "ایجاد مدیر", exact: true }).count() === 0 && await page.locator("table button").count() === 0,
      "the fixed-admin directory preserves historical accounts without creating new login identities or editing others");
    await page.goto(base + "/admin/profile");
    check(await page.locator("input[name=username]").inputValue() === "admin" && await page.locator("input[name=username]").getAttribute("readonly") !== null, "administrator profile displays the unchanged read-only login username");
    const emailForm = page.getByRole("form", { name: "تغییر ایمیل مدیر", exact: true });
    await emailForm.locator("input[name=email]").fill("second.browser@example.test");
    await emailForm.getByRole("button", { name: "تغییر ایمیل", exact: true }).click();
    await page.getByRole("alert").filter({ hasText: "این ایمیل قبلاً استفاده شده است." }).waitFor();
    check(true, "duplicate administrator email shows an actionable Persian error");
    await emailForm.locator("input[name=email]").fill("owner.browser@example.test");
    await emailForm.getByRole("button", { name: "تغییر ایمیل", exact: true }).click();
    await emailForm.getByText("owner.browser@example.test", { exact: true }).waitFor();
    const identity = await page.evaluate(async () => (await fetch("/api/auth/me")).json());
    check(identity.username === "admin" && identity.email === "owner.browser@example.test", "changing the profile email preserves the admin login username");
    const passwordForm = page.getByRole("form", { name: "تغییر رمز مدیر", exact: true });
    await passwordLabelChecks(page, check, "change", passwordForm);
    check(await passwordForm.getByLabel("رمز عبور فعلی", { exact: true }).count() === 1, "administrator password change retains its current-password label");
    check(await passwordForm.getByText(PASSWORD_GUIDANCE, { exact: true }).isVisible(), "administrator password change explains the policy before submission");
    await passwordForm.locator("input[name=currentPassword]").fill("wrong-current");
    await passwordForm.locator("input[name=password]").fill("Giti!6");
    await passwordForm.locator("input[name=confirmation]").fill("Giti!6");
    await passwordForm.getByRole("button", { name: "تغییر رمز عبور", exact: true }).click();
    await page.getByRole("alert").filter({ hasText: "رمز عبور فعلی صحیح نیست." }).waitFor();
    check(true, "administrator password change verifies the current password");
    await passwordForm.locator("input[name=currentPassword]").fill("Old.Test.Password@1");
    await passwordForm.locator("input[name=confirmation]").fill("Other!6");
    await passwordForm.getByRole("button", { name: "تغییر رمز عبور", exact: true }).click();
    await page.getByRole("alert").filter({ hasText: "تکرار رمز عبور یکسان نیست." }).waitFor();
    check(await passwordForm.locator("input[name=confirmation]").getAttribute("aria-invalid") === "true", "administrator password confirmation is validated inline");
    await passwordForm.locator("input[name=confirmation]").fill("Giti!6");
    await passwordForm.getByRole("button", { name: "تغییر رمز عبور", exact: true }).click();
    await page.getByRole("link", { name: "ورود دوباره به مدیریت", exact: true }).waitFor();
    check(await status("/api/auth/me") === 401 && await status("/api/customer/me") === 200, "administrator password change invalidates admin sessions and preserves the customer's login");
    await adminLogin("Giti!6");
    check(await status("/api/auth/me") === 200, "the fixed admin username works with the newly changed password after an email change");
    await page.setViewportSize({ width: 390, height: 844 });
    await page.waitForFunction(() => document.querySelector(".admin-sidebar").getBoundingClientRect().left >= innerWidth - 1);
    check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), "administrator profile forms fit an RTL mobile viewport");
    await page.screenshot({ path: ".qa/administrator-profile-mobile.png", fullPage: true });
    const before = await context.cookies();
    await control({ advanceSeconds: 901 });
    await page.goto(base + "/admin/profile");
    await page.waitForURL(url => url.pathname === "/admin/profile");
    const adminRenewed = await context.cookies();
    check(before.find(c => c.name === "giti_admin_refresh")?.value !== adminRenewed.find(c => c.name === "giti_admin_refresh")?.value &&
      before.find(c => c.name === "giti_customer_refresh")?.value === adminRenewed.find(c => c.name === "giti_customer_refresh")?.value,
    "administrator expiry renews only the administrator cookie family through its own route");
    await page.goto(base + "/account");
    await page.waitForURL(url => url.pathname === "/account");
    await page.locator(".customer-orders[aria-busy=false]").waitFor();
    const customerRenewed = await context.cookies();
    check(adminRenewed.find(c => c.name === "giti_customer_refresh")?.value !== customerRenewed.find(c => c.name === "giti_customer_refresh")?.value &&
      adminRenewed.find(c => c.name === "giti_admin_refresh")?.value === customerRenewed.find(c => c.name === "giti_admin_refresh")?.value,
    "customer expiry renews independently without rotating administrator credentials");
    await page.evaluate(async () => fetch("/api/customer/logout", { method: "POST" }));
    check(await status("/api/auth/me") === 200 && await status("/api/customer/me") === 401, "customer logout preserves backend administrator authorization");
    await customerLogin();
    await page.goto(base + "/admin/profile");
    await page.getByRole("button", { name: "منوی مدیریت", exact: true }).click();
    await page.getByRole("button", { name: "خروج از حساب", exact: true }).click();
    await page.waitForURL(url => url.pathname === "/admin/login");
    check(await status("/api/customer/me") === 200 && await status("/api/auth/me") === 401, "the administrator logout button preserves the customer session");
    assert.ok((await context.cookies()).every(c => !["giti_access", "giti_refresh"].includes(c.name)));
  } catch (error) {
    await page.screenshot({ path: ".qa/independent-authentication-failure.png", fullPage: true }).catch(() => {});
    throw error;
  } finally { await context.close(); }
}
