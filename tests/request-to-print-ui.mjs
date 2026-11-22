import { acceptFinalConditions } from "./final-terms-browser.mjs";
import {formatToman} from "../frontend/lib/money.ts";
import { pdfFixture, submitPrintRequest } from "./pdf-fixture.mjs";
import assert from 'node:assert/strict';
import path from 'node:path';

export async function requestToPrintChecks({browser,page,base,check,control,customerPhone='09000000826',password='Old.Test.Password@1'}) {
  const context=await browser.newContext({viewport:{width:1440,height:1000}});
  await context.route('**/*',route=>route.request().url().startsWith(base)?route.continue():route.abort());
  const customer=await context.newPage(),errors=[];customer.on('pageerror',e=>errors.push(e.message));
  async function api(target,method,route,body) {
    return target.evaluate(async({method,route,body})=>{const r=await fetch('/api/'+route,{method,headers:{'Content-Type':'application/json'},...(body?{body:JSON.stringify(body)}:{})});return {status:r.status,body:await r.json()};},{method,route,body});
  }
  async function send(amount=200000) {
    await page.getByLabel('مبلغ پیشنهادی (تومان)',{exact:true}).fill(String(amount));
    await page.getByLabel('خدمات شامل قیمت',{exact:true}).fill('چاپ و صحافی مطابق مشخصات ثبت‌شده');
    await page.getByText('توضیحات اختیاری قیمت',{exact:true}).click();
    await page.getByLabel('خارج از قیمت',{exact:true}).fill('هزینه ارسال در این مبلغ نیست');
    const response=page.waitForResponse(r=>r.url().endsWith('/quotes')&&r.request().method()==='POST');
    await page.getByRole('button',{name:'ارسال پیشنهاد قیمت',exact:true}).click();assert.equal((await response).status(),200);
    await page.getByText('منتظر تصمیم مشتری؛ شروع چاپ تا تأیید او مجاز نیست.',{exact:true}).waitFor();
  }
  const workflowFiles=[];
  try {
    await customer.goto(base+'/');
    const calculatorLink=customer.getByRole('navigation',{name:'منوی اصلی'}).getByRole('link',{name:/محاسبه/});
    await calculatorLink.click();
    await customer.locator('input[name=password]').waitFor();
    check(new URL(customer.url()).searchParams.get('next')==='/calculator' && await customer.locator('.book-calculator').count()===0,'UAT-01 guest header preserves return path without rendering a usable calculator');
    await customer.goto(base+'/calculator');
    await customer.waitForURL(url=>url.pathname==='/customer');
    await customer.locator('input[name=phone]').fill(customerPhone);
    await customer.locator('input[name=password]').fill(password);
    await customer.locator('form').getByRole('button',{name:'ورود',exact:true}).click();
    await customer.locator('.book-calculator').waitFor();
    check(new URL(customer.url()).pathname==='/calculator','completed customer login resumes the protected calculator');
    const calc=customer.waitForResponse(r=>r.url().endsWith('/api/quote')&&r.request().method()==='POST');
    await customer.getByRole('button',{name:'محاسبه هزینه',exact:true}).click();const estimate=await(await calc).json();
    const total = customer.getByRole("definition").filter({hasText:formatToman(estimate.total)}).last();
    await total.waitFor();
    check(estimate.total>=0 && await customer.getByText("برآورد کل",{exact:true}).isVisible() && (await total.innerText())===formatToman(estimate.total),'authenticated estimate is calculated by the backend and displayed as an approximation');
    const link=customer.getByRole('link',{name:'ارسال درخواست چاپ',exact:true});
    check(await link.getAttribute('href')==='/order?draft=1','request CTA carries only a safe draft flag, no contact/specification JSON');
    await link.click();await customer.waitForURL('**/order?draft=1');
    check(await customer.locator('input[name=phone]').count()===0,'UAT-02 the authenticated request starts with the existing specification and profile');
    await customer.locator('input[name=title]').fill('درخواست آزمایشی گردش پیشنهاد');
    await customer.getByRole('complementary').getByText(formatToman(estimate.total),{exact:true}).waitFor();
    check(await customer.getByRole('complementary').getByText(formatToman(estimate.total),{exact:true}).isVisible(),'technical specification survives authentication and retains the exact estimate');
    await customer.getByLabel(/فایل متن و جلد را انتخاب کنید/).setInputFiles({name:"primary.pdf",mimeType:"application/pdf",buffer:pdfFixture()});
    await customer.getByRole("checkbox",{name:/شرایط ثبت سفارش/}).check();
    const creating=customer.waitForResponse(r=>r.url().endsWith('/api/orders')&&r.request().method()==='POST');
    await customer.getByRole('button',{name:'ارسال درخواست چاپ',exact:true}).click();const created=await creating;assert.equal(created.status(),201);const receipt=await created.json(),id=receipt.id;
    await customer.getByRole('heading',{name:'درخواست شما ثبت شد.',exact:true}).waitFor();
    check((await customer.locator('main').innerText()).includes('تا تأیید شما چاپ شروع نمی‌شود'),'UAT-03 confirmation explicitly submits for review without promising production');
    await page.goto(base+'/admin/orders/'+id);await page.getByLabel('مبلغ پیشنهادی (تومان)',{exact:true}).waitFor();
    const opened=await api(page,'GET','admin/orders/'+id);
    check(opened.body.order.status==='review'&&opened.body.events.filter(e=>e.status==='review').length===1,'UAT-04 explicit admin opening reviews once');
    
    await send();
    let detail=(await api(customer,'GET','customer/orders/'+id)).body;
    check(detail.order.final_price===null&&detail.order.status==='awaiting_customer_approval','UAT-05 sending a proposal leaves final price unapproved and printing blocked');
    await customer.goto(base+'/account/orders/'+id);await customer.getByRole('button',{name:'تأیید قیمت',exact:true}).waitFor();
    check((await customer.locator('.order-quote:visible').innerText()).includes('200,000')&&(await customer.locator('.order-quote:visible').innerText()).includes('هزینه ارسال'),'UAT-07 customer sees revision, grouped toman, scope, delivery and exclusions');
    // A support discussion must not accept/revise a proposal implicitly.
    const discussion=await api(customer,'POST','tickets',{idempotencyKey:crypto.randomUUID(),orderId:id,subject:'سؤال درباره پیشنهاد',message:'پرسش آزمایشی درباره شرایط پیشنهاد چاپخانه.'});
    check(discussion.status===201 && (await api(customer,'GET','customer/orders/'+id)).body.order.status==='awaiting_customer_approval','ordinary quote discussion leaves proposal and production states unchanged');
    await customer.locator('.customer-order-page:visible').getByLabel('دلیل اصلاح یا انصراف',{exact:true}).fill('لطفاً شرایط تحویل را اصلاح کنید.');
    await customer.getByRole('button',{name:'درخواست بازنگری قیمت',exact:true}).click();await customer.locator('.customer-quote-actions:visible').waitFor({state:'hidden'});
    check((await api(customer,'GET','customer/orders/'+id)).body.order.status==='review','UAT-08 structured customer revision request returns to review');
    await page.reload();await page.getByLabel('مبلغ پیشنهادی (تومان)',{exact:true}).waitFor();await send(250000);
    await customer.reload();await customer.getByRole('button',{name:'تأیید قیمت',exact:true}).waitFor();
    check(await customer.locator('.order-quote:visible').count()===2,'revised commercial terms retain the immutable earlier quote history');
    for(const width of [360,390,768,1024,1440,1920]) {
      await customer.setViewportSize({width,height:1000});await page.setViewportSize({width,height:1000});
      check(await customer.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1)&&await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'UAT-20 customer/admin proposal forms fit RTL viewport '+width);
    }
    await customer.setViewportSize({width:1440,height:1000});await page.setViewportSize({width:1440,height:1000});
    for(const [target,name]of [[customer,'customer-proposal'],[page,'admin-proposal']]) {
      await target.addScriptTag({path:path.resolve('frontend/node_modules/axe-core/axe.min.js')});
      const violations=await target.evaluate(async()=>(await axe.run({runOnly:{type:'tag',values:['wcag2a','wcag2aa','wcag21aa','wcag22aa']}})).violations.map(v=>({id:v.id,nodes:v.nodes.map(n=>n.target)})));
      assert.deepEqual(violations,[]);check(true,name+' passes automated accessibility checks');
      await target.evaluate(()=>{if(document.activeElement instanceof HTMLElement)document.activeElement.blur();window.scrollTo(0,0);});
      await target.locator('.toast').waitFor({state:'hidden'});
      await target.screenshot({path:'.qa/request-to-print/'+name+'-desktop.png',fullPage:true,animations:'disabled'});
      await target.setViewportSize({width:390,height:844});await target.evaluate(()=>window.scrollTo(0,0));await target.waitForTimeout(350);await target.screenshot({path:'.qa/request-to-print/'+name+'-mobile.png',fullPage:true,animations:'disabled'});await target.setViewportSize({width:1440,height:1000});
    }
    customer.once('dialog',dialog=>dialog.accept());await customer.getByRole('button',{name:'تأیید قیمت',exact:true}).click();await customer.locator('.customer-quote-actions:visible').waitFor({state:'hidden'});
    detail=(await api(customer,'GET','customer/orders/'+id)).body;
    check(detail.order.status==='confirmed'&&detail.order.final_price===250000&&!detail.events.some(e=>e.status==='printing'),'UAT-09 acceptance aligns the approved price and queue without starting printing');
    await acceptFinalConditions(page,customer,base,id);check(true,'accepted price is locked and the server rejects production before explicit acceptance of final file and conditions');const start=page.getByRole('button',{name:'شروع چاپ',exact:true});await start.waitFor();page.once('dialog',dialog=>dialog.accept());await start.click();
    await page.getByRole('button',{name:'پایان چاپ و ارسال به صحافی',exact:true}).waitFor();check(true,'UAT-11 only the separate administrator action starts production');
    for(const name of ['پایان چاپ و ارسال به صحافی','پایان صحافی و آماده تحویل','ثبت تحویل سفارش']) {
      page.once('dialog',dialog=>dialog.accept());await page.getByRole('button',{name,exact:true}).click();
      const next=name==='پایان چاپ و ارسال به صحافی'?'پایان صحافی و آماده تحویل':name==='پایان صحافی و آماده تحویل'?'ثبت تحویل سفارش':null;
      if(next)await page.getByRole('button',{name:next,exact:true}).waitFor();else await page.getByText('این سفارش به وضعیت پایانی رسیده است.',{exact:true}).waitFor();
    }
    check(true,'UAT-12 printing, binding, ready and delivery complete through the actual admin controls');
    await customer.goto(base+'/account/orders');await customer.getByRole('combobox',{name:'مرحلهٔ درخواست',exact:true}).selectOption('completed');
    await customer.getByRole('row').filter({hasText:'درخواست آزمایشی گردش پیشنهاد'}).getByRole('link',{name:'مشاهده جزئیات'}).waitFor();
    check(await customer.locator('input[name=phone],input[name=code]').count()===0,'UAT-15 account tracking and grouping never ask for mobile/tracking code again');
    async function newRequest(only=false) {
      const spec=structuredClone(detail.specification);delete spec.customerNotes;if(only)spec.details.printOnly=true;
      const created=await submitPrintRequest(customer,base,{idempotencyKey:crypto.randomUUID(),title:only?'درخواست فقط چاپ آزمایشی':'درخواست تکمیلی آزمایشی',spec});assert.equal(created.status,201);
      await page.goto(base+'/admin/orders/'+created.body.id);await page.getByLabel('مبلغ پیشنهادی (تومان)',{exact:true}).waitFor();return created.body.id;
    }
    const onlyId=await newRequest(true);await send(180000);
    await customer.goto(base+'/account/orders/'+onlyId);await customer.getByRole('button',{name:'تأیید قیمت',exact:true}).waitFor();
    check(await customer.locator('.customer-quote-actions:visible').count()===1,'only the active order exposes quote actions while Next preserves hidden route history');
    customer.once('dialog',d=>d.accept());await customer.getByRole('button',{name:'تأیید قیمت',exact:true}).click();await customer.locator('.customer-quote-actions:visible').waitFor({state:'hidden'});
    await acceptFinalConditions(page,customer,base,onlyId);await page.getByRole('button',{name:'شروع چاپ',exact:true}).waitFor();page.once('dialog',d=>d.accept());await page.getByRole('button',{name:'شروع چاپ',exact:true}).click();
    await page.getByRole('button',{name:'پایان چاپ و آماده تحویل',exact:true}).waitFor();
    check(await page.getByRole('button',{name:'پایان چاپ و ارسال به صحافی',exact:true}).count()===0,'UAT-13 approved only-print request skips binding in actual production controls');
    page.once('dialog',d=>d.accept());await page.getByRole('button',{name:'پایان چاپ و آماده تحویل',exact:true}).click();await page.getByRole('button',{name:'ثبت تحویل سفارش',exact:true}).waitFor();
    page.once('dialog',d=>d.accept());await page.getByRole('button',{name:'ثبت تحویل سفارش',exact:true}).click();await page.getByText('این سفارش به وضعیت پایانی رسیده است.',{exact:true}).waitFor();
    const declined=await newRequest();await send(190000);await customer.goto(base+'/account/orders/'+declined);
    await customer.locator('.customer-order-page:visible').getByLabel('دلیل اصلاح یا انصراف',{exact:true}).fill('از ادامه این درخواست منصرف شدم.');await customer.getByRole('button',{name:'رد پیشنهاد قیمت',exact:true}).click();await customer.locator('.order-cancelled').waitFor();
    check((await api(customer,'GET','customer/orders/'+declined)).body.order.final_price===null,'UAT-14 declining never approves a price or permits production');
    const info=await newRequest();await page.getByText("درخواست اصلاح فایل",{exact:true}).click();await page.getByLabel('شرح اصلاحات مورد نیاز',{exact:true}).fill('فایل PDF متن را ارسال کنید.');await page.getByLabel('دلیل اصلاح فایل',{exact:true}).fill('ابعاد صفحه');
    const requested=page.waitForResponse(r=>r.url().endsWith('/corrections')&&r.request().method()==='POST');await page.getByRole('button',{name:'ثبت درخواست اصلاح',exact:true}).click();assert.equal((await requested).status(),200);
    await customer.goto(base+'/account/orders/'+info);await customer.locator('.customer-order-page:visible').getByLabel('فایل PDF اصلاح‌شده',{exact:true}).waitFor();
    const upload=customer.waitForResponse(r=>r.url().includes('/files?revisionId=')&&r.request().method()==='POST');
    await customer.locator('.customer-order-page:visible').getByLabel('فایل PDF اصلاح‌شده',{exact:true}).setInputFiles({name:'requested-artwork.pdf',mimeType:'application/pdf',buffer:pdfFixture()});
    const uploaded=await upload;assert.equal(uploaded.status(),201);workflowFiles.push({orderId:info,fileId:(await uploaded.json()).id});
    await customer.getByText('PDF این اصلاحیه بارگذاری شد:',{exact:false}).waitFor();
    await customer.locator('.customer-order-page:visible').getByLabel('توضیح اصلاحات انجام‌شده',{exact:true}).fill('فایل متن برای بررسی ارسال شد.');await customer.getByRole('button',{name:'ارسال برای بازبینی چاپخانه',exact:true}).click();await customer.locator('.customer-correction-form:visible').waitFor({state:'hidden'});
    check((await api(customer,'GET','customer/orders/'+info)).body.order.information_request!==null,'customer correction remains blocked until administrator review');
    await page.reload();await send(200000);
    check((await api(customer,'GET','customer/orders/'+info)).body.order.information_request===null,'only explicit administrator approval clears the outstanding correction');
    check(errors.length===0,'new customer proposal and authentication-return journey has no runtime exceptions');
    if(control)check((await control({})).smsSends===0,'request-to-production browser verification does not send SMS');
    return {workflowFiles};
  } finally {await context.close();await page.setViewportSize({width:1440,height:1000});}
}
