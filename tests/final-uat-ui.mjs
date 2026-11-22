import {pdfFixture} from "./pdf-fixture.mjs";
import { mkdirSync } from "node:fs";
export async function finalUatChecks({ page, base, check }) {
  const folder = "docs/screenshots/final-uat";
  mkdirSync(folder, { recursive: true });
  const groups = page
    .getByRole("group", { name: "گروه تنظیمات چاپ" })
    .getByRole("button");
  await groups.first().waitFor();
  check(
    (await groups.count()) === 8,
    "all eight print catalog groups stay on one page",
  );
  await page.getByRole("table").first().waitFor();
  for (const [name, width] of [
    ["desktop", 1440],
    ["tablet", 800],
    ["mobile", 390],
  ]) {
    await page.setViewportSize({ width, height: 900 });
    check(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth + 1,
      ),
      "catalog has no page overflow at " + width,
    );
    if (name === "mobile") {
      await page.waitForFunction(()=>document.querySelector(".admin-sidebar")?.getBoundingClientRect().left >= innerWidth-1);
      check(true,"closed navigation clears the mobile catalog");
    }
    await page.screenshot({ path: folder + "/catalog-" + name + ".png", animations:"disabled" });
  }
  await page.getByRole("button", { name: "افزودن گزینه", exact: true }).click();
  await page.getByRole("dialog").waitFor();
  check(
    await page.getByRole("dialog").isVisible(),
    "catalog editor opens in modal",
  );
  await page.keyboard.press("Escape");
  await page.getByRole("dialog").waitFor({ state: "hidden" });
  await page.getByLabel("جست‌وجو", { exact: true }).fill("ZZZZ-absent");
  check(
    (await page.getByRole("cell").count()) === 0,
    "catalog search filters rows",
  );
  await page.getByLabel("جست‌وجو", { exact: true }).fill("");
  const size = page.getByLabel("تعداد در صفحه", { exact: true }).first();
  const atBottom=await size.evaluate(el=>{
    const region=el.closest("section[aria-label]");
    return !!region && !!(region.querySelector("table").compareDocumentPosition(el) & Node.DOCUMENT_POSITION_FOLLOWING);
  });
  check(atBottom,"page size follows the table in reading and keyboard order");
  await size.selectOption("10");
  check(
    (await page.getByRole("row").count()) <= 11,
    "catalog page size controls result rows",
  );
  const sort = page.getByRole("button", { name: "کد داخلی ↕" });
  await sort.click();
  check(
    (await sort.locator("..").getAttribute("aria-sort")) === "ascending",
    "table discloses its sort direction",
  );
  const codes = page.getByRole("table").locator("tbody tr td:nth-child(2)");
  const collator = new Intl.Collator("fa", { numeric: true });
  const ascending = await codes.allTextContents();
  check(ascending.length > 1 && ascending.every((code, i) => !i || collator.compare(ascending[i - 1], code) <= 0),
    "catalog rows follow ascending code order");
  await sort.click();
  const descending = await codes.allTextContents();
  check(descending[0] !== ascending[0] && descending.every((code, i) => !i || collator.compare(descending[i - 1], code) >= 0),
    "sorting the same column again updates the rendered rows");
  await page.getByLabel("جست‌وجو", { exact: true }).fill(descending[0]);
  check((await codes.allTextContents()).includes(descending[0]) && await codes.count() < descending.length,
    "search uses the current sorted rows");
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.goto(base + "/admin/services");
  await page.getByRole("table").waitFor();
  await page.screenshot({ path: folder + "/services-desktop.png" });
  await page
    .getByRole("table")
    .getByRole("link", { name: "ویرایش" })
    .first()
    .click();
  await page.getByRole("button", { name: "افزودن بخش" }).click();
  check(
    await page.getByRole("dialog").isVisible(),
    "service descriptions edit one section at a time",
  );
  await page.keyboard.press("Escape");
  await page.goto(base + "/admin/orders");
  check(
    (await page
      .getByRole("button", { name: "سفارش‌های جدید", exact: true })
      .count()) === 0,
    "order filtering replaces redundant new-orders button",
  );
  check(
    (await page
      .locator(".admin-nav")
      .getByText("اعلان‌ها", { exact: true })
      .count()) === 0,
    "admin sidebar omits notifications",
  );
  await page
    .getByRole("button", { name: /^اعلان‌ها/ })
    .first()
    .click();
  check(
    await page.getByRole("button", { name: "خواندن همه" }).isVisible(),
    "notification bell opens persistent read controls",
  );
  await page.keyboard.press("Escape");
  await page.setViewportSize({ width: 390, height: 900 });
  await page.goto(base + "/blog");
  check(
    (await page.getByRole("searchbox").count()) === 0,
    "magazine has no public search",
  );
  await page.screenshot({ path: folder + "/magazine-mobile.png" });
  await page.goto(base + "/tickets");
  await page.locator("#new-ticket").waitFor();
  check(
    (await page.getByRole("table").count()) > 0,
    "support list and composer are available",
  );
  await page.goto(base + "/order");
  await page.locator("input[type=file]").first().waitFor({ state: "attached" });
  check(
    await page.getByRole("button", { name: "ارسال درخواست چاپ" }).isDisabled(),
    "order cannot submit without primary PDF and consent",
  );
  await page.getByRole("textbox",{name:"عنوان کتاب یا پروژه"}).fill("کتاب نمونهٔ پذیرش");
  await page.locator("input[type=file]").first().setInputFiles({name:"invalid.pdf",mimeType:"application/pdf",buffer:Buffer.from("ordinary text")});
  await page.getByText("محتوای فایل PDF معتبر نیست.",{exact:true}).waitFor();
  await page.locator("input[type=file]").first().setInputFiles({name:"uat.pdf",mimeType:"application/pdf",buffer:pdfFixture()});
  await page.getByRole("checkbox",{name:/شرایط ثبت سفارش/}).check();
  const create=page.waitForResponse(r=>r.request().method()==="POST"&&new URL(r.url()).pathname==="/api/orders");
  await page.getByRole("button",{name:"ارسال درخواست چاپ"}).click();
  const response=await create;check(response.status()===201,"valid PDF and explicit consent create a request through the form");
  const {id}=await response.json();
  const adminOrder=await page.context().newPage();
  try{
    await adminOrder.goto(base+"/admin/orders/"+id);
    check(await adminOrder.getByRole("button",{name:"تأیید فایل تولید"}).count()===0,"review uses the next business action");
    await adminOrder.getByRole("link",{name:"uat.pdf",exact:true}).first().waitFor();
    const amount=adminOrder.getByLabel("مبلغ پیشنهادی (تومان)");
    await amount.fill("4440000");check(await amount.inputValue()==="4,440,000","quote input groups editable integer toman");
    await adminOrder.locator(".admin-quote-panel").screenshot({path:folder+"/price-proposal.png"});
    await adminOrder.getByLabel("خدمات شامل قیمت").fill("چاپ کتاب مطابق فایل تأییدشده");
    const proposal=adminOrder.waitForResponse(r=>r.request().method()==="POST"&&r.url().endsWith("/quotes"));
    await adminOrder.getByRole("button",{name:"ارسال پیشنهاد قیمت",exact:true}).click();check((await proposal).ok(),"administrator issues quote");
    await page.goto(base+"/account/orders/"+id);
    page.once("dialog",d=>d.accept());
    const acceptance=page.waitForResponse(r=>r.url().endsWith("/quote/accept"));
    await page.getByRole("button",{name:"تأیید قیمت",exact:true}).click();check((await acceptance).ok(),"owner accepts the quoted amount");
    await adminOrder.reload();await adminOrder.getByText("تنظیم شرایط نهایی",{exact:true}).click();
    check(await adminOrder.getByRole("button",{name:"ارسال پیشنهاد قیمت",exact:true}).count()===0,"accepted order hides re-quote");
    await adminOrder.locator("textarea[name=delivery]").fill("تحویل حضوری روز سه‌شنبه");await adminOrder.locator("textarea[name=scope]").fill("چاپ و صحافی");
    const conditions=adminOrder.waitForResponse(r=>r.request().method()==="POST"&&r.url().endsWith("/delivery-terms"));
    await adminOrder.getByRole("button",{name:"ارسال شرایط نهایی برای تأیید مشتری"}).click();check((await conditions).ok(),"versioned nonfinancial conditions saved");
    await page.reload();await page.getByText("تحویل حضوری روز سه‌شنبه",{exact:true}).first().waitFor();await page.screenshot({path:folder+"/final-review-mobile.png"});
    const agreement=page.locator(".customer-order-details:visible .delivery-conditions");
    await agreement.locator("summary").filter({hasText:"درخواست اصلاح شرایط"}).click();
    await agreement.getByLabel("شرح اصلاح شرایط").fill("زمان دریافت را به چهارشنبه تغییر دهید.");
    const amendment=page.waitForResponse(r=>r.url().endsWith("/delivery-terms/request-amendment"));
    await agreement.getByRole("button",{name:"ارسال درخواست اصلاح شرایط"}).click();
    check((await amendment).ok(),"customer requests an amendment to exactly the pending terms");
    await adminOrder.reload();await adminOrder.getByText("تنظیم شرایط نهایی",{exact:true}).click();
    await adminOrder.getByLabel("شرایط تحویل",{exact:true}).fill("دریافت حضوری چهارشنبه");
    const updated=adminOrder.waitForResponse(r=>r.request().method()==="POST"&&r.url().endsWith("/delivery-terms"));
    await adminOrder.getByRole("button",{name:"ارسال شرایط نهایی برای تأیید مشتری"}).click();
    check((await updated).ok(),"administrator issues replacement terms without repricing");
    await page.reload();await page.getByText("دریافت حضوری چهارشنبه",{exact:true}).first().waitFor();
    const final=page.waitForResponse(r=>r.url().endsWith("/delivery-terms/accept"));
    await agreement.getByRole("button",{name:"تأیید نهایی و ثبت توافق"}).click();check((await final).ok(),"customer explicitly authorizes final conditions");
    await adminOrder.reload();const start=adminOrder.getByRole("button",{name:"شروع چاپ",exact:true});await start.waitFor();
    const production=adminOrder.waitForResponse(r=>r.url().endsWith("/start-printing"));adminOrder.once("dialog", d => d.accept());await start.click();check((await production).ok(),"production starts for the accepted price and approved PDF");
  }finally{await adminOrder.close();}
  await page.goto(base + "/calculator");
  await page.locator("#spec-extras-title").waitFor();
  const headings=await page.locator(".spec-section h2, #spec-extras-title").allTextContents();
  check(headings.indexOf("تیراژ و صفحات") > headings.findIndex(h=>h.startsWith("خدمات تکمیلی")) && headings.some(h=>h.startsWith("خدمات تکمیلی")),"quantity follows finishing options");
  const trim = page.locator(".catalog-trigger").first();
  await trim.click();
  const picker = page.locator(".catalog-popup");
  await picker.waitFor();
  const fit = await picker.boundingBox();
  check(fit && fit.x >= 0 && fit.x + fit.width <= 391 && fit.height < 500, "mobile selector stays inside the viewport");
  const search = picker.locator("input");
  await search.fill("ZZZ-no-match");
  await picker.getByText("گزینه‌ای پیدا نشد.").waitFor();
  await page.keyboard.press("Escape");
  await picker.waitFor({state:"hidden"});
  check(await trim.evaluate(el=>el===document.activeElement),"keyboard dismissal returns focus to the catalog trigger");
  await page.getByRole("button", { name: "محاسبه هزینه", exact: true }).click();
  await page.locator(".price-summary").waitFor();
  check(
    (await page.locator(".price-summary dt").count()) === 8 && await page.getByText("برآورد کل",{exact:true}).isVisible() && await page.getByText("مبلغ صرفه‌جویی").count() === 0,
    "calculator summarizes the selected cut, quantity, finishing, discount and estimate",
  );
  await page.screenshot({ path: folder + "/calculator-mobile.png" });
  await page.goto(base+"/tickets");
  await page.locator("#new-ticket input[name=subject]").fill("درخواست آزمایشی چاپ");
  await page.locator("#new-ticket textarea").fill("لطفاً نمونه کتاب را بررسی کنید.");
  await page.locator("#new-ticket button").click();
  await page.waitForURL(/\/tickets\/[\da-f-]{36}$/);
  const ticket=page.url().split("/").at(-1);
  const manager=await page.context().newPage();
  try{
    await manager.goto(base+"/admin/tickets/"+ticket);
    await manager.locator(".ticket-thread textarea").fill("پاسخ ثبت‌شدهٔ چاپخانه");
    await manager.locator(".ticket-thread form button").click();
    await manager.getByText("پاسخ ثبت‌شدهٔ چاپخانه",{exact:true}).waitFor();
    await page.bringToFront();
    await page.getByText("پاسخ ثبت‌شدهٔ چاپخانه",{exact:true}).waitFor({timeout:20000});
    check(await page.locator(".ticket-bubble").count()===2,"customer receives persisted administrator reply once");
    await page.reload();await page.getByText("پاسخ ثبت‌شدهٔ چاپخانه",{exact:true}).waitFor();
    check(await page.locator(".ticket-bubble").count()===2,"thread survives refresh without duplicates");
    await page.screenshot({path:folder+"/support-mobile.png"});
    await page.goto(base+"/account/notifications");
    const item=page.locator(".notification-item").first();await item.waitFor();
    check(await item.locator("a").count()===0 && await item.locator(".notification-open button").count()===0,"notification text has no entity navigation");
    const before=page.url(); await item.locator(".notification-open").click();
    check(page.url()===before,"reading notification text keeps the user on the same page");
    const unread = page.locator(".notification-item.is-unread");
    const count = await unread.count();
    await unread.first().getByRole("button",{name:/خواندن اعلان/}).click();
    await page.waitForFunction(n => document.querySelectorAll(".notification-item.is-unread").length < n, count);
    check(page.url()===before,"marking an individual notification read preserves location");
    await page.getByRole("button",{name:"خواندن همه"}).click();
    await page.locator(".notification-item.is-unread").first().waitFor({state:"detached"});
    check(await page.locator(".notification-item.is-unread").count()===0,"mark-all-read updates the persisted list");
  }finally{await manager.close();}

}
