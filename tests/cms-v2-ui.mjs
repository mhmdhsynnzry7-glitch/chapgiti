import assert from "node:assert/strict";

export async function paginationChecks({page,base,check,control}) {
  await control({seedOrdersFor:'09000000826',orderCount:25,seedTicketsFor:'09000000826',ticketCount:25});
  const pager=()=>page.locator('.giti-pagination');
  async function waitPage(number) {
    await pager().and(page.locator('[aria-busy=false]')).locator('.pagination-summary').filter({hasText:new RegExp('صفحه '+number.toLocaleString('fa-IR')+' از')}).waitFor();
  }
  async function walk({url,api,selector,field,items='items',attribute}) {
    const expected=[];
    // Ask the existing API for all pages, using its actual page-size contract.
    for(let p=1;;p++) {
      const response=await page.request.get(base+'/api/'+api+'?page='+p);
      assert.equal(response.status(),200);const data=await response.json();expected.push(...data[items].map(row=>String(row[field])));
      if(expected.length>=data.total)break;
      assert.ok(p<20,'bounded isolated pagination fixture');
    }
    await page.goto(base+url);await waitPage(1);
    check(await pager().getByRole('button',{name:'صفحه قبل',exact:true}).isDisabled(),url+' disables Previous on its first page');
    const actual=[];let current=1;
    while(true) {
      const values=await page.locator(selector).evaluateAll((nodes,attribute)=>nodes.map(node=>attribute?node.getAttribute(attribute).split('/').at(-1):node.textContent.trim()),attribute);
      actual.push(...values);
      if(await pager().getByRole('button',{name:'صفحه بعد',exact:true}).isDisabled())break;
      await pager().getByRole('button',{name:'صفحه بعد',exact:true}).click();await waitPage(++current);assert.ok(current<20);
    }
    check(new Set(actual).size===actual.length&&JSON.stringify(actual)===JSON.stringify(expected),url+' traverses every owned/filtered row exactly once in API order');
    check(current>1&&await pager().getByRole('button',{name:'صفحه بعد',exact:true}).isDisabled(),url+' shows total pages and disables Next on the final page');
    await pager().getByRole('button',{name:'صفحه قبل',exact:true}).click();await waitPage(current-1);
    check(true,url+' Previous returns to the preceding page');
  }
  await walk({url:'/account/orders',api:'customer/orders',selector:'table a[href^="/account/orders/"]',field:'id',attribute:'href'});
  await walk({url:'/admin/orders',api:'admin/orders',selector:'.admin-content table tbody tr bdi',field:'code',items:'orders'});
  await pager().getByRole('button',{name:'صفحه بعد',exact:true}).click();await waitPage(2);
  await page.getByRole('combobox',{name:'فیلتر وضعیت',exact:true}).selectOption('new');await waitPage(1);
  check(await pager().getByRole('button',{name:'صفحه قبل',exact:true}).isDisabled(),'order status filtering resets to page one instead of leaving an empty later page');
  await walk({url:'/admin/customers',api:'admin/users',selector:'.admin-content table tbody tr td:nth-child(2)',field:'phone'});
  await pager().getByRole('button',{name:'صفحه بعد',exact:true}).click();await waitPage(2);
  await page.getByLabel('جست‌وجوی نام یا موبایل',{exact:true}).fill('09000000826');await page.getByRole('button',{name:'جست‌وجو',exact:true}).click();await waitPage(1);
  check(await page.locator('.admin-content table tbody tr').count()===1&&await pager().getByRole('button',{name:'صفحه بعد',exact:true}).isDisabled(),'customer search resets pagination and shows only the matching customer');
  await walk({url:'/tickets',api:'tickets',selector:'table a[href^="/tickets/"]',field:'id' ,attribute:'href'});
  await walk({url:'/admin/tickets',api:'admin/tickets',selector:'table a[href^="/admin/tickets/"]',field:'id' ,attribute:'href'});
  await pager().getByRole('button',{name:'صفحه بعد',exact:true}).click();await waitPage(2);
  await page.getByRole('combobox',{name:/^وضعیت گفتگو/}).selectOption('closed');await waitPage(1);
  check(await page.getByRole('table',{name:'درخواست‌های پشتیبانی'}).getByRole('cell',{name:'بسته‌شده',exact:true}).count()>0&&await pager().getByRole('button',{name:'صفحه بعد',exact:true}).isDisabled(),'ticket status filtering cooperates with total pages and resets to the first page');
  await page.goto(base+'/admin/activity');await waitPage(1);
  await pager().getByRole('button',{name:'صفحه بعد',exact:true}).click();await waitPage(2);
  await page.getByRole('combobox',{name:/^نوع فعالیت/}).selectOption('login');await waitPage(1);
  check(await pager().getByRole('button',{name:'صفحه قبل',exact:true}).isDisabled(),'activity filters reset the shared pagination without changing stored events');
  await page.goto(base+'/admin/orders');await waitPage(1);
  await pager().getByRole('button',{name:'صفحه بعد',exact:true}).click();await waitPage(2);
  const revalidated=page.waitForResponse(r=>r.url().includes('/api/admin/orders?')&&new URL(r.url()).searchParams.get('page')==='2');
  await page.evaluate(()=>window.dispatchEvent(new Event('focus')));await revalidated;await waitPage(2);
  check(true,'focus refresh preserves a still-valid current page');
  // Simulate a concurrent reduction in results without changing stored records.
  let reduced=false;
  const shrink=async route=>{
    if(!reduced&&new URL(route.request().url()).searchParams.get('page')==='2') {
      reduced=true;await route.fulfill({status:200,contentType:'application/json',body:JSON.stringify({orders:[],total:1,page:2})});
    } else await route.continue();
  };
  await page.route('**/api/admin/orders?*',shrink);
  await page.getByRole('button',{name:'تازه‌سازی سفارش‌ها',exact:true}).click();await waitPage(1);
  await page.unroute('**/api/admin/orders?*',shrink);
  check(reduced,'a shrinking result set recovers to a valid page without trapping the user');
  await pager().getByRole('button',{name:'صفحه بعد',exact:true}).focus();await page.keyboard.press('Enter');await waitPage(2);
  check(true,'shared pagination supports keyboard activation');
  for(const width of [360,390,768,1024,1440,1920]) {
    await page.setViewportSize({width,height:1000});
    const layout=await pager().evaluate(nav=>{const [prev,next]=nav.querySelectorAll('button');return {overflow:document.documentElement.scrollWidth-innerWidth,prevX:prev.getBoundingClientRect().x,nextX:next.getBoundingClientRect().x};});
    check(layout.overflow<=1&&layout.prevX>layout.nextX,'shared pagination fits '+width+'px with Previous on the RTL right');
  }
  await page.setViewportSize({width:1440,height:1000});
  await page.screenshot({path:'.qa/cms-v2/pagination-desktop.png',animations:'disabled',fullPage:true});
  await page.setViewportSize({width:390,height:844});await page.waitForTimeout(350);
  await page.screenshot({path:'.qa/cms-v2/pagination-mobile.png',animations:'disabled',fullPage:true});
  await page.setViewportSize({width:1440,height:1000});
  check((await control({})).smsSends===0,'pagination verification sends no SMS');
}

