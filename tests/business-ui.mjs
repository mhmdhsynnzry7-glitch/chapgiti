import { formatToman } from "../frontend/lib/money.ts";

// This module runs only inside modernization-ui's isolated in-memory API host.
export async function businessChecks({ page, base, check, control }) {
  async function catalogSelect(label,text){await page.getByLabel(label,{exact:true}).click();await page.getByRole("option",{name:text,exact:true}).click();}
  await page.goto(base + "/admin/orders");
  await page.locator(".admin-content table tbody tr").first().waitFor();
  const initial = await page.locator(".admin-content table tbody tr").count();
  check(initial > 0, "administrator list renders committed customer orders");
  await page.getByRole("combobox", { name: "فیلتر وضعیت", exact: true }).selectOption("new");
  await page.waitForFunction(() => document.querySelector('select[aria-label="فیلتر وضعیت"]')?.value === "new" && !document.querySelector(".admin-content .loading"));
  check(await page.locator('select[aria-label="فیلتر وضعیت"]').inputValue() === "new", "new-order shortcut applies the authoritative new status filter");
  const before = await page.evaluate(async () => (await (await fetch("/api/admin/orders?status=new")).json()).total);
  await control({ seedOrdersFor: "09000000826", orderCount: 1 });
  const refreshed = page.waitForResponse(r => r.url().includes("/api/admin/orders?") && r.status() === 200);
  await page.evaluate(() => window.dispatchEvent(new Event("focus")));
  check((await (await refreshed).json()).total === before + 1, "regression: an open administrator list refetches new committed orders on focus without changing filters");
  const manual = page.waitForResponse(r => r.url().includes("/api/admin/orders?") && r.status() === 200);
  await page.getByRole("button", { name: "تازه‌سازی سفارش‌ها", exact: true }).click();
  check((await (await manual).json()).total === before + 1, "explicit order refresh retrieves the current list");
  await page.getByRole("combobox", { name: "فیلتر وضعیت", exact: true }).selectOption("");
  await page.locator(".admin-content table tbody tr").first().waitFor();
  const firstCode = await page.locator(".admin-content table tbody tr").first().locator("bdi").innerText();
  await page.getByRole("textbox", { name: "جست‌وجوی سفارش", exact: true }).fill(firstCode.trim());
  await page.getByRole("button", { name: "جست‌وجو", exact: true }).click();
  await page.waitForFunction(() => document.querySelectorAll(".admin-content table tbody tr").length === 1 && !document.querySelector(".admin-content .loading"));
  check(await page.locator(".admin-content table tbody tr").count() === 1, "administrator tracking-code search returns the matching committed order");

  await page.goto(base + "/admin/settings");
  await page.getByRole("button", { name: "تعرفه‌های چاپ", exact: true }).click();
  check(await page.getByRole("button", { name: "اتصال ملی‌پیامک", exact: true }).count() === 0 && await page.getByRole("link", { name: "گزارش پیامک", exact: true }).count() === 0 && await page.getByRole("link", { name: "تاریخچه فعالیت کاربران", exact: true }).isVisible(), "SMS navigation is replaced with distinct customer activity while provider settings stay private");
  await page.getByRole("heading", { name: "تعرفه خدمات تکمیلی", exact: true }).waitFor();
  check(await page.locator('input[aria-label^="تعرفه "]').count() === 15, "all fifteen authoritative finishing tariffs have numeric administrator inputs");
  const gold = page.getByRole("spinbutton", { name: "تعرفه طلاکوب", exact: true });
  const prior = Number(await gold.inputValue());
  await gold.fill(String(prior + 123));
  check((await gold.locator("..").innerText()).includes(formatToman(prior + 123)), "tariff editing retains a numeric input and displays grouped toman with its per-book unit");
  const saved = page.waitForResponse(r => r.url().endsWith("/api/admin/settings") && r.request().method() === "PUT");
  await page.getByRole("button", { name: "ذخیره خدمات تکمیلی", exact: true }).click();
  check((await saved).status() === 200, "administrator can explicitly save a finishing tariff in isolated data");
  await page.reload(); await page.getByRole("button", { name: "تعرفه‌های چاپ", exact: true }).click();
  check(Number(await gold.inputValue()) === prior + 123, "saved finishing tariff survives a fresh settings request");
  // Restore only the isolated test fixture; never contacts operational settings.
  await gold.fill(String(prior));
  const restored = page.waitForResponse(r => r.url().endsWith("/api/admin/settings") && r.request().method() === "PUT");
  await page.getByRole("button", { name: "ذخیره خدمات تکمیلی", exact: true }).click(); await restored;

  await page.goto(base + "/calculator");
  await page.locator("#spec-extras-title").click();
  const foil = page.getByRole("checkbox", { name: "طلاکوب", exact: true });
  check(await foil.isDisabled() && (await foil.locator("..").innerText()).includes("سلفون مات"), "unavailable foil explains its coating prerequisite next to the option");
  await catalogSelect("پوشش جلد","سلفون مات");
  check(await foil.isEnabled(), "matte cover enables existing foil pricing");
  await foil.check();
  await catalogSelect("نوع صحافی","جلد سخت عطف تخت");
  check(await page.getByRole("checkbox", { name: "روبان جلد سخت", exact: true }).isEnabled(), "hard binding enables hard-cover finishing services");
  await catalogSelect("چاپ جلد","طلق");
  check(await foil.isDisabled() && !await foil.isChecked() && (await page.locator("#finishing-options [role=status]").innerText()).includes("طلاکوب"), "changing to plastic cannot keep a hidden stale matte-foil selection and explains its removal");

  await page.goto(base + "/portfolio");
  await page.waitForURL("**/services");
  check(true, "retired portfolio URL redirects without removing calculator or customer-directory features");
  await page.goto(base + "/admin/customers");
  await page.getByRole("columnheader", { name: "آخرین ورود", exact: true }).waitFor();
  check(await page.getByRole("columnheader", { name: "سفارش‌ها", exact: true }).isVisible() && (await page.locator(".admin-content").innerText()).includes("آنلاین بودن نیست"), "customer directory distinguishes stored login evidence from valid sessions and online presence");
  check(await page.getByRole("button", { name: "ویرایش", exact: true }).count() === 0, "directory has no customer profile edit actions");
}
