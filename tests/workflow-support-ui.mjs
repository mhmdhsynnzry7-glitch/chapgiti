import { submitPrintRequest } from "./pdf-fixture.mjs";
import { acceptFinalConditions } from "./final-terms-browser.mjs";
import assert from "node:assert/strict";

// Runs only against the existing isolated API/browser harness. No operational SQL or SMS.
export async function workflowSupportChecks({ page, base, check,control }) {
  const request = (method, path, body) => page.evaluate(async ({method,path,body}) => {
    const response = await fetch("/api/" + path, {method, headers:{"Content-Type":"application/json"}, ...(body ? {body:JSON.stringify(body)} : {})});
    return { status: response.status, body: await response.json() };
  }, {method,path,body});
  await page.goto(base + "/admin/customers");
  await page.getByRole("heading", { name:"مشتریان", exact:true, level:2 }).waitFor();
  const nav=page.locator(".admin-nav");
  for (const name of ["کاربران","حساب‌های مدیران","پیام‌های دریافتی","گزارش پیامک"]) check(await nav.getByRole("link", {name,exact:true}).count()===0, "duplicate/obsolete sidebar entry removed: " + name);
  check(await nav.getByRole("link",{name:"پروفایل مدیر",exact:true}).isVisible() && await nav.getByRole("link",{name:"تاریخچه مدیریت",exact:true}).isVisible(), "administrator profile and separate management audit remain available");
  await page.locator(".admin-content tbody tr").first().waitFor();
  check(!/[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9-]{23}/i.test(await page.locator(".admin-content table").innerText()) && await page.getByLabel("نقش تاریخی").count()===0 && await page.getByLabel("وضعیت شماره").count()===0, "customer directory has no visible GUIDs or obsolete filters");
  await page.locator(".admin-content tbody").getByRole("button",{name:"مشاهده جزئیات"}).first().click();
  check(await page.getByRole("dialog").locator("input").count()===0, "customer details are read-only"); await page.keyboard.press("Escape");
  await page.goto(base + "/admin/users"); await page.waitForURL("**/admin/customers");check(true,"legacy users URL redirects to the consolidated directory");

  const spec={pages:120,quantity:1,size:"raghi",paper:"white",color:"bw",binding:"soft"};
  const created=await submitPrintRequest(page,base,{idempotencyKey:crypto.randomUUID(),title:"Workflow browser book",spec});
  assert.equal(created.status,201); const id=created.body.id;
  const serverContext=await page.context().browser().newContext({javaScriptEnabled:false});
  try {
    await serverContext.addCookies(await page.context().cookies());
    await serverContext.route('**/*',route=>route.request().url().startsWith(base)?route.continue():route.abort());
    const serverPage=await serverContext.newPage();await serverPage.goto(base+'/admin/orders/'+id);
    check(await serverPage.locator('.order-editor').isVisible(),"administrator order specification is rendered before JavaScript hydration");
    check(await serverPage.locator('.order-workflow-actions button').evaluateAll(buttons=>buttons.length>0&&buttons.every(button=>button.disabled))&&await serverPage.getByRole('button',{name:'ثبت یادداشت اجرایی',exact:true}).isDisabled(),"server-rendered order actions stay disabled until explicit POST open succeeds");
    const stillNew=await request('GET','admin/orders/'+id);
    check(stillNew.body.order.status==='new'&&stillNew.body.events.every(event=>event.status!=='review'),"SSR GET with JavaScript disabled never starts order review");
  } finally {await serverContext.close();}
  let unrelatedLists=0;
  const countList=request=>{if(/\/api\/admin\/orders\?/.test(request.url()))unrelatedLists++;};
  page.on('request',countList);
  await page.goto(base + "/admin/orders/" + id,{waitUntil:'domcontentloaded'});
  await page.getByRole("button",{name:"ارسال پیشنهاد قیمت",exact:true}).waitFor();
  await page.waitForFunction(()=>[...document.querySelectorAll('main button')].some(button=>button.textContent?.trim()==='ارسال پیشنهاد قیمت'&&!button.disabled&&button.getBoundingClientRect().width>0));
  page.off('request',countList);
  check(unrelatedLists===0,"opening order details does not fetch an unused administrator order list");
  await page.getByRole("button",{name:"ارسال پیشنهاد قیمت",exact:true}).waitFor();
  const opened=await request("GET","admin/orders/"+id);
  check(opened.body.order.status==="review" && opened.body.order.final_price===null && opened.body.events.filter(event=>event.status==="review").length===1,"explicit admin screen opening starts review once without approving the suggested price");
  
  const amount=page.getByRole("textbox",{name:"مبلغ پیشنهادی (تومان)",exact:true});
  check(await amount.inputValue()==="" && await page.locator(".order-editor input[type=number]").count()===0,"proposal amount is explicit and uses grouped text without numeric spinners");
  await amount.fill("۴۴۰٬۰۰۰");await amount.blur();
  check(await amount.inputValue()==="440,000","Persian approved amount normalizes and groups while retaining a numeric API contract");
  await page.getByLabel("خدمات شامل قیمت",{exact:true}).fill("Printed book with specified binding");
  const approve=page.waitForResponse(response=>response.url().endsWith("/api/admin/orders/"+id+"/quotes")&&response.request().method()==="POST");
  await page.getByRole("button",{name:"ارسال پیشنهاد قیمت",exact:true}).click();
  const response=await approve;check(response.status()===200 && response.request().postDataJSON().amountToman===440000,"proposal persists the numeric amount without starting production");
  const pending=await request("GET","customer/orders/"+id),proposal=pending.body.quotes.at(-1);
  assert.equal((await request("POST","customer/orders/"+id+"/quote/accept",{idempotencyKey:crypto.randomUUID(),expectedVersion:pending.body.order.version,quoteId:proposal.id,quoteVersion:proposal.version})).status,200);
  const owner=await page.context().newPage();
  let linkedTicketId;
  try { linkedTicketId=await acceptFinalConditions(page,owner,base,id); } finally { await owner.close(); }
  await page.getByRole("button",{name:"شروع چاپ",exact:true}).waitFor();
  page.once("dialog",dialog=>dialog.accept());await page.getByRole("button",{name:"شروع چاپ",exact:true}).click();
  await page.getByRole("button",{name:"پایان چاپ و ارسال به صحافی",exact:true}).waitFor();
  await page.locator('textarea[name=operationalNote]').fill("Private browser operational note");
  const savedNote=page.waitForResponse(response=>response.url().endsWith("/notes")&&response.request().method()==="POST");
  await page.getByRole("button",{name:"ثبت یادداشت اجرایی",exact:true}).click();assert.equal((await savedNote).status(),200);
  await page.locator(".operational-note").filter({hasText:"Private browser operational note"}).waitFor();
  check(true,"private operational note is appended with history");
  const beforeReload=(await request("GET","admin/orders/"+id)).body.events.map(e=>e.id);
  await page.reload();await page.getByRole("button",{name:"پایان چاپ و ارسال به صحافی",exact:true}).waitFor();
  const reopened=await request("GET","admin/orders/"+id);
  check(JSON.stringify(reopened.body.events.map(e=>e.id))===JSON.stringify(beforeReload),"reopening admin details does not duplicate the review event");
  for(const label of ["پایان چاپ و ارسال به صحافی","پایان صحافی و آماده تحویل","ثبت تحویل سفارش"]) {
    await page.getByRole("button",{name:label,exact:true}).waitFor();page.once("dialog",dialog=>dialog.accept());
    const updated=page.waitForResponse(response=>response.url().endsWith("/api/admin/orders/"+id)&&response.request().method()==="PATCH");
    await page.getByRole("button",{name:label,exact:true}).click();assert.equal((await updated).status(),200);
  }
  await page.getByText("این سفارش به وضعیت پایانی رسیده است.",{exact:true}).waitFor();check(true,"context actions complete the print-binding-ready-delivered journey");
  await page.goto(base + "/account/orders/"+id);await page.locator(".customer-order-details:visible").waitFor();
  const activeOrder = page.locator(".customer-order-details:visible");
  check(!(await page.locator("main").innerText()).includes("Private browser operational note") && (await activeOrder.locator(".order-price-totals").innerText()).includes("قیمت نهایی سفارش"),"customer sees approved total but never operational notes");
  check(await activeOrder.getByRole("list",{name:"مراحل سفارش"}).getByRole("listitem").count()===9,"bound book shows the nine agreed stages including binding");
  const support=await request("POST","tickets",{orderId:id,subject:"پشتیبانی سفارش آزمایشی",message:"پیگیری سفارش تکمیل‌شده آزمایشی",idempotencyKey:crypto.randomUUID()});
  assert.equal(support.status,200);const ticketId=support.body.id;
  assert.equal(ticketId,linkedTicketId);
  await page.goto(base+"/admin/tickets/"+ticketId);
  await page.getByText("پیگیری سفارش تکمیل‌شده آزمایشی",{exact:true}).waitFor();
  check(await page.getByRole("button",{name:"ارسال پاسخ",exact:true}).isVisible(),"order-linked support permits explicit admin replies");
  await page.goto(base+"/admin/activity");await page.getByLabel("نوع فعالیت").selectOption("ticket_created");
  await page.locator('.admin-content a[href="/admin/tickets/'+ticketId+'"]').waitFor();check(true,"activity timeline links actual support creation to its conversation");
  await page.getByLabel("از تاریخ (میلادی)").fill("2099-01-01");await page.getByText("موردی برای نمایش پیدا نشد.",{exact:true}).waitFor();check(true,"activity date filter has an honest empty state");
  await page.goto(base+"/admin/sms");await page.getByRole("heading",{name:"گزارش پیامک‌های سفارش",exact:true}).waitFor();check(true,"legacy SMS log route remains functional independently of customer activity");
  await page.goto(base+"/contact");check(await page.locator("main form, main textarea").count()===0 && await page.locator('main a[href="/tickets"]').count()===0,"contact is informational with no ticket creation entry point");
  await page.goto(base+"/blog");await page.getByRole("heading",{name:"مجله چاپ گیتی",exact:true}).waitFor();check(true,"restored magazine stays independent from the retired support mutations and active calculator");
  await page.goto(base+"/calculator");check(await page.locator(".book-calculator").count()>0 || await page.getByRole("heading").filter({hasText:"محاسبه"}).count()>0,"main calculator remains available");
  await page.goto(base+"/admin/customers");await page.setViewportSize({width:1024,height:768});
  check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),"admin customer directory fits a tablet viewport");await page.setViewportSize({width:1440,height:1000});
}
