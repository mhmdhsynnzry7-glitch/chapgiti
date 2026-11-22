import assert from "node:assert/strict";
import {mkdirSync,writeFileSync} from "node:fs";
import path from "node:path";

// Runs only in the loopback browser harness with a memory-only API and synthetic identities.
export async function workflowModernizationChecks({page, browser, base, check, control}) {
  const dir = path.join(process.env.CHAP_BROWSER_EVIDENCE || ".qa/modern-workflow", "screens");
  mkdirSync(dir,{recursive:true});
  const guest = await browser.newContext();
  await guest.route("**/*",route=>route.request().url().startsWith(base) ? route.continue() : route.abort());
  const publicPage=await guest.newPage(), results=[];
  async function inspect(target, key, url, open, capture=false) {
    const response=await target.goto(base+url);
    if (response) assert.ok(response.ok(),key+" response");
    else {
      const fragment = new URL(base + url).hash;
      assert.ok(fragment && new URL(target.url()).hash === fragment, key + " fragment navigation");
      // Fragment navigation can overlap React replacing the streamed page content.
      await target.waitForFunction(selector => {
        const matches = document.querySelectorAll(selector);
        return matches.length === 1 && matches[0].checkVisibility({ visibilityProperty: true });
      }, fragment);
    }
    if (open) await open(target);
    if (["profile","recovery","notifications"].includes(key)) {
      await target.locator(".customer-header-link[aria-current=page]").waitFor();
      check(true,key+" highlights the account navigation");
    }
    if (key==="request") {
      await target.locator('.header-actions a[href="/order"][aria-current=page]').waitFor();
      check(true,"request creation marks the order action as current");
    }
    for (const width of [1440,768,390]) {
      await target.setViewportSize({width,height:width===390?844:1000});
      await target.evaluate(()=>document.fonts.ready);
      await target.evaluate(()=>new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve))));
      const geometry=await target.evaluate(()=>({rtl:document.documentElement.dir==="rtl",overflow:document.documentElement.scrollWidth-innerWidth}));
      results.push({page:key,width,...geometry});
      writeFileSync(path.join(dir,"geometry.json"),JSON.stringify(results,null,2));
      if (capture) await target.screenshot({path:path.join(dir,key+"-"+width+".png"),animations:"disabled"});
      check(geometry.rtl && geometry.overflow<=1, key+" RTL, no horizontal overflow at "+width);
    }
    await target.setViewportSize({width:1440,height:1000});
  }
  try {
    await publicPage.goto(base+"/calculator");
    await publicPage.locator("form input[name=password]").waitFor();
    check(new URL(publicPage.url()).searchParams.get("next")==="/calculator" && !await publicPage.locator(".book-calculator").count(),"direct guest navigation never mounts calculator inputs");
    await page.goto(base+"/admin/settings");await page.getByRole("button",{name:"تعرفه‌های چاپ",exact:true}).click();
    const selector=page.getByLabel("انتخاب قطع کتاب",{exact:true});
    async function chooseCut(name) {
      await selector.click();
      const search=page.getByPlaceholder("جست‌وجو…");
      await search.fill(name);
      const option=page.getByRole("option").filter({hasText:name}).first();
      await option.click();
    }
    await chooseCut("رقعی");
    const editor=page.locator(".cut-tariff-panel");
    const bw=editor.getByRole("textbox",{name:"چاپ هر صفحه سیاه و سفید",exact:true});
    await bw.waitFor();
    check(await editor.locator("fieldset input").count()===11,"all eleven cut-specific prices are editable under their actual catalog cut");
    await bw.fill("۱۲۳۴");
    await editor.getByText(/تغییرات ذخیره‌نشده/).waitFor();
    page.once("dialog",dialog=>dialog.dismiss());
    await chooseCut("وزیری");
    check((await selector.innerText()).includes("رقعی") && (await bw.inputValue()).replaceAll(",","")==="1234","dismissing a cut change preserves Persian-number edits");
    const saved=page.waitForResponse(r=>r.request().method()==="PUT" && r.url().includes("/admin/print-tariffs/"));
    await editor.getByRole("button",{name:"ذخیره تعرفه این قطع"}).click();
    check((await saved).ok(),"only the selected cut is versioned on explicit save");
    await page.goto(base+"/calculator");await page.getByRole("button",{name:"محاسبه هزینه",exact:true}).click();
    await page.locator(".price-summary").waitFor();
    const previous=await page.locator(".price-summary strong").innerText();
    const reestimate=page.waitForResponse(r=>r.request().method()==="POST" && r.url().endsWith("/api/quote"));
    await page.locator(".catalog-trigger").first().click();
    await page.getByRole("option").filter({hasText:"وزیری"}).first().click();
    check((await reestimate).ok(),"changing a cut after estimation automatically calls the authenticated quote API");
    await page.locator(".price-summary").getByText(/وزیری/).waitFor();
    check((await page.locator(".price-summary strong").innerText())!==previous,"the new cut replaces the old estimate");
    await page.route("**/api/quote",r=>r.fulfill({status:401,json:{error:"نشست منقضی"}}));
    await page.getByRole("button",{name:"محاسبه هزینه",exact:true}).click();
    await page.getByRole("link",{name:"ورود دوباره"}).waitFor();
    check(!await page.locator(".price-summary").count(),"expired session hides the result and offers customer sign-in");
    await page.unroute("**/api/quote");
    await control({seedOrdersFor:"09000000826",orderCount:2});
    const ownerOrders=await (await page.request.get(base+"/api/customer/orders?page=1")).json(), order=ownerOrders.items[0];
    const ticket=await page.request.post(base+"/api/tickets",{headers:{Origin:base},data:{idempotencyKey:crypto.randomUUID(),orderId:order.id,subject:"پرسش دربارهٔ نمونه",message:"نسخهٔ فایل آزمایشی را بررسی کنید."}});
    assert.equal(ticket.status(),201);
    const ticketId=(await ticket.json()).id;
    const tariff=async t=>{await t.getByRole("button",{name:"تعرفه‌های چاپ",exact:true}).click();await chooseCut("رقعی");await bw.waitFor();};
    const views=[
      [publicPage,"home","/",null,true], [publicPage,"login","/customer?change=1"],[publicPage,"registration","/customer?mode=register"],
      [publicPage,"recovery","/customer/forgot-password"],[page,"profile","/customer/profile"],
      [page,"account","/account",t=>t.locator(".customer-orders tbody tr").first().waitFor(),true],[page,"calculator","/calculator",t=>t.locator(".book-calculator").waitFor(),true],
      [page,"request","/order"],[page,"my-orders","/account/orders",t=>t.locator("tbody tr").first().waitFor(),true],
      [page,"order","/account/orders/"+order.id],[page,"files","/account/orders/"+order.id+"#order-files"],[page,"support","/tickets"],
      [page,"conversation","/tickets/"+ticketId,t=>t.locator(".ticket-bubble").first().waitFor(),true],[page,"notifications","/account/notifications"],
      [page,"admin","/admin"],[page,"admin-orders","/admin/orders"],[page,"tariffs","/admin/settings",tariff,true],
      [page,"catalog","/admin/print-settings"],[page,"customers","/admin/customers"],[page,"magazine","/admin/articles"],[page,"settings","/admin/settings"]
    ];
    for(const args of views) await inspect(...args);
    await page.goto(base+"/account/orders/"+order.id);
    check(await page.locator('#giti-main-nav a[aria-current="page"]').getAttribute("href")==="/account/orders","nested customer order highlights its parent navigation item");
    await page.goto(base+"/admin/orders/"+order.id);
    const adminMenu=page.locator(".admin-nav");
    await adminMenu.waitFor({state:"visible"});
    const activeOrder=adminMenu.locator('a[aria-current="page"][href="/admin/orders"]');
    await activeOrder.waitFor({state:"visible"});
    await page.locator(".timeline").getByText(/ثبت‌کننده مشخص نیست/).first().waitFor();
    check(true,"an order history event with no recorded author renders without breaking the admin detail");
    await page.getByText("ثبت نشده / ثبت نشده",{exact:true}).waitFor();
    check(true,"an incomplete historical specification displays its missing page and copy counts without inventing values");
    check(await adminMenu.locator('[aria-current="page"]').count()===1,"administrator detail highlights only the order parent");
  } finally {await guest.close();}
}
