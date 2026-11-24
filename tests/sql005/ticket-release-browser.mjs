import assert from "node:assert/strict";
import { pdfFixture } from "../pdf-fixture.mjs";
import { acceptFinalConditions } from "../final-terms-browser.mjs";

const png = Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+ipUgAAAAASUVORK5CYII=","base64");
const upload = (name, buffer, mimeType) => ({name,buffer,mimeType});
const mutation = (page, suffix) => page.waitForResponse(r => r.request().method() === "POST" && r.url().endsWith(suffix));

export async function ticketReleaseBrowser({browser,base,check,artifacts}) {
  assert.equal(new URL(base).hostname,"127.0.0.1");
  const contexts=[];
  const errors=[];
  async function tab(){
    const ctx=await browser.newContext({viewport:{width:1440,height:950}});
    contexts.push(ctx);
    await ctx.route("**/*", r=>r.request().url().startsWith(base+"/") ? r.continue() : r.abort());
    const page=await ctx.newPage();page.on("pageerror",e=>errors.push(e.message));page.setDefaultTimeout(12000);
    return page;
  }
  async function api(page,path,method="GET",body){
    const r=await page.request.fetch(base+"/api/"+path,{method,headers:{Origin:base},...(body?{data:body}:{})});
    assert(r.ok(),path+" "+r.status());
    return r.json();
  }
  async function matches(fn, label) {
    for(let i=0;i<55;i++){if(await fn())return;await new Promise(done=>setTimeout(done,120));}
    assert.fail(label);
  }
  const admin=await tab(),customer=await tab(),guest=await tab();
  const phone="09177771024", pass="Ticket.Fixture@2026";
  const customerOrder=id=>"customer/orders/"+id, adminOrder=id=>"admin/orders/"+id;
  async function intake(orderId) {
    const kind=orderId?"linked":"general", title=orderId?"پیگیری سفارش آزمایشی":"درخواست پشتیبانی آزمایشی";
    const pdf=pdfFixture("ticket "+kind), pdfName=kind+".pdf";
    await customer.goto(base+"/tickets"+(orderId?"?order="+orderId:""));
    const form=customer.locator("#new-ticket");await form.getByLabel("موضوع تیکت").fill(title);
    await form.getByLabel("متن درخواست").fill("متن درخواست آزمایشی برای بررسی فایل‌های پیام اول");
    const input=form.getByLabel("پیوست فایل (اختیاری)",{exact:true});
    assert(await form.getByText("انتخاب فایل‌ها",{exact:true}).isVisible());
    if (!orderId) {
      for (const [files, problem] of [
        [[upload("wrong.txt", png, "text/plain")], "فایل PDF"],
        [Array.from({length:4}, (_, i) => upload(i+".png",png,"image/png")), "تعداد پیوست"],
        [[upload("large.png", Buffer.alloc(4*1024*1024+1), "image/png")], "حجم پیوست"],
      ]) {
        await input.setInputFiles(files);
        assert((await form.getByRole("alert").innerText()).includes(problem));
        assert.equal(await form.locator(".ticket-file-list li").count(),0);
      }
    }
    await input.setInputFiles(upload(pdfName,pdf,"application/pdf"));
    const transfer=await customer.evaluateHandle(bytes=>{const d=new DataTransfer();d.items.add(new File([Uint8Array.from(bytes)],"نمونه.png",{type:"image/png"}));return d;},[...png]);
    try {await form.locator(".ticket-drop").dispatchEvent("drop",{dataTransfer:transfer});}finally{await transfer.dispose();}
    await form.getByRole("button",{name:"حذف نمونه.png"}).click();
    await input.setInputFiles(upload("نمونه.png",png,"image/png"));
    for(const width of [390,768,1024,1440]){
      await customer.setViewportSize({width,height:900});
      assert(await customer.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1));
      if(!orderId)await form.screenshot({path:artifacts+"/ticket-create-"+width+".png",animations:"disabled"});
    }
    const submit=form.getByRole("button",{name:"ارسال تیکت",exact:true});
    const endpoint=base+"/api/tickets/attachments";
    let committedId;
    if(!orderId){
      await customer.route(endpoint,route=>route.abort("failed"),{times:1});
      await submit.click();await form.getByRole("alert").waitFor();
      assert.equal(await form.locator(".ticket-file-list li").count(),2);
      let responses;
      await customer.route(endpoint,async route=>{
        const sent=await Promise.all([route.fetch(),route.fetch()]);
        responses=sent.map(r=>r.status()).sort();
        const ids=await Promise.all(sent.map(async r=>(await r.json()).id));
        assert.equal(ids[0],ids[1]);committedId=ids[0];await route.abort("failed");
      },{times:1});
      await submit.click();await form.getByRole("alert").waitFor();
      assert.deepEqual(responses,[200,201]);
    }
    const result=mutation(customer,"/tickets/attachments");
    await submit.click();
    const response=await result;assert.equal(response.status(),orderId?201:200);
    const id=(await response.json()).id;if(committedId)assert.equal(id,committedId);
    await customer.waitForURL(base+"/tickets/"+id);
    const cr="tickets/"+id,ar="admin/tickets/"+id;
    const initial=await api(admin,ar);
    assert.equal(initial.messages.length,1);assert.equal(initial.messages[0].attachments.length,2);
    assert.equal(initial.ticket.order_id??null,orderId??null);
    check(true,kind+" created once with the initial PDF and image; file selection, removal and RTL reflow work");
    if(!orderId)check(true,"network failure before upload, concurrent writes and response loss after commit retain one ticket and one set of attachments");
    const notifications=async(page,role)=>(await api(page,role+"/notifications?page=1&pageSize=20")).items;
    const hasTicket=async(page,role)=>(await notifications(page,role)).some(row=>Object.values(row).some(v=>typeof v==="string"&&v.includes(id)));
    await matches(()=>hasTicket(admin,"admin"),"new ticket administrator notification");
    await admin.goto(base+"/admin/tickets/"+id);await admin.locator(".ticket-bubble").first().waitFor();
    for(const [file,bytes] of [[pdfName,pdf],["نمونه.png",png]]){
      const href=await admin.getByRole("link",{name:file,exact:true}).getAttribute("href");
      assert((await (await admin.request.get(base+href)).body()).equals(bytes));
    }
    const fileId=initial.messages[0].attachments[0].id;
    assert.equal((await guest.request.get(base+"/api/"+cr+"/attachments/"+fileId)).status(),401);
    for(const [sender,scope,target,role] of [[admin,ar,customer,"customer"],[customer,cr,admin,"admin"]]){
      // Keep the recipient away from the detail until its unread state is observed.
      const offline = !orderId && role === "customer";
      if (offline) await target.context().setOffline(true);
      else await target.goto(base+(role==="admin"?"/admin":"/account"));
      const name=role+"-response.pdf",bytes=pdfFixture(name);
      await sender.locator(".ticket-reply textarea").fill("پاسخ آزمایشی به درخواست همراه فایل");
      await sender.locator(".ticket-reply input[type=file]").setInputFiles(upload(name,bytes,"application/pdf"));
      const posted=mutation(sender,"/messages/attachments");
      await sender.getByRole("button",{name:"ارسال پاسخ",exact:true}).click();assert.equal((await posted).status(),201);
      await matches(async()=> (await api(target,role+"/notifications/unread-count")).unreadSupportCount>0, "persisted unread support count");
      await matches(()=>hasTicket(target,role),"recipient notification");
      if (offline) {
        await target.context().setOffline(false);
        await target.evaluate(() => window.dispatchEvent(new Event("online")));
      } else await target.goto(base+(role==="admin"?"/admin/tickets/":"/tickets/")+id);
      const link=target.getByRole("link",{name,exact:true});await link.waitFor({timeout:35000});const href=await link.getAttribute("href");
      assert((await (await target.request.get(base+href)).body()).equals(bytes));
      const tr=role==="admin"?ar:cr;
      await matches(async()=> (await api(target,tr)).messages.some(m=>m.read_at && m.attachments?.some(f=>f.name===name)),"message marked read");
    }
    await admin.getByRole("button",{name:"بستن تیکت"}).click();
    await matches(async()=> (await api(customer,cr)).ticket.status==="closed","ticket closed");
    const final=await api(admin,ar);assert.equal(final.messages.length,3);
    assert.deepEqual(final.messages.map(m=>m.attachments.length),[2,1,1]);
    await admin.screenshot({path:artifacts+"/ticket-"+kind+"-admin.png",animations:"disabled"});
    check(true,kind+" real SQL conversation notifies both roles, allows authorized byte-identical downloads, records reads and closes with exactly three messages");
    return id;
  }
  try {
    await customer.goto(base+"/customer?mode=register");
    await customer.locator("input[name=phone]").fill(phone);
    await customer.getByRole("button",{name:"ادامه ثبت‌نام",exact:true}).click();
    await customer.getByLabel("کد تأیید",{exact:true}).fill("۰۱۲۳");
    await customer.getByRole("button",{name:"تأیید کد و ادامه",exact:true}).click();
    await customer.locator("input[name=firstName]").fill("آزمایش");await customer.locator("input[name=lastName]").fill("چاپ گیتی");
    await customer.locator("input[name=password]").fill(pass);await customer.locator("input[name=confirmation]").fill(pass);
    await customer.getByRole("button",{name:"تکمیل ثبت‌نام",exact:true}).click();await customer.locator(".auth-success").waitFor();
    async function login(){
      await customer.goto(base+"/customer?change=1&next=%2Faccount");
      await customer.locator("input[name=phone]").fill(phone);await customer.locator("input[name=password]").fill(pass);
      await customer.locator("form").getByRole("button",{name:"ورود",exact:true}).click();await customer.waitForURL(base+"/account");
    }
    await login();
    const previous = await customer.context().cookies(base);
    await customer.getByRole("button",{name:"خروج از حساب",exact:true}).click();await customer.waitForURL(base+"/");
    await guest.context().addCookies(previous);
    const stale = await guest.request.post(base+"/api/tickets/attachments",{headers:{Origin:base},multipart:{
      subject:"پیوست بعد از خروج",message:"درخواست آزمایشی با نشست منقضی",
      idempotencyKey:crypto.randomUUID(),files:upload("stale.pdf",pdfFixture("revoked"),"application/pdf"),
    }});
    assert.equal(stale.status(),401);await guest.context().clearCookies();
    check(true,"revoked SQL customer session cannot upload an attachment or create a ticket");
    await login();
    await admin.goto(base+"/admin/login");await admin.locator("input[name=password]").fill("Isolated.Sql@2026");
    await admin.getByRole("button",{name:"ورود به مدیریت",exact:true}).click();await admin.waitForURL(base+"/admin");
    assert.equal((await admin.request.get(base+"/api/customer/me")).status(),401);
    assert.equal((await customer.request.get(base+"/api/auth/me")).status(),401);
    check(true,"disposable SQL registration, OTP transport, completed profile, logout and independent customer/admin logins");
    await customer.goto(base+"/calculator");const calc=mutation(customer,"/api/quote");
    await customer.getByRole("button",{name:"محاسبه هزینه",exact:true}).click();assert.equal((await calc).status(),200);
    await customer.getByRole("link",{name:"ارسال درخواست چاپ",exact:true}).click();
    await customer.locator("input[name=title]").fill("پذیرش نهایی چاپ با فایل اصلاحیه");
    const original=pdfFixture("starting version");
    await customer.getByLabel(/فایل متن و جلد را انتخاب کنید/).setInputFiles(upload("release-original.pdf",original,"application/pdf"));
    await customer.getByRole("checkbox",{name:/شرایط ثبت سفارش/}).check();
    const created=mutation(customer,"/api/orders");await customer.getByRole("button",{name:"ارسال درخواست چاپ",exact:true}).click();
    const createdResponse=await created;assert.equal(createdResponse.status(),201);const id=(await createdResponse.json()).id;
    await admin.goto(base+"/admin/orders/"+id);await admin.getByLabel("مبلغ پیشنهادی (تومان)",{exact:true}).waitFor();
    const source=await api(admin,adminOrder(id));assert(source.order.status==="review" && source.files.length===1);
    assert((await (await admin.request.get(base+"/api/admin/files/"+source.files[0].id)).body()).equals(original));
    check(true,"real estimate leads to an SQL-backed order and an administrator can inspect the original private PDF");
    await intake();await intake(id);
    await admin.goto(base+"/admin/orders/"+id);await admin.getByText("درخواست اصلاح فایل",{exact:true}).click();
    const requestForm=admin.locator(".request-information-form:visible");
    await requestForm.getByLabel("شرح اصلاحات مورد نیاز",{exact:true}).fill("لطفاً صفحهٔ اصلاح‌شده را ارسال کنید.");
    await requestForm.getByLabel("دلیل اصلاح فایل",{exact:true}).fill("بازبینی نسخهٔ اولیه");
    const correction=mutation(admin,"/corrections");await requestForm.getByRole("button",{name:"ثبت درخواست اصلاح",exact:true}).click();assert.equal((await correction).status(),200);
    const customerNotices=await api(customer,"customer/notifications?page=1&pageSize=20");
    assert(customerNotices.items.some(row=>Object.values(row).some(v=>typeof v==="string" && v.includes(id))));
    await customer.goto(base+"/account/orders/"+id);const reply=customer.locator(".customer-correction-form:visible");
    await reply.getByLabel("توضیح اصلاحات انجام‌شده",{exact:true}).fill("جایگزینی PDF پس از اصلاح حاشیه‌ها");
    assert(await reply.getByRole("button",{name:"ارسال برای بازبینی چاپخانه",exact:true}).isDisabled());
    const fixed=pdfFixture("new corrected version");await reply.getByLabel("فایل PDF اصلاح‌شده").setInputFiles(upload("release-corrected.pdf",fixed,"application/pdf"));
    await reply.getByText(/PDF این اصلاحیه بارگذاری شد/).waitFor();
    await reply.getByRole("button",{name:"ارسال برای بازبینی چاپخانه",exact:true}).click();await reply.waitFor({state:"hidden"});
    await admin.reload();await admin.locator(".correction-review-form:visible").waitFor();
    const panel=admin.locator(".admin-quote-panel:visible");
    await panel.getByLabel("مبلغ پیشنهادی (تومان)",{exact:true}).fill("280000");await panel.getByLabel("خدمات شامل قیمت").fill("چاپ و صحافی مطابق فایل بررسی‌شده");
    const quote=mutation(admin,"/quotes");await panel.getByRole("button",{name:"ارسال پیشنهاد قیمت",exact:true}).click();assert.equal((await quote).status(),200);
    const revised=await api(admin,adminOrder(id));assert.equal(revised.corrections.at(-1).state,"approved");
    assert((await (await admin.request.get(base+"/api/admin/files/"+revised.order.approved_file_id)).body()).equals(fixed));
    check(true,"customer receives correction notice, missing PDF is blocked, admin approves the new bytes when issuing the quote");
    await customer.reload();customer.once("dialog",d=>d.accept());
    const accepted=mutation(customer,"/quote/accept");await customer.getByRole("button",{name:"تأیید قیمت",exact:true}).click();
    assert.equal((await accepted).status(),200);
    await matches(async()=> (await api(customer,customerOrder(id))).order.status==="confirmed","owner accepted quote");
    await acceptFinalConditions(admin,customer,base,id);
    for(const name of ["شروع چاپ","پایان چاپ و ارسال به صحافی","پایان صحافی و آماده تحویل","ثبت تحویل سفارش"]){
      const action=admin.getByRole("button",{name,exact:true});await action.waitFor();admin.once("dialog",d=>d.accept());await action.click();
    }
    await matches(async()=> (await api(customer,customerOrder(id))).order.status==="completed","customer sees delivery");
    const done=await api(admin,adminOrder(id));
    assert(done.events.some(e=>e.status==="printing")&&done.events.some(e=>e.status==="binding")&&done.events.some(e=>e.status==="ready"));
    assert(done.finalConditions.acceptedAt);await admin.screenshot({path:artifacts+"/delivered-order.png",animations:"disabled"});
    check(true,"signed final terms, stale revision conflict, re-acceptance, printing, binding, readiness and handover persist with history");
    assert.deepEqual(errors,[]);check(true,"independent SQL browser sessions finish without uncaught JavaScript errors");
  } finally { await Promise.all(contexts.map(ctx=>ctx.close())); }
}
