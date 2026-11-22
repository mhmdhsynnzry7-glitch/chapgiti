import assert from "node:assert/strict";
import path from "node:path";

// Called only by the network-restricted, memory-only browser suite.
export async function accessibilityResponsiveChecks({ browser, page, base, check }) {
  const axePath = path.resolve("frontend/node_modules/axe-core/axe.min.js");
  async function authenticatedView() {
    // Long-lived SignalR/SSE requests intentionally never reach networkidle.
    await page.locator('.notification-bell:visible .sr-only').filter({hasText:'اعلان خوانده‌نشده'}).waitFor({state:'attached'});
  }
  async function accessible(target, label) {
    await target.evaluate(() => document.fonts.ready);
    await target.addScriptTag({ path: axePath });
    const violations = await target.evaluate(async () => {
      const result = await window.axe.run(document, {
        runOnly: { type: "tag", values: ["wcag2a", "wcag2aa", "wcag21aa", "wcag22aa"] },
      });
      return result.violations.map(v => ({ id: v.id, nodes: v.nodes.map(n => ({ target: n.target, summary: n.failureSummary })) }));
    });
    assert.deepEqual(violations, [], label + ": " + JSON.stringify(violations));
    check(true, label + " passes automated WCAG 2.2 AA rules");
  }
  const guest = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
  await guest.route("**/*", route => route.request().url().startsWith(base) ? route.continue() : route.abort());
  const publicPage = await guest.newPage();
  try {
    for (const route of ["/", "/customer?change=1", "/customer/forgot-password"]) {
      await publicPage.goto(base + route, { waitUntil: "networkidle" });
      await accessible(publicPage, route);
    }
    await publicPage.goto(base + "/");
    const dots = publicPage.locator(".banner-dots button");
    check(await dots.evaluateAll(nodes => nodes.every(node => {
      const rect = node.getBoundingClientRect(); return rect.width >= 24 && rect.height >= 24;
    })), "banner pagination retains visible dots with at least 24px touch targets");
    // Exercise the loading fallback itself without altering authentication or API data.
    await publicPage.setContent('<html lang="fa" dir="rtl"><head></head><body><main><div class="container page-body customer-order-loading" aria-busy="true"><p role="status">در حال دریافت اطلاعات سفارش…</p></div></main></body></html>');
    const stylesheet = await page.locator('link[rel="stylesheet"]').first().getAttribute('href');
    assert.ok(stylesheet); await publicPage.addStyleTag({ url: base + stylesheet });
    check(await publicPage.locator('.customer-order-loading').evaluate(node => node.getBoundingClientRect().height >= innerHeight), "streamed order fallback reserves a viewport and prevents the footer shifting into view");
    await page.goto(base + "/order", { waitUntil: "domcontentloaded" });
    await page.locator('main form').waitFor(); await authenticatedView();
    await page.setViewportSize({ width: 320, height: 844 });
    check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), "order submission actions reflow without horizontal scrolling at 320px");
    await page.setViewportSize({ width: 1440, height: 1000 });
    const html = await (await page.request.get(base + "/admin")).text();
    check(html.includes('<div class="metrics-grid">'), "authenticated dashboard metrics are present in server-rendered HTML");
    let overviewRequests = 0;
    const countOverview = request => { if (request.url().includes('/api/admin/overview')) overviewRequests++; };
    page.on('request', countOverview);
    try {
      await page.goto(base + "/admin", { waitUntil: "domcontentloaded" });
      await page.locator('.metrics-grid').waitFor(); await authenticatedView();
      check(overviewRequests === 0, "server-rendered dashboard does not duplicate its initial overview request");
      const refresh = page.waitForResponse(response => response.url().includes('/api/admin/overview'));
      await page.evaluate(() => window.dispatchEvent(new Event('focus')));
      check((await refresh).status() === 200, "server-rendered dashboard still refreshes on focus");
    } finally { page.off('request', countOverview); }
    await page.goto(base + "/admin/orders", { waitUntil: "domcontentloaded" });
    await authenticatedView();
    for (const width of [360, 390, 600, 601, 768, 1024, 1440, 1920]) {
      await page.setViewportSize({ width, height: 1000 });
      check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), "admin order filters/table do not overflow at " + width + "px");
    }
    await accessible(page, "admin order list");
    const first = await page.evaluate(async () => (await (await fetch("/api/admin/orders?page=1")).json()).orders[0]?.id);
    assert.ok(first);
    await page.goto(base + "/admin/orders/" + first, { waitUntil: "domcontentloaded" });
    await page.locator('.order-editor').waitFor(); await authenticatedView();
    await accessible(page, "admin order breakdown on a light panel");
    for (const route of ["banners"]) {
      await page.goto(base + "/admin/" + route, { waitUntil: "domcontentloaded" });
      await authenticatedView();
      const opener = page.getByRole("button", { name: route === "banners" ? "افزودن بنر" : "افزودن نمونه‌کار", exact: true });
      await opener.click();
      const dialog = page.getByRole("dialog"); await dialog.waitFor();
      check(await dialog.evaluate(node => node.contains(document.activeElement)), route + " dialog receives focus");
      await accessible(page, route + " editor dialog");
      await page.keyboard.press("Shift+Tab");
      check(await dialog.evaluate(node => node.contains(document.activeElement)), route + " dialog traps keyboard focus");
      await page.keyboard.press("Escape");
      await dialog.waitFor({ state: "hidden" });
      await page.waitForFunction(() => document.activeElement?.textContent?.trim() === "افزودن بنر");
      check(await opener.evaluate(node => node === document.activeElement), route + " dialog restores opener focus");
    }
  } finally {
    await page.setViewportSize({ width: 1440, height: 1000 });
    await guest.close();
  }
}
