import assert from "node:assert/strict";
import { formatToman } from "../frontend/lib/money.ts";

export async function checkQuoteSummary(page, quote, check) {
  const summary = page.locator(".price-summary");
  await summary.locator("strong").filter({ hasText: formatToman(quote.total) }).waitFor();
  const expected = [
    ["قیمت هر جلد قبل از تخفیف", formatToman(quote.unitBeforeDiscount)],
    ["جمع مبلغ مشمول تخفیف", formatToman(quote.subtotal)],
    ["تخفیف تیراژ", quote.discountPercent ? quote.discountPercent.toLocaleString("fa-IR") + "٪" : "بدون تخفیف"],
    ["قیمت هر جلد بعد از تخفیف", formatToman(quote.unit)],
    ["مبلغ نهایی", formatToman(quote.total)],
  ];
  const rows = await summary.locator(":scope > div").evaluateAll(items =>
    items.map(item => [item.querySelector("dt").textContent, item.querySelector("dd").textContent]));
  assert.deepEqual(rows, expected);
  check(await page.locator(".spine-estimate").isVisible(), "five ordered price rows match the quote API and spine estimate remains visible");
}

export async function checkSignedInName(page, base, check) {
  const response = await page.request.get(base + "/api/customer/me");
  assert.ok(response.ok());
  const { firstName, lastName } = await response.json();
  assert.ok(firstName.trim() && lastName.trim());
  const fullName = firstName.trim() + " " + lastName.trim();
  const link = page.getByRole("link", { name: "حساب کاربری " + fullName, exact: true });
  const name = link.locator("span");
  const viewport = page.viewportSize();
  await page.evaluate(() => document.fonts.ready);
  for (const width of [320, 390, 768, 1024, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    assert.equal(await name.innerText(), fullName);
    const visible = await name.evaluate(el => {
      const rect = el.getBoundingClientRect();
      const header = el.closest("header").getBoundingClientRect();
      return rect.width > 0 && rect.height > 0 && rect.left >= 0 && rect.right <= innerWidth + 1 &&
        rect.top >= header.top - 1 && rect.bottom <= header.bottom + 1 &&
        el.scrollWidth <= el.clientWidth + 1 && el.scrollHeight <= el.clientHeight + 1;
    });
    check(visible, "full signed-in name fits the public header at " + width + "px");
  }
  await page.setViewportSize(viewport);
  const returnUrl = page.url();
  await link.click();
  await page.getByRole("heading", { level: 1, name: fullName + "، خوش آمدی.", exact: true }).waitFor();
  check(true, "account greeting includes both customer names after password login");
  await page.goto(returnUrl);
}