export async function servicesCmsChecks({ page, base, check, control }) {
  await page.goto(base + "/services");
  check(await page.locator(".cms-service-card").count() === 6, "V2 services use the preserved initial catalog from the backend");
  check(await page.locator('.cms-service-card a[href="/calculator"],.cms-service-card a[href="/order"]').count() === 0,
    "service cards have no calculator/order actions");
  await page.goto(base + "/admin/services");
  await page.getByRole("link", { name: "افزودن خدمت", exact: true }).click();
  await page.locator('input[name=title]').fill("خدمت آزمایشی پنل");
  await page.locator('input[name=slug]').fill("ui-service-review");
  await page.locator('input[name=subtitle]').fill("زیرعنوان آزمایشی در محیط ایزوله");
  await page.locator('textarea[name=description]').fill("توضیح کوتاه آزمایشی برای بررسی ذخیره‌سازی خدمات.");
  await page.locator('textarea[name=fullDescription]').fill("توضیح کامل آزمایشی؛ این داده فقط در حافظهٔ میزبان آزمون نگهداری می‌شود.");
  await page.locator('textarea[name=features]').fill("ویژگی اول آزمایشی\nویژگی دوم آزمایشی");
  await page.getByRole("button", { name: "افزودن بخش", exact: true }).click();
  const sectionEditor=page.getByRole("dialog");
  await sectionEditor.getByLabel("عنوان بخش", { exact: true }).fill("بخش آزمایشی");
  await sectionEditor.getByLabel("متن بخش", { exact: true }).fill("متن آزمایشی بخش محتوای خدمت برای کنترل فرم مدیریت.");
  await sectionEditor.getByRole("button",{name:"ذخیره بخش"}).click();
  await page.getByRole("row").filter({hasText:"بخش آزمایشی"}).waitFor();
  check(await page.locator('input[name=seoTitle]').count()===0,"service editor derives SEO from its main title");
  const creating=page.waitForResponse(r=>r.url().endsWith('/api/admin/services')&&r.request().method()==='POST');
  await page.getByRole("button", { name: "ذخیره خدمت", exact: true }).click();
  const created=await creating; assert.equal(created.status(),201);
  const {id,service}=await created.json();
  await page.waitForURL('**/admin/services/'+id);
  await page.locator('input[name=title]').waitFor();
  check(await page.locator('input[name=title]').inputValue()==="خدمت آزمایشی پنل", "actual services form persists a draft and reopens the new editor");
  const publicDraft=await page.request.get(base+'/api/public/services/ui-service-review');
  check(publicDraft.status()===404,"new inactive service is unavailable publicly");
  const {id:serviceId, aliases, ...payload}=service;
  assert.equal(serviceId,id);
  const legacy={seoTitle:"عنوان قدیمی آزمایشی",seoDescription:"خلاصهٔ قدیمی آزمایشی"};
  const seeded=await page.request.put(base+"/api/admin/services/"+id,{headers:{Origin:base},data:{...payload,...legacy}});
  assert.equal(seeded.status(),200);
  await page.reload();
  await page.locator('input[name=active]').check();
  await page.locator('input[name=slug]').fill('ui-service-renamed');
  const updating=page.waitForResponse(r=>r.url().endsWith('/api/admin/services/'+id)&&r.request().method()==='PUT');
  await page.getByRole("button", { name: "ذخیره خدمت", exact: true }).click();
  const updated=await updating;assert.equal(updated.status(),200);
  const edited=(await updated.json()).service;
  check(edited.seoTitle===legacy.seoTitle && edited.seoDescription===legacy.seoDescription,"editing visible fields preserves both legacy SEO values");
  await page.locator('input[name=slug]').waitFor();
  await page.locator('input[name=title]').fill('تغییر ذخیره‌نشده');
  page.once('dialog',d=>d.dismiss());await page.getByRole('link',{name:'انصراف',exact:true}).click();
  check(new URL(page.url()).pathname.endsWith(id)&&await page.locator('input[name=title]').inputValue()==='تغییر ذخیره‌نشده',"service editor preserves unsaved changes when navigation is cancelled");
  page.once('dialog',d=>d.accept());await page.getByRole('link',{name:'انصراف',exact:true}).click();await page.waitForURL('**/admin/services');
  await page.goto(base+'/services/ui-service-review');await page.waitForURL('**/services/ui-service-renamed');
  check((await page.title()).includes('خدمت آزمایشی پنل') && (await page.locator('meta[name=description]').getAttribute('content')).includes('توضیح کوتاه آزمایشی')&&(await page.locator('main').innerText()).includes('بخش آزمایشی'),"published service details/SEO use saved content and old slugs redirect canonically");
  check(await page.locator('main a[href="/calculator"],main a[href="/order"]').count()===0,"service detail has consultation as its only primary action");
  for(const width of [360,390,768,1024,1440,1920]){await page.setViewportSize({width,height:900});check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'published service fits RTL viewport '+width);}
  await page.setViewportSize({width:1440,height:1000});
  await page.getByRole('link',{name:'درخواست مشاوره',exact:true}).click();
  await page.waitForURL('**/contact');await page.getByRole('heading',{name:'ارتباط با چاپ گیتی',exact:true}).waitFor();
  check(await page.locator('main form,main textarea').count()===0,"consultation CTA opens configured contact details without creating a ticket");
  await page.goto(base+'/admin/services');
  const row=page.getByRole('row').filter({has:page.getByRole('cell',{name:'خدمت آزمایشی پنل',exact:true})});
  const toggling=page.waitForResponse(r=>r.url().endsWith('/api/admin/services/'+id)&&r.request().method()==='PUT');
  await row.getByRole('button',{name:'غیرفعال',exact:true}).click();assert.equal((await toggling).status(),200);
  check((await page.request.get(base+'/api/public/services/ui-service-renamed')).status()===404,"administrator deactivation immediately removes the service and its aliases from public APIs");
  if(control)check((await control({})).smsSends===0,"services and consultation browser checks do not send SMS");
}

