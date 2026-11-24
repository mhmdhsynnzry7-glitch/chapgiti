import assert from "node:assert/strict";
import { acceptFinalConditions } from "./final-terms-browser.mjs";
import { pdfFixture, submitPrintRequest } from "./pdf-fixture.mjs";

// Only the existing memory API/loopback harness invokes this journey.
export async function orderRevisionChecks({page:admin,base,check,control}) {
  const context=admin.context();await context.addInitScript(()=>Object.defineProperty(document,"visibilityState",{configurable:true,get:()=>"visible"}));
  const customer=await context.newPage(),errors=[];
  for(const target of [admin,customer])target.on("pageerror",e=>errors.push(e.message));
  async function api(target,method,path,body,expected=200){
    const result=await target.evaluate(async({method,path,body})=>{const r=await fetch("/api/"+path,{method,headers:{"Content-Type":"application/json"},...(body?{body:JSON.stringify(body)}:{})});return{status:r.status,body:await r.json()};},{method,path,body});
    assert.equal(result.status,expected,path+" "+JSON.stringify(result.body));return result.body;
  }
  async function until(predicate,label,timeout=10000){const end=Date.now()+timeout;while(Date.now()<end){if(await predicate())return;await new Promise(r=>setTimeout(r,80));}assert.fail(label);}
  const detail=()=>api(admin,"GET","admin/orders/"+id);
  const customerDetail=()=>api(customer,"GET","customer/orders/"+id);
  const counts=(target,scope)=>api(target,"GET",scope+"/notifications/unread-count");
  const liveOrder=target=>target.locator(target===admin?".order-editor:visible":".customer-order-details:visible");
  const spec={pages:120,quantity:1,size:"raghi",paper:"white",color:"bw",binding:"soft"};
  let id,original;
  async function quote(amount){
    const panel=admin.locator(".admin-quote-panel:visible");
    await panel.getByLabel("مبلغ پیشنهادی (تومان)",{exact:true}).fill(String(amount));await panel.getByLabel("خدمات شامل قیمت",{exact:true}).fill("چاپ بر اساس مشخصات و فایل تأییدشده");
    const sent=admin.waitForResponse(r=>r.url().endsWith("/quotes")&&r.request().method()==="POST");await panel.getByRole("button",{name:"ارسال پیشنهاد قیمت",exact:true}).click();assert.equal((await sent).status(),200);
    await until(async()=>(await detail()).order.status==="awaiting_customer_approval","quote persisted");
  }
  async function accept(){await customer.reload();const button=liveOrder(customer).getByRole("button",{name:"تأیید قیمت",exact:true});await button.waitFor();customer.once("dialog",d=>d.accept());await button.click();await liveOrder(customer).locator(".customer-quote-actions").waitFor({state:"hidden"});}
  async function requestCorrection(required){
    const toggle=admin.locator("summary").filter({hasText:"درخواست اصلاح فایل"});
    if(!await toggle.evaluate(e=>e.parentElement.open))await toggle.click();
    const form=admin.locator(".request-information-form:visible");await form.getByLabel("شرح اصلاحات مورد نیاز",{exact:true}).fill(required?"لطفاً اندازهٔ صفحه و فایل PDF را اصلاح کنید.":"لطفاً عنوان نهایی روی جلد را تأیید کنید.");
    await form.getByLabel("دلیل اصلاح فایل",{exact:true}).fill("بررسی صفحه‌آرایی");
    const sent=admin.waitForResponse(r=>r.url().endsWith("/corrections")&&r.request().method()==="POST");await form.getByRole("button",{name:"ثبت درخواست اصلاح",exact:true}).click();assert.equal((await sent).status(),200);
    await until(async()=>(await detail()).corrections.at(-1).state==="requested","correction request persisted");
  }
  try {
    const settings=await api(admin,"GET","admin/settings");
    await customer.goto(base+"/order");await customer.locator("input[name=title]").fill("آزمون اصلاحیه و فایل خصوصی");
    const initialInput=customer.getByLabel(/فایل متن و جلد را انتخاب کنید/);
    check(await initialInput.getAttribute("required")!==null&&await customer.getByText("فایل PDF کتاب (الزامی)",{exact:false}).isVisible(),"required service shows a mandatory original PDF before submission");
    let creations=0;const countCreate=r=>{if(r.url().endsWith("/api/orders")&&r.method()==="POST")creations++;};customer.on("request",countCreate);
    await customer.getByRole("checkbox",{name:/شرایط ثبت سفارش/}).check();
    const submit=customer.getByRole("button",{name:"ارسال درخواست چاپ",exact:true});
    check(creations===0 && await submit.isDisabled() && await initialInput.evaluate(e=>e.validity.valueMissing),"missing PDF prevents submission");
    await initialInput.setInputFiles({name:"invalid.pdf",mimeType:"application/pdf",buffer:Buffer.from("%PDF-forged")});
    await customer.getByRole("alert").filter({hasText:"محتوای فایل PDF معتبر نیست."}).waitFor();
    check(creations===0 && await submit.isDisabled(),"invalid file cannot create an order");
    await initialInput.setInputFiles({name:"original.pdf",mimeType:"application/pdf",buffer:pdfFixture("original")});
    const created=customer.waitForResponse(r=>r.url().endsWith("/api/orders")&&r.request().method()==="POST");await customer.getByRole("button",{name:"ارسال درخواست چاپ",exact:false}).click();const response=await created;assert.equal(response.status(),201);id=(await response.json()).id;customer.off("request",countCreate);
    await customer.locator(".tracking-code").waitFor();
    check((await detail()).files.length===1,"valid initial PDF and order persist together through the actual multipart form");
    original=(await detail()).files[0].id;
    await admin.goto(base+"/admin/orders");await until(async()=>(await counts(admin,"admin")).unseenOrderCount===1,"administrator order badge receives new request");
    await until(()=>admin.locator('.admin-nav [data-badge="orders"]').count().then(n=>n===1),"admin badge renders authorized summary");
    await api(admin,"POST","admin/notifications/read-all",{});
    check((await counts(admin,"admin")).unseenOrderCount===1,"reading notification does not mark the order seen");
    await admin.goto(base+"/admin/orders/"+id);await admin.getByLabel("مبلغ پیشنهادی (تومان)",{exact:true}).waitFor();
    await until(async()=>(await counts(admin,"admin")).unseenOrderCount===0,"actual visible administrator detail marks the order viewed");check(true,"viewed order count decreases only after its detail is visible");
    await quote(200000);
    await customer.goto(base+"/account");await customer.locator(".notification-bell").click();await customer.locator(".notification-popover").waitFor();
    await until(()=>customer.locator('.nav [data-badge="orders"]').count().then(n=>n===1),"customer navigation receives actionable order badge");
    check(await customer.getByRole("heading",{name:"اعلان‌ها",exact:true}).isVisible()&&await customer.locator('.notification-popover').getByRole("button",{name:"تازه‌سازی",exact:true}).count()===0,"notification center has one simple heading and no ordinary refresh control");
    await customer.locator(".notification-popover").getByRole("button",{name:"خواندن همه",exact:true}).click();
    await until(async()=>(await counts(customer,"customer")).unreadCount===0,"read-all persisted");
    check((await counts(customer,"customer")).unseenOrderCount===1,"customer order attention remains independent from notification read-all");
    await customer.goto(base+"/account/orders/"+id);await liveOrder(customer).waitFor();
    await until(async()=>(await counts(customer,"customer")).unseenOrderCount===0,"customer visible detail acknowledges observed sequence");
    await accept();await admin.reload();await admin.getByText("درخواست اصلاح فایل",{exact:true}).waitFor();
    await requestCorrection(true);await customer.reload();
    let form=customer.locator(".customer-correction-form:visible");await form.waitFor();
    check(await form.getByRole("button",{name:"ارسال برای بازبینی چاپخانه",exact:true}).isDisabled(),"mandatory correction clearly blocks submission until a new PDF is uploaded");
    let state=await customerDetail(),latest=state.corrections.at(-1);
    await form.getByText(latest.requestText,{exact:true}).waitFor();
    await api(admin,"POST","admin/orders/"+id+"/start-printing",{idempotencyKey:crypto.randomUUID(),expectedVersion:state.order.version},409);
    check(!(await detail()).productionReadiness.canStart,"server blocks production while a correction is open");
    const old=await api(customer,"POST","customer/orders/"+id+"/corrections/submit",{idempotencyKey:crypto.randomUUID(),expectedVersion:state.order.version,revisionId:latest.id,revisionVersion:latest.version,message:"Trying an old attachment",fileId:original},409);
    check(old.code==="CORRECTION_NEW_PDF_REQUIRED","direct old-attachment reuse cannot satisfy a new-PDF request");
    const bad=customer.waitForResponse(r=>r.url().includes("/files?revisionId=")&&r.request().method()==="POST");await form.getByLabel("فایل PDF اصلاح‌شده",{exact:true}).setInputFiles({name:"bad.pdf",mimeType:"application/pdf",buffer:Buffer.from("%PDF-fake")});assert.equal((await bad).status(),400);await liveOrder(customer).getByRole("alert").waitFor();
    check(await form.getByRole("button",{name:"ارسال برای بازبینی چاپخانه",exact:true}).isDisabled(),"invalid corrected PDF keeps the mandatory upload unsatisfied");
    const uploaded=customer.waitForResponse(r=>r.url().includes("/files?revisionId=")&&r.request().method()==="POST");await form.getByLabel("فایل PDF اصلاح‌شده",{exact:true}).setInputFiles({name:"corrected.pdf",mimeType:"application/pdf",buffer:pdfFixture("corrected")});assert.equal((await uploaded).status(),201);
    await form.getByText("PDF این اصلاحیه بارگذاری شد:",{exact:false}).waitFor();
    await form.getByLabel("توضیح اصلاحات انجام‌شده",{exact:true}).fill("اندازهٔ صفحه اصلاح شد و فایل جدید بارگذاری شد.");
    await form.getByRole("button",{name:"ارسال برای بازبینی چاپخانه",exact:true}).click();await form.waitFor({state:"hidden"});
    check((await customerDetail()).corrections.at(-1).state==="submitted","customer submission remains pending administrator review");
    await admin.reload();let review=admin.locator(".correction-review-form:visible");await review.waitFor();
    check(await review.getByRole("button",{name:"رد اصلاحیه",exact:true}).isDisabled(),"administrator rejection requires a meaningful reason");
    await review.getByLabel("توضیح بازبینی / دلیل رد",{exact:true}).fill("عنوان جلد نیاز به تأیید نهایی دارد.");await review.getByRole("button",{name:"رد اصلاحیه",exact:true}).click();await review.waitFor({state:"hidden"});
    check((await detail()).productionReadiness.canStart===false,"rejected correction keeps production blocked on the server");
    await requestCorrection(true);await customer.reload();form=customer.locator(".customer-correction-form:visible");await form.waitFor();
    check(await form.getByRole("button",{name:"ارسال برای بازبینی چاپخانه",exact:true}).isDisabled(),"next round requires its own replacement");
    const nextFile=customer.waitForResponse(r=>r.url().includes("/files?revisionId=")&&r.request().method()==="POST");
    await form.getByLabel("فایل PDF اصلاح‌شده").setInputFiles({name:"approved.pdf",mimeType:"application/pdf",buffer:pdfFixture("approved revision")});
    assert.equal((await nextFile).status(),201); await form.getByText(/PDF این اصلاحیه بارگذاری شد/).waitFor();
    await form.getByLabel("توضیح اصلاحات انجام‌شده",{exact:true}).fill("عنوان نهایی روی جلد تأیید می‌شود.");await form.getByRole("button",{name:"ارسال برای بازبینی چاپخانه",exact:true}).click();await form.waitFor({state:"hidden"});
    await admin.reload();
    check(!(await detail()).productionReadiness.canStart,"replacement pending review blocks production");
    await acceptFinalConditions(admin,customer,base,id);
    state=await detail();
    check(state.files.length===3 && state.corrections.length===2 &&
      state.files.find(f=>f.id===state.order.approved_file_id)?.name==="approved.pdf","issuing final terms reviews the exact replacement and preserves history");
    const start=admin.getByRole("button",{name:"شروع چاپ",exact:true});await start.waitFor();admin.once("dialog",d=>d.accept());await start.click();await admin.getByRole("button",{name:"پایان چاپ و ارسال به صحافی",exact:true}).waitFor();
    state=await detail();check(state.order.status==="printing"&&state.order.approved_file_id!==original&&state.quotes.filter(q=>q.state==="accepted").length===1,"production uses the corrected approved file while previous quote acceptance remains immutable");
    await customer.reload();await liveOrder(customer).locator(".correction-history li").last().waitFor();
    check(await liveOrder(customer).locator(".correction-history li").count()===2,"customer sees both correction cycles after refresh");
    const history=customer.getByRole("region",{name:"تاریخچهٔ اصلاحیه‌ها"});
    const previous=history.locator("details").filter({has:customer.getByText("اصلاحیهٔ ۱",{exact:true})});
    check(await previous.getAttribute("open")===null,"earlier round is collapsed in compact history");
    await previous.locator("summary").click();await previous.getByText("عنوان جلد نیاز به تأیید نهایی دارد.",{exact:false}).waitFor();
    check((await liveOrder(customer).innerText()).includes("نسخهٔ ۲"),"customer sees retained PDF version history after refresh");
    const pdfLink=liveOrder(customer).locator('section[aria-label="فایل‌های سفارش"] a').filter({hasText:'corrected.pdf'});
    const previewLink=liveOrder(customer).getByRole('link',{name:'نمایش corrected.pdf در برگهٔ جدید',exact:true});
    const downloadResponse=await context.request.get(base+await pdfLink.getAttribute('href'));
    const previewResponse=await context.request.get(base+await previewLink.getAttribute('href'));
    check(downloadResponse.status()===200&&previewResponse.status()===200&&downloadResponse.headers()['content-type'].startsWith('application/pdf')&&previewResponse.headers()['content-security-policy']==='sandbox',"customer PDF download and sandboxed preview links resolve through owner-authorized API endpoints");

    const second=await submitPrintRequest(customer,base,{idempotencyKey:crypto.randomUUID(),title:"Unseen menu test",spec});assert.equal(second.status,201);
    await admin.goto(base+"/admin/orders");await until(async()=>(await counts(admin,"admin")).unseenOrderCount>0,"new unseen menu test order");
    let views=0;const watch=r=>{if(r.method()==="POST"&&/\/orders\/[^/]+\/view$/.test(new URL(r.url()).pathname))views++;};admin.on("request",watch);
    const before=(await counts(admin,"admin")).unseenOrderCount;
    await admin.setViewportSize({width:1440,height:1000});await admin.getByRole("button",{name:"جمع‌کردن نوار کناری",exact:true}).click();
    check(await admin.locator('.admin-shell').evaluate(e=>e.classList.contains('sidebar-collapsed'))&&await admin.locator('.admin-nav [data-badge="orders"]').isVisible(),"collapsed sidebar retains its accessible order badge");
    check(await admin.locator('.admin-nav a[href="/admin/orders"]').getAttribute("title")==="سفارش‌ها","collapsed navigation has meaningful tooltips");
    await admin.setViewportSize({width:390,height:844});await admin.getByRole("button",{name:"منوی مدیریت",exact:true}).click();await admin.locator(".admin-sidebar.open").waitFor();
    check(await admin.locator('.admin-sidebar.open [data-badge="orders"]').isVisible()&&views===0&&(await counts(admin,"admin")).unseenOrderCount===before,"mobile sidebar opening shows counts without marking any order viewed");
    admin.off("request",watch);await admin.getByRole("button",{name:"بستن منو",exact:true}).click();
    const capped=route=>route.fulfill({status:200,json:{unreadCount:125,latestSequence:9999,unseenOrderCount:125,orderViewsAvailable:true}});
    await admin.route("**/api/admin/notifications/unread-count",capped);await admin.evaluate(()=>window.dispatchEvent(new Event("chap:notifications-refresh")));
    await admin.setViewportSize({width:1440,height:1000});await until(()=>admin.locator('.admin-nav [data-badge="orders"] bdi').textContent().then(t=>t==="99+"),"badge count caps at 99+");
    check(await admin.locator('.notification-bell .notification-badge').textContent()==="99+","large menu and notification badges display direction-safe 99+");
    await admin.unroute("**/api/admin/notifications/unread-count",capped);await admin.reload();
    await customer.goto(base+"/tickets");await customer.locator("#new-ticket").waitFor();
    const general=await api(customer,"POST","tickets",{idempotencyKey:crypto.randomUUID(),subject:"پشتیبانی عمومی آزمایشی",message:"این پیام برای بررسی کارکرد پشتیبانی است."},201);
    await api(admin,"POST","admin/tickets/"+general.id+"/messages",{idempotencyKey:crypto.randomUUID(),message:"پاسخ آزمایشی چاپخانه"},201);
    await customer.goto(base+"/tickets/"+general.id);
    await customer.getByText("پاسخ آزمایشی چاپخانه",{exact:true}).waitFor();
    check(await customer.locator(".ticket-bubble").count()===2,"general support shows the persisted conversation");
    const contactSite={...settings.site,phone:"02100000000",email:"contact-test@example.test",address:"نشانی آزمایشی برای بررسی چیدمان؛ ".repeat(7),hours:"روزهای کاری طبق هماهنگی آزمایشی",telegram:"https://t.me/chap_giti_test",bale:"https://ble.ir/chap_giti_test",instagram:"https://www.instagram.com/chap_giti_test/"};
    await api(admin,"PUT","admin/settings",{site:contactSite,rates:settings.rates,orderRequirements:settings.orderRequirements});
    await customer.goto(base+"/contact");await customer.getByRole("heading",{name:"ارتباط با چاپ گیتی",exact:true}).waitFor();
    check(await customer.locator('.contact-card').count()===4&&await customer.locator('.contact-social-links a').count()===3,"contact renders only configured information and supported channels");
    check(await customer.locator('main a[href="tel:02100000000"]').count()===1&&await customer.locator('main a[href="mailto:contact-test@example.test"]').count()===1,"contact phone/email use actual CMS tel/mailto values");
    check(await customer.locator('main a[target="_blank"]').evaluateAll(links=>links.every(a=>a.rel.includes('noopener')&&a.rel.includes('noreferrer'))),"external contact links use safe new-tab attributes");
    for(const width of [360,390,768,1024,1440,1920]){await customer.setViewportSize({width,height:900});check(await customer.evaluate(()=>document.documentElement.dir==='rtl'&&document.documentElement.scrollWidth<=innerWidth+1),'contact cards wrap in RTL without overflow at '+width+'px');}
    await customer.locator('.contact-value').first().focus();check(await customer.locator('.contact-value').first().evaluate(e=>getComputedStyle(e).outlineStyle!=='none'),"contact links expose a visible keyboard focus state");
    if(await customer.locator('main').count()){
      await customer.addScriptTag({path:'frontend/node_modules/axe-core/axe.min.js'});
      const violations=await customer.evaluate(async()=>(await window.axe.run('.contact-page',{runOnly:{type:'tag',values:['wcag2a','wcag2aa','wcag21aa','wcag22aa']}})).violations.map(v=>({id:v.id,nodes:v.nodes.map(n=>n.target)})));
      assert.deepEqual(violations,[]);check(true,"contact page passes automated accessibility checks");
    }
    await customer.setViewportSize({width:1440,height:1000});await customer.screenshot({path:'.qa/order-workflow-2026-11-16/contact-desktop.png',fullPage:true});await customer.setViewportSize({width:390,height:844});await customer.screenshot({path:'.qa/order-workflow-2026-11-16/contact-mobile.png',fullPage:true});
    check(await admin.locator('.admin-nav a[href="/admin/tickets"]').count()===1,"support remains available from administrator navigation");
    await api(admin,"PUT","admin/settings",{site:{...settings.site,phone:"",email:"",address:"",hours:"",instagram:"",telegram:"",bale:""},rates:settings.rates,orderRequirements:settings.orderRequirements});
    await customer.reload();check(await customer.locator('.contact-card,.contact-social-links a').count()===0&&await customer.getByText('اطلاعات ارتباطی چاپخانه هنوز ثبت نشده است.',{exact:true}).isVisible(),"missing CMS contact values produce an honest empty state without invented details");
    check((await control({})).smsSends===0&&errors.length===0,"new UI workflow uses no real SMS and has no uncaught browser errors");
  }finally{await customer.close();await admin.setViewportSize({width:1440,height:1000});}
}
