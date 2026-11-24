import {formatToman} from "../frontend/lib/money.ts";
import assert from "node:assert/strict";
import { mkdirSync } from "node:fs";
export async function catalogMagazineChecks({ page, base, check }) {
  const root = ".qa/onion-ef-2026-11-17/screenshots";
  mkdirSync(root, { recursive: true });
  async function api(method, path, body, status = 200) {
    const response = await page.request.fetch(base + "/api/" + path, {
      method,
      headers: { Origin: base, "Content-Type": "application/json" },
      ...(body ? { data: body } : {}),
    });
    const json = await response.json();
    assert.equal(response.status(), status, path + " " + JSON.stringify(json));
    return json;
  }
  async function screenshot(name) {
    for (const [width, height, label] of [
      [1440, 1000, "desktop"],
      [768, 1024, "tablet"],
      [390, 844, "mobile"],
    ]) {
      await page.setViewportSize({ width, height });
      await page.evaluate(() => window.scrollTo(0, 0));
      await page.waitForTimeout(180);
      check(
        await page.evaluate(
          () => document.documentElement.scrollWidth <= innerWidth + 1,
        ),
        name + " fits " + width + "px without horizontal overflow",
      );
      await page.screenshot({
        path: root + "/" + name + "-" + label + ".png",
        fullPage: true,
        animations: "disabled",
      });
    }
    await page.setViewportSize({ width: 1440, height: 1000 });
  }
  const trigger = (label) => page.getByLabel(label, { exact: true });
  async function select(label, text) {
    await trigger(label).click();
    const options = page.getByRole("option"),
      match = options.filter({ hasText: text });
    await match.waitFor();
    assert.equal(await match.count(), 1);
    await match.click();
  }
  let releaseCatalog;
  const catalogGate = new Promise((resolve) => (releaseCatalog = resolve));
  const unavailable = async (route) => {
    await catalogGate;
    await route.fulfill({
      status: 503,
      contentType: "application/json",
      body: JSON.stringify({ error: "دریافت تنظیمات چاپ آزمایشی ناموفق بود." }),
    });
  };
  await page.route("**/api/public/print-catalog", unavailable);
  await page.goto(base + "/order");
  await page
    .getByText("در حال دریافت گزینه‌های چاپ…", { exact: true })
    .waitFor();
  check(true, "catalog loading has an accessible explicit state");
  releaseCatalog();
  await page
    .getByRole("alert")
    .filter({ hasText: "دریافت تنظیمات چاپ آزمایشی ناموفق بود." })
    .waitFor();
  check(
    await page
      .getByRole("button", { name: "تلاش دوباره", exact: true })
      .isVisible(),
    "catalog failures expose retry instead of static fallback options",
  );
  await page.unroute("**/api/public/print-catalog", unavailable);
  await page.getByRole("button", { name: "تلاش دوباره", exact: true }).click();
  await trigger("قطع کتاب").waitFor();
  const html = await (await page.request.get(base + "/calculator")).text();
  check(html.includes("قطع کتاب"), "authenticated calculator includes its catalog fields in the first HTML response");
  await page.goto(base + "/calculator");
  await trigger("قطع کتاب").click();
  await page.getByLabel("جست‌وجو در قطع کتاب", { exact: true }).fill("وزیری");
  check(
    (await page.getByRole("option").count()) === 2,
    "Persian catalog search filters actual API labels",
  );
  const popup = page.locator(".catalog-popup");
  check(
    await popup.evaluate(
      (el) => !el.closest("form") && el.getBoundingClientRect().height <= 375,
    ),
    "popup is portaled and height-bounded outside clipping forms",
  );
  await page.keyboard.press("Escape");
  await popup.waitFor({state:"hidden"});
  await trigger("قطع کتاب").and(page.locator(":focus")).waitFor();
  check(
    !(await popup.isVisible()) &&
      (await trigger("قطع کتاب").evaluate(
        (el) => el === document.activeElement,
      )),
    "Escape closes the popup and restores trigger focus",
  );
  await trigger("قطع کتاب").click();
  await page.getByLabel("جست‌وجو در قطع کتاب", { exact: true }).fill("وزیری");
  await page
    .getByRole("option")
    .filter({ hasText: /^وزیری/ })
    .click();
  check(
    (await page
      .getByLabel("عرض کتاب (سانتی‌متر)", { exact: true })
      .inputValue()) === "17" &&
      (await page
        .getByLabel("طول کتاب (سانتی‌متر)", { exact: true })
        .inputValue()) === "24",
    "standard dimensions come from the selected SQL catalog record",
  );
  check(
    (await page
      .getByLabel("عرض کتاب (سانتی‌متر)", { exact: true })
      .getAttribute("readonly")) !== null,
    "standard dimensions are read-only",
  );
  await trigger("چاپ متن").click();
  await page
    .getByLabel("جست‌وجو در چاپ متن", { exact: true })
    .fill("یافت نمی شود");
  check(
    await page.getByText("گزینه‌ای پیدا نشد.", { exact: true }).isVisible(),
    "selector displays a useful empty search state",
  );
  await page.keyboard.press("Escape");
  await trigger("چاپ متن").focus();
  await page.keyboard.press("ArrowDown");
  await page.keyboard.press("ArrowDown");
  await page.keyboard.press("Enter");
  await popup.waitFor({state:"hidden"});
  check(
    !(await popup.isVisible()),
    "selector supports keyboard navigation and selection",
  );
  async function verifyDiscount(quantity, percent) {
    await page.getByLabel("تعداد جلد (تیراژ)",{exact:true}).fill(String(quantity));
    const calculation=page.waitForResponse(r=>r.request().method()==="POST" && r.url().endsWith("/api/quote"));
    await page.getByRole("button",{name:"محاسبه هزینه",exact:true}).click();
    const result=await (await calculation).json();
    assert.equal(result.discountPercent,percent);
    await page.getByRole("definition").getByText(formatToman(result.total),{exact:true}).waitFor();
    check(await page.getByRole("definition",{exact:true}).filter({hasText:percent ? percent.toLocaleString("fa-IR")+"٪" : "بدون تخفیف"}).isVisible(),"catalog discount at "+quantity+" copies equals "+percent+" percent");
  }
  await verifyDiscount(50,0);await verifyDiscount(51,10);
  await screenshot("selectors");
  await select("چاپ متن", "چهار رنگ (تونری)");
  await select("چاپ متن", "ترکیبی (سیاه و سفید + رنگی تونری)");
  check(
    Number(
      await page
        .getByLabel("تعداد صفحات سیاه و سفید", { exact: true })
        .inputValue(),
    ) +
      Number(
        await page
          .getByLabel("تعداد صفحات رنگی / دو رنگ", { exact: true })
          .inputValue(),
      ) ===
      120,
    "switching from color-only to mixed printing preserves the total page count",
  );
  await trigger("قطع کتاب").click();
  await screenshot("selector-popup");
  await page.keyboard.press("Escape");
  await page.goto(base + "/admin/print-settings");
  await page.getByRole("table").getByRole("cell",{name:"وزیری",exact:true}).waitFor();
  check(await page.getByRole("group",{name:"گروه تنظیمات چاپ"}).getByRole("button").count()===8,"all eight editable catalog groups are available");
  await screenshot("admin-catalog");
  await page.getByText("تخفیف تیراژ",{exact:true}).click();
  await page.getByLabel("درصد تخفیف",{exact:true}).fill("12");
  const policySaved=page.waitForResponse(r=>r.request().method()==="PUT" && r.url().endsWith("/quantity-discount"));
  await page.getByRole("button",{name:"ذخیره تخفیف"}).click();
  assert.equal((await policySaved).status(),200);
  const catalog = (await api("GET", "admin/print-catalog")).items,
    format = catalog.find((i) => i.kind === "bookSizes" && i.code === "vaziri");
  const protectedOption=catalog.find(item=>item.isDefault);
  const protectedResult=await api("DELETE","admin/print-catalog/"+protectedOption.id+"?version="+protectedOption.version,undefined,409);
  check(protectedResult.code==="CATALOG_DELETE_PROTECTED","catalog defaults remain protected through the browser proxy");
  const unused=await api("POST","admin/print-catalog",{...format, code:"uat-trim",label:"قطع آزمایشی",isDefault:false},201);
  await api("DELETE","admin/print-catalog/"+unused.id+"?version=1");
  check(!(await api("GET","admin/print-catalog")).items.some(item=>item.id===unused.id),"deleting an unused catalog option reaches the repository");

  await page
    .getByRole("row").filter({has:page.getByRole("cell",{name:format.label,exact:true})}).getByRole("button",{name:"ویرایش",exact:true})
    .click();
  await page
    .getByLabel("نام نمایشی فارسی", { exact: true })
    .fill("وزیری ویرایش مرورگر");
  const saved = page.waitForResponse(
    (r) =>
      r.url().includes("/api/admin/print-catalog/") &&
      r.request().method() === "PUT",
  );
  await page.getByRole("button", { name: "ذخیره گزینه", exact: true }).click();
  assert.equal((await saved).status(), 200);
  await page.goto(base + "/calculator");
  await trigger("قطع کتاب").waitFor();
  await select("قطع کتاب", "وزیری ویرایش مرورگر");
  check(
    await trigger("قطع کتاب")
      .innerText()
      .then((t) => t.includes("وزیری ویرایش مرورگر")),
    "admin edits reach the customer selector after deliberate catalog refetch",
  );
  await verifyDiscount(51,12);
  const hiddenSize=catalog.find(item=>item.kind==="bookSizes" && item.code!==format.code && !item.isDefault && item.active);
  assert.ok(hiddenSize);
  const disabledLabel="اندازه غیرفعال آزمایشی";
  await api("PUT","admin/print-catalog/"+hiddenSize.id,{...hiddenSize,label:disabledLabel,active:false});
  await page.reload();await trigger("قطع کتاب").click();
  await page.getByLabel("جست‌وجو در قطع کتاب",{exact:true}).fill(disabledLabel);
  await page.getByText("گزینه‌ای پیدا نشد.",{exact:true}).waitFor();
  check(await page.getByRole("option").count()===0,"disabled size is unavailable for new selections");
  await page.keyboard.press("Escape");
  await select("قطع کتاب","وزیری ویرایش مرورگر");
  const updated = {
    ...format,
    label: "وزیری ویرایش دوم",
    version: format.version + 1,
  };
  await api("PUT", "admin/print-catalog/" + format.id, updated);
  await page.evaluate(() => window.dispatchEvent(new Event("focus")));
  await page.waitForFunction(() =>
    document
      .querySelector(".catalog-trigger")
      ?.textContent.includes("وزیری ویرایش دوم"),
  );
  check(
    (await page
      .getByLabel("عرض کتاب (سانتی‌متر)", { exact: true })
      .inputValue()) === "17",
    "focus refetch preserves the selected stable code and dimensions",
  );
  await api("PUT", "admin/print-catalog/" + format.id, {
    ...updated,
    version: updated.version + 1,
    metadata: { ...updated.metadata, width: 18 },
  });
  await page.evaluate(() => window.dispatchEvent(new Event("focus")));
  const applyDimensions = page.getByRole("button", {
    name: "اعمال ابعاد جدید",
    exact: true,
  });
  await applyDimensions.waitFor();
  check(
    (await page
      .getByLabel("عرض کتاب (سانتی‌متر)", { exact: true })
      .inputValue()) === "17",
    "changed standard dimensions require explicit confirmation instead of silently changing an edited request",
  );
  await applyDimensions.click();
  check(
    (await page
      .getByLabel("عرض کتاب (سانتی‌متر)", { exact: true })
      .inputValue()) === "18",
    "confirmed standard dimensions use the refreshed catalog metadata",
  );
  const defaultFormat = (await api("GET", "admin/print-catalog")).items.find(
    (i) => i.id === format.id,
  );
  await api("PUT", "admin/print-catalog/" + format.id, {
    ...defaultFormat,
    isDefault: true,
  });
  await page.goto(base + "/order");
  await trigger("قطع کتاب").waitFor();
  check(
    (await trigger("قطع کتاب").innerText()).includes("وزیری ویرایش دوم") &&
      (await page
        .getByLabel("عرض کتاب (سانتی‌متر)", { exact: true })
        .inputValue()) === "18",
    "a fresh order reads the current SQL default and dimensions instead of legacy SSR constants",
  );
  await page.goto(base + "/admin/articles/new");
  await page
    .getByLabel("عنوان مقاله", { exact: true })
    .fill("راهنمای انتخاب قطع کتاب");
  await page
    .getByLabel("نشانی مقاله", { exact: true })
    .fill("browser-print-guide");
  await page.getByLabel("دسته‌بندی", { exact: true }).fill("راهنمای چاپ");
  await page
    .getByLabel("خلاصه مقاله", { exact: true })
    .fill(
      "راهنمایی برای انتخاب قطع، کاغذ و آماده‌سازی کتاب؛ این مقاله فقط یک دادهٔ آزمایشی است.",
    );
  await page
    .getByLabel("متن مقاله", { exact: true })
    .fill(
      "## قطع مناسب برای کتاب شما\n\nانتخاب قطع، اولین قدم برای آماده‌سازی یک کتاب خواندنی است. این نوشته دادهٔ آزمایشی مرورگر است.\n\n- اندازه متن و تصاویر را بررسی کنید.\n- حاشیهٔ برش را حفظ کنید.\n\n### مقایسه ابعاد\n\n| قطع | عرض | طول |\n| --- | --- | --- |\n| رقعی | ۱۴٫۵ | ۲۱٫۵ |\n| وزیری | ۱۷ | ۲۴ |",
    );
  await screenshot("admin-editor");
  page.once("dialog", (dialog) => dialog.dismiss());
  await page.getByRole("link", { name: "بازگشت به مجله", exact: true }).click();
  check(
    new URL(page.url()).pathname === "/admin/articles/new",
    "unsaved article edits survive an accidentally clicked navigation link",
  );
  const created = page.waitForResponse(
    (r) =>
      r.url().endsWith("/api/admin/articles") &&
      r.request().method() === "POST",
  );
  await page
    .getByRole("button", { name: "ذخیره پیش‌نویس", exact: true })
    .click();
  assert.equal((await created).status(), 201);
  await page.waitForURL((url) =>
    /\/admin\/articles\/[a-f0-9-]+$/.test(url.pathname),
  );
  const id = new URL(page.url()).pathname.split("/").at(-1);
  await api("GET", "public/articles/browser-print-guide", undefined, 404);
  check(true, "saved drafts stay private");
  const publish = page.waitForResponse(
    (r) =>
      r.url().endsWith("/api/admin/articles/" + id) &&
      r.request().method() === "PUT",
  );
  await page.getByRole("button", { name: "انتشار مقاله", exact: true }).click();
  assert.equal((await publish).status(), 200);
  await page.getByRole("button", { name: "لغو انتشار", exact: true }).waitFor();
  await page.goto(base + "/admin/articles");
  await page
    .getByRole("heading", { name: "مجله چاپ گیتی", exact: true })
    .waitFor();
  await page
    .getByRole("row").filter({hasText:"راهنمای انتخاب قطع کتاب"}).getByRole("link", { name: "ویرایش راهنمای انتخاب قطع کتاب", exact: true })
    .waitFor();
  await screenshot("admin-magazine-list");
  await page.goto(base + "/blog");
  await page
    .getByRole("heading", { name: "مجله چاپ گیتی", exact: true })
    .waitFor();
  await page
    .getByRole("heading", { name: "راهنمای انتخاب قطع کتاب", exact: true })
    .waitFor();
  await screenshot("magazine-list");
  await page
    .getByRole("link")
    .filter({
      has: page.getByRole("heading", {
        name: "راهنمای انتخاب قطع کتاب",
        exact: true,
      }),
    })
    .click();
  await page
    .getByRole("heading", {
      level: 1,
      name: "راهنمای انتخاب قطع کتاب",
      exact: true,
    })
    .waitFor();
  check(
    (await page.locator(".article-content h2").count()) === 1 &&
      (await page.locator(".article-content h3").count()) === 1 &&
      (await page.locator(".article-content li").count()) === 2 &&
      (await page.locator(".article-content table").count()) === 1,
    "article Markdown renders correct heading/list/table structures",
  );
  check(
    (await page.locator(".article-back a").count()) === 1 &&
      (await page
        .locator(
          ".article-back a[href='/calculator'],.article-back a[href='/order'],a[href='/portfolio'],a[href='/tickets/new']",
        )
        .count()) === 0,
    "article footer has no removed calculator/order or retired feature links",
  );
  check(
    (await page.locator("link[rel=canonical]").getAttribute("href")).endsWith(
      "/blog/browser-print-guide",
    ) &&
      (await page
        .locator("meta[property='og:type']")
        .getAttribute("content")) === "article",
    "article SSR metadata has the canonical URL and correct Open Graph type",
  );
  await screenshot("magazine-detail");
  await page.goto(base + "/admin/articles/" + id);
  await page.getByRole("button", { name: "لغو انتشار", exact: true }).waitFor();
  const unpublish = page.waitForResponse(
    (r) =>
      r.url().endsWith("/api/admin/articles/" + id) &&
      r.request().method() === "PUT",
  );
  await page.getByRole("button", { name: "لغو انتشار", exact: true }).click();
  assert.equal((await unpublish).status(), 200);
  await api("GET", "public/articles/browser-print-guide", undefined, 404);
  await page.goto(base + "/blog");
  check(
    (await page.locator(".magazine-card").count()) === 0,
    "unpublished article disappears from the next server-rendered public list",
  );
  const sitemapDraft = await api("GET", "admin/articles/" + id);
  const sitemapCreated = await api(
    "POST",
    "admin/articles",
    { ...sitemapDraft, slug: "sitemap", status: "draft" },
    201,
  );
  const sitemapArticle = await api(
    "GET",
    "admin/articles/" + sitemapCreated.id,
  );
  await api("PUT", "admin/articles/" + sitemapCreated.id, {
    ...sitemapArticle,
    status: "published",
  });
  await page.goto(base + "/blog/sitemap");
  await page
    .getByRole("heading", { level: 1, name: sitemapArticle.title, exact: true })
    .waitFor();
  check(
    true,
    "the article slug sitemap opens its real SSR article instead of colliding with sitemap metadata",
  );
  check(
    (await api("GET", "public/magazine/sitemap")).some(
      (a) => a.slug === "sitemap",
    ),
    "the separate published-only sitemap metadata endpoint remains functional",
  );
  const category="صفحه‌بندی آزمایشی";
  const slugs=[];
  for(let i=1;i<=13;i++){
    const slug="pagination-guide-"+i;
    const draft=await api("POST","admin/articles",{...sitemapArticle,title:"مقاله آزمایشی "+i,category,slug,status:"draft"},201);
    const savedDraft=await api("GET","admin/articles/"+draft.id);
    await api("PUT","admin/articles/"+draft.id,{...savedDraft,status:"published"});
    slugs.push(slug);
  }
  await page.goto(base+"/blog");
  await page.getByRole("navigation",{name:"دسته‌بندی مجله"}).getByRole("link",{name:category,exact:true}).click();
  const pager=page.getByRole("navigation",{name:"صفحه‌بندی مجله"});
  const visibleSlugs=()=>page.getByRole("main").getByRole("link").filter({has:page.getByRole("heading")}).evaluateAll(links=>links.map(a=>a.pathname.split("/").at(-1)));
  const first=await visibleSlugs();
  await pager.getByRole("link",{name:"صفحه بعد"}).click();
  await pager.getByRole("link",{name:"صفحه قبل"}).waitFor();
  const second=await visibleSlugs();
  check(first.length===12 && second.length===1 && JSON.stringify([...first,...second].sort())===JSON.stringify(slugs.sort()),"category pagination preserves all published articles exactly once");
  check(await page.getByRole("searchbox").count()===0,"public magazine uses categories without search");

}