export async function bannerContactChecks({page,base,check}) {
  await page.goto(base+'/contact');check(await page.locator('.contact-social-links a').count()===0,'empty contact social settings produce no invented accounts');
  await page.goto(base+'/admin/banners');await page.getByRole('button',{name:'افزودن بنر',exact:true}).click();
  const dialog=page.getByRole('dialog');await dialog.getByLabel('عنوان بنر',{exact:true}).fill('بنر بدون دکمه آزمایشی');
  check(!(await dialog.getByLabel('نمایش دکمه در بنر',{exact:true}).isChecked())&&await dialog.getByLabel('متن دکمه',{exact:true}).count()===0,'new banner starts with its CTA disabled and no required button inputs');
  await dialog.locator('input[type=file]').setInputFiles({name:'synthetic.png',mimeType:'image/png',buffer:Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/l9sAAAAASUVORK5CYII=','base64')});
  await dialog.locator('.banner-image-preview img').waitFor();
  const saving=page.waitForResponse(r=>r.url().endsWith('/api/admin/banners')&&r.request().method()==='PUT');
  await dialog.getByRole('button',{name:'ذخیره بنر',exact:true}).click();assert.equal((await saving).status(),200);
  await page.evaluate(async()=>{const r=await fetch('/api/admin/banners');const all=await r.json();const index=all.findIndex(b=>b.title==='بنر بدون دکمه آزمایشی');const [banner]=all.splice(index,1);all.unshift(banner);await fetch('/api/admin/banners',{method:'PUT',headers:{'Content-Type':'application/json'},body:JSON.stringify(all)});});
  await page.goto(base+'/');await page.getByRole('heading',{name:'بنر بدون دکمه آزمایشی',exact:true}).waitFor();
  check(await page.locator('.hero-banner-copy a').count()===0&&await page.locator('a.hero-banner').count()===0,'a banner without CTA has no empty button or whole-banner link');
  await page.goto(base+'/admin/banners');await page.getByRole('button',{name:'ویرایش بنر بدون دکمه آزمایشی',exact:true}).click();
  await page.getByLabel('نمایش دکمه در بنر',{exact:true}).check();await page.getByLabel('متن دکمه',{exact:true}).fill('مشاهده خدمات');await page.getByLabel('لینک دکمه',{exact:true}).fill('/services');
  const enabled=page.waitForResponse(r=>r.url().endsWith('/api/admin/banners')&&r.request().method()==='PUT');await page.getByRole('button',{name:'ذخیره بنر',exact:true}).click();assert.equal((await enabled).status(),200);
  await page.goto(base+'/');check(await page.locator('.hero-banner-copy').getByRole('link',{name:'مشاهده خدمات',exact:true}).getAttribute('href')==='/services','banner with an explicitly enabled CTA remains functional');
  const prior=await page.evaluate(async()=>await(await fetch('/api/admin/settings')).json());
  await page.goto(base+'/admin/settings');await page.getByLabel('لینک اینستاگرام',{exact:true}).fill('https://www.instagram.com/cms_giti_demo');await page.getByLabel('لینک تلگرام',{exact:true}).fill('https://t.me/cms_giti_demo');await page.getByLabel('لینک بله',{exact:true}).fill('https://ble.ir/cms_giti_demo');
  const socials=page.waitForResponse(r=>r.url().endsWith('/api/admin/settings')&&r.request().method()==='PUT');await page.getByRole('button',{name:'ذخیره تنظیمات',exact:true}).click();assert.equal((await socials).status(),200);
  await page.goto(base+'/contact');
  check(await page.locator('.contact-social-links a').count()===3,'configured Instagram/Telegram/Bale links appear on the contact page');
  for(const [name,url]of [['اینستاگرام','https://www.instagram.com/cms_giti_demo'],['تلگرام','https://t.me/cms_giti_demo'],['بله','https://ble.ir/cms_giti_demo']]){
    const link=page.getByRole('link',{name:name+' (در برگهٔ جدید)',exact:true});check(await link.getAttribute('href')===url&&(await link.getAttribute('rel')).includes('noopener'),'contact '+name+' has a safe functional URL and accessible label');
  }
  const after=await page.evaluate(async()=>await(await fetch('/api/admin/settings')).json());
  check(JSON.stringify(prior.rates)===JSON.stringify(after.rates)&&['phone','email','address','hours'].every(key=>prior.site[key]===after.site[key]),'social form save preserves all tariffs and previous contact fields');
}

export async function publicCleanupChecks({page,base,check}) {
  await page.goto(base+'/');
  check(await page.locator('a[href^="/blog"]').count()>0&&await page.locator('a[href^="/portfolio"]').count()===0,'magazine is restored in public navigation while portfolio stays retired');
  check(await page.locator('footer a[href^="/admin"]').count()===0,'public footer no longer advertises administrator login');
  check(await page.locator('.finishing-service-card').count()===4&&await page.locator('.finishing-service-card a,.finishing-service-card button,a.finishing-service-card,.finishing-service-card .card-link').count()===0,'finishing visuals remain informational with no calculator-selection behavior');
  check(await page.locator('main a[href="/calculator"]').count()>0&&await page.locator('a[href="/order"]').count()>0,'independent calculator and order actions remain available elsewhere');
  for(const route of ['/portfolio','/portfolio/hardcover']){await page.goto(base+route);await page.waitForURL('**/services');check(true,'legacy public '+route+' redirects deliberately to services');}
  await page.goto(base+'/admin');
  check(await page.locator('.admin-nav a[href="/admin/services"],.admin-nav a[href="/admin/banners"],.admin-nav a[href="/admin/settings"]').count()===3,'administrator navigation retains services, banners and site/contact settings');
  check(await page.locator('.admin-nav a[href="/admin/articles"]').count()===1&&await page.locator('.admin-nav a[href="/admin/portfolio"]').count()===0,'administrator magazine is active while portfolio stays retired');
  for(const route of ['/admin/portfolio']){await page.goto(base+route);await page.waitForURL('**/admin/services');check(true,'legacy administrator '+route+' redirects to services management');}
  const archive=await page.evaluate(async()=>({portfolio:await(await fetch('/api/admin/portfolio')).json(),articles:await(await fetch('/api/admin/articles')).json(),public:await(await fetch('/api/public/bootstrap')).json()}));
  check(archive.portfolio.length>0&&archive.public.portfolio.length===0&&archive.public.articles.length===0,'historical portfolio data remains in the private API while public bootstrap has no retired content');
  await page.goto(base+'/admin/login');check(await page.locator('input[name=username]').count()===1 || new URL(page.url()).pathname==='/admin','independent administrator login remains available despite removal of its footer link');
}
