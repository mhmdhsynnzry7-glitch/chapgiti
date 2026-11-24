// Local audit only: existing memory-only TestApiHost, loopback Next verification build,
// network allowlist, no operational configuration, SQL connection or real SMS.
import assert from 'node:assert/strict';
import { submitPrintRequest } from './pdf-fixture.mjs';
import { browserPort } from "./browser-port.mjs";
import path from 'node:path';
import { spawn } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, writeFileSync, createWriteStream } from 'node:fs';
import { pathToFileURL } from 'node:url';
const root=process.cwd(), stage=process.argv.includes('--load-only')?'load-check':process.argv.includes('--screenshots-only')?'screenshots':process.argv.includes('--focus-order')?'diagnostic':process.argv.includes('--final')?'final':'baseline';
const cmsV2=process.argv.includes('--cms-v2');
const out=process.env.CHAP_BROWSER_EVIDENCE ? path.resolve(process.env.CHAP_BROWSER_EVIDENCE) : cmsV2?path.resolve('.qa/cms-v2',process.argv.includes('--perf-recheck')?'performance-recheck':process.argv.includes('--perf-only')?'performance':'visual'):path.resolve('.qa/full-audit',stage);mkdirSync(out,{recursive:true});
assert.ok(existsSync('frontend/.next/BUILD_ID'));
assert.notEqual(JSON.parse(readFileSync('frontend/.next/required-server-files.json','utf8')).config.output,'standalone');
const runtime=['frontend/node_modules/playwright/index.mjs','.qa/browser-tools/node_modules/playwright-core/index.mjs'].find(existsSync);
const {chromium}=await import(pathToFileURL(path.resolve(runtime)).href);
const children=[],logs=[],errors=[],result={stage,buildId:readFileSync('frontend/.next/BUILD_ID','utf8').trim(),pages:[],responsive:[],accessibility:[],performance:[],load:[],checks:[],errors};
const pause=ms=>new Promise(r=>setTimeout(r,ms));
const port=await browserPort(),base=`http://127.0.0.1:${port}`,env={...process.env};
for(const key of Object.keys(env))if(/^(SQLSERVER_|ConnectionStrings__|Storage__|Sms__|Otp__|OTP_|MELIPAYAMAK_|DATA_DIR$)/i.test(key))delete env[key];
function start(file,args,name,options={}){const log=createWriteStream(path.join(out,name+'.log'));logs.push(log);const c=spawn(file,args,{cwd:root,env,windowsHide:true,stdio:['ignore','pipe','pipe'],...options});c.stdout.pipe(log);c.stderr.pipe(log);children.push(c);return c;}
async function stop(c){if(!c?.pid||c.exitCode!==null)return;await new Promise(r=>spawn('taskkill.exe',['/PID',String(c.pid),'/T','/F'],{windowsHide:true,stdio:'ignore'}).once('exit',r));}
async function processStats(){
 const ids=children.filter(c=>c.exitCode===null).map(c=>c.pid).join(',');
 const command=`@(Get-Process -Id ${ids} | Select-Object Id,CPU,WorkingSet64,HandleCount,@{Name='ThreadCount';Expression={$_.Threads.Count}}) | ConvertTo-Json -Compress`;
 return await new Promise((resolve,reject)=>{const c=spawn('powershell.exe',['-NoProfile','-Command',command],{windowsHide:true,stdio:['ignore','pipe','ignore']});let s='';c.stdout.on('data',b=>s+=b);c.once('exit',code=>{try{if(code!==0)throw Error('Process metrics unavailable');resolve(JSON.parse(s));}catch(e){reject(e);}});});
}
function check(ok,name){result.checks.push({name,pass:!!ok});console.log((ok?'PASS ':'FAIL ')+name);}
function save(){writeFileSync(path.join(out,process.argv.includes('--perf-only')?'performance.json':'browser.json'),JSON.stringify(result,null,2));}
let browser;
try{
 const api=start('dotnet',['backend/tests/ChapGiti.AuthenticationTests/bin/Verification/net10.0/ChapGiti.AuthenticationTests.dll','--serve-ui'],'api',{env:{...env,CHAP_GITI_TEST_SITE_URL:base}});
 const apiOrigin=await new Promise((resolve,reject)=>{let s='';const t=setTimeout(()=>reject(Error('Memory API startup timeout')),30000);api.stdout.on('data',b=>{s+=b;const m=s.match(/TEST_API_ORIGIN=(http:\/\/127\.0\.0\.1:\d+)/);if(m){clearTimeout(t);resolve(m[1]);}});api.once('exit',()=>reject(Error('Memory API exited')));});
 const web=start(process.execPath,[path.resolve('frontend/node_modules/next/dist/bin/next'),'start','--hostname','127.0.0.1','--port',String(port)],'web',{cwd:path.resolve('frontend'),env:{...env,CHAP_GITI_VERIFY_BUILD:'true',NODE_ENV:'production',SITE_URL:base,DOTNET_API_URL:apiOrigin,INDEXING_ENABLED:'false'}});
 let ready=false;for(let n=0;n<180;n++){try{ready=(await fetch(base+'/customer',{signal:AbortSignal.timeout(2000)})).ok;}catch{}if(ready)break;if(web.exitCode!==null)throw Error('Next startup failed');await pause(250);}assert.ok(ready);
 browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});
 async function context(){const c=await browser.newContext({viewport:{width:1440,height:1000}});await c.route('**/*',r=>r.request().url().startsWith(base)?r.continue():r.abort());c.on('page',p=>p.on('pageerror',e=>errors.push(e.message)));return c;}
 const guest=await context(),customer=await context(),admin=await context();
 async function request(c,method,url,data){const r=await c.request.fetch(base+'/api/'+url,{method,headers:{Origin:base},...(data?{data}:{})});let body;try{body=await r.json();}catch{body={};}return {status:r.status(),body};}
 const pw='Old.Test.Password@1';
 assert.equal((await request(customer,'POST','customer/login',{phone:'09000000826',password:pw})).status,200);
 assert.equal((await request(admin,'POST','auth/login',{username:'admin',password:pw})).status,200);
 const catalog=(await request(guest,'GET','public/services')).body;
 const firstService=catalog[0];assert.ok(firstService?.id&&firstService?.slug);
 if(cmsV2){
  const settings=(await request(admin,'GET','admin/settings')).body;
  settings.site.instagram='https://www.instagram.com/cms_giti_demo';settings.site.telegram='https://t.me/cms_giti_demo';settings.site.bale='https://ble.ir/cms_giti_demo';
  assert.equal((await request(admin,'PUT','admin/settings',settings)).status,200);
 }
 const order=await submitPrintRequest(customer,base,{idempotencyKey:crypto.randomUUID(),title:'کتاب آزمایشی ممیزی',spec:{pages:120,quantity:1,size:'raghi',paper:'white',color:'bw',binding:'soft'}});assert.equal(order.status,201);
 const orderId=order.body.id;
 const archiveResponse=await fetch(apiOrigin+'/__test/control',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({seedArchivedTicketFor:'09000000826'})});assert.ok(archiveResponse.ok);const ticketId=(await archiveResponse.json()).archiveTicketId;
 const publicPaths=['/','/services','/blog','/portfolio','/contact','/privacy','/customer?change=1','/customer/forgot-password','/admin/login','/session/renew?next=/account','/admin/session-renew?next=/admin'];
 const discover=await guest.newPage();
 for(const section of ['/services']){await discover.goto(base+section);const hrefs=await discover.locator(`main a[href^="${section}/"]`).evaluateAll(nodes=>nodes.map(a=>new URL(a.href).pathname));assert.ok(hrefs.length);publicPaths.push(...new Set(hrefs));}
 await discover.close();
 const protectedPaths=['/calculator','/order','/account','/account/orders','/account/orders/'+orderId,'/customer/profile','/tickets','/tickets/'+ticketId,'/track'];
 const adminPaths=['/admin/services','/admin/services/new','/admin/services/'+firstService.id,'/admin','/admin/orders','/admin/orders?status=new','/admin/orders/'+orderId,'/admin/customers','/admin/profile','/admin/banners','/admin/portfolio','/admin/articles','/admin/tickets','/admin/tickets/'+ticketId,'/admin/settings','/admin/activity','/admin/audit','/admin/messages','/admin/admins','/admin/sms'];
 const cases=[...publicPaths.map(url=>({role:'guest',context:guest,url})),...protectedPaths.map(url=>({role:'guest',context:guest,url})),...protectedPaths.map(url=>({role:'customer',context:customer,url})),...adminPaths.map(url=>({role:'admin',context:admin,url}))];
 const axes=path.resolve('frontend/node_modules/axe-core/axe.min.js');assert.ok(existsSync(axes));
 if(!process.argv.includes('--perf-only')&&!process.argv.includes('--load-only')){
 for(const item of (process.argv.includes('--focus-order')?cases.filter(c=>c.role==='customer'&&c.url==='/order'):process.argv.includes('--screenshots-only')?cases.filter(c=>(c.role==='guest'&&['/','/customer?change=1'].includes(c.url))||(c.role==='customer'&&['/calculator','/account/orders/'+orderId,'/tickets/'+ticketId].includes(c.url))||(c.role==='admin'&&['/admin','/admin/orders','/admin/orders/'+orderId].includes(c.url))):cases)){
  const p=await item.context.newPage();
  const renewing=item.url.startsWith('/session/renew')||item.url.startsWith('/admin/session-renew');
  // Authenticated pages keep a realtime subscription and can poll while they are ready.
  const response=await p.goto(base+item.url,{waitUntil:'domcontentloaded'});
  if(item.role==='guest' && protectedPaths.includes(item.url)){
   await p.waitForURL(url=>url.pathname==='/customer');
   await p.locator('form input[name=password]').waitFor({state:'visible'});
  }
  if(renewing)await p.getByRole('heading',{name:'دوباره وارد حساب شوید',exact:true}).waitFor({timeout:10000});
  else await p.getByRole('heading').first().waitFor({state:'visible'});
  if(item.role==='customer' && item.url==='/calculator') await p.locator('.book-calculator .catalog-trigger').first().waitFor();
  if(item.role==='customer' && item.url==='/account') await p.locator('.customer-orders tbody tr').first().waitFor();
  await p.evaluate(()=>document.fonts.ready);
  result.pages.push({role:item.role,url:item.url,status:response?.status(),destination:new URL(p.url()).pathname,title:await p.title(),h1:await p.locator('h1').count()});
  for(const width of [320,360,390,768,1024,1440,1920]){
   await p.setViewportSize({width,height:1000});await pause(40);
   const geometry=await p.evaluate(()=>({overflow:document.documentElement.scrollWidth-innerWidth,rtl:document.documentElement.dir==='rtl',font:getComputedStyle(document.body).fontFamily,offenders:document.documentElement.scrollWidth>innerWidth+1?[...document.querySelectorAll('main *')].filter(e=>{const r=e.getBoundingClientRect();return r.width>0&&(r.left<0||r.right>innerWidth)}).map(e=>({tag:e.tagName,class:e.getAttribute('class'),left:e.getBoundingClientRect().left,right:e.getBoundingClientRect().right,html:e.outerHTML.slice(0,180)})):[]}));
   result.responsive.push({role:item.role,url:item.url,width,...geometry});
  }
  await p.setViewportSize({width:1440,height:1000});
  await p.addScriptTag({path:axes});
  const a=await p.evaluate(async()=>{const r=await axe.run(document,{runOnly:{type:'tag',values:['wcag2a','wcag2aa','wcag21aa','wcag22aa']}});return {passes:r.passes.length,incomplete:r.incomplete.map(i=>({id:i.id,nodes:i.nodes.length})),violations:r.violations.map(v=>({id:v.id,impact:v.impact,help:v.help,nodes:v.nodes.map(n=>({target:n.target,html:n.html,summary:n.failureSummary}))}))};});
  result.accessibility.push({role:item.role,url:item.url,width:1440,...a});
  if(process.argv.includes('--final')){await p.setViewportSize({width:390,height:844});const mobileAxe=await p.evaluate(async()=>{const r=await axe.run(document,{runOnly:{type:'tag',values:['wcag2a','wcag2aa','wcag21aa','wcag22aa']}});return {passes:r.passes.length,violations:r.violations.map(v=>({id:v.id,impact:v.impact,nodes:v.nodes.map(n=>({target:n.target,html:n.html,summary:n.failureSummary}))}))};});result.accessibility.push({role:item.role,url:item.url,width:390,...mobileAxe});await p.setViewportSize({width:1440,height:1000});}
  if((cmsV2&&(['/services','/services/'+firstService.slug,'/contact','/admin/services','/admin/services/new','/admin/services/'+firstService.id,'/admin/banners','/admin/settings'].includes(item.url)))||['/','/customer?change=1','/calculator','/account/orders/'+orderId,'/tickets/'+ticketId,'/admin','/admin/orders','/admin/orders/'+orderId].includes(item.url)){
   const name=(item.role+'-'+item.url.replace(/[^a-z0-9]+/gi,'-')).slice(0,90);
   await pause(350);await p.screenshot({animations:'disabled',path:path.join(out,name+'-desktop.png'),fullPage:true});await p.setViewportSize({width:390,height:844});await pause(350);await p.screenshot({animations:'disabled',path:path.join(out,name+'-mobile.png'),fullPage:true});
  }
  console.log('AUDIT '+item.role+' '+item.url+' violations='+a.violations.length);save();await p.close();
 }
 const mobile=await guest.newPage();await mobile.setViewportSize({width:390,height:844});await mobile.goto(base+'/');
 const toggle=mobile.locator('.mobile-toggle');await toggle.click();check(await toggle.getAttribute('aria-expanded')==='true','mobile navigation announces expanded state');await mobile.keyboard.press('Escape');check(await toggle.getAttribute('aria-expanded')==='false'&&await toggle.evaluate(e=>e===document.activeElement),'Escape closes mobile nav and restores focus');await mobile.close();
 // Audit the actual phone -> OTP -> profile states with the fake sender, never a real provider.
 const registration=await guest.newPage();await registration.goto(base+'/customer?change=1');await registration.getByRole('button',{name:'ثبت‌نام',exact:true}).click();await registration.locator('input[name=phone]').fill('09129998877');await registration.getByRole('button',{name:'ادامه ثبت‌نام',exact:true}).click();await registration.getByLabel('کد تأیید',{exact:true}).waitFor();
 for(const state of ['otp','profile']){
  if(state==='profile'){await registration.getByLabel('کد تأیید',{exact:true}).fill('0123');await registration.getByRole('button',{name:'تأیید کد و ادامه',exact:true}).click();await registration.locator('input[name=firstName]').waitFor();}
  for(const width of [360,390,768,1024,1440,1920]){await registration.setViewportSize({width,height:1000});result.responsive.push({role:'registration-'+state,url:'/customer',width,...await registration.evaluate(()=>({overflow:document.documentElement.scrollWidth-innerWidth,rtl:document.documentElement.dir==='rtl'}))});}
  await registration.addScriptTag({path:axes});const a=await registration.evaluate(async()=>{const r=await axe.run({runOnly:{type:'tag',values:['wcag2a','wcag2aa','wcag21aa','wcag22aa']}});return {violations:r.violations.map(v=>({id:v.id,impact:v.impact,nodes:v.nodes.map(n=>({target:n.target,html:n.html,summary:n.failureSummary}))}))};});result.accessibility.push({role:'registration-'+state,url:'/customer',...a});await registration.screenshot({animations:'disabled',path:path.join(out,'registration-'+state+'.png'),fullPage:true});
 }
 await registration.close();
 }
 if(process.argv.includes('--performance')||process.argv.includes('--perf-only')){
 const selected=[['home',guest,'/'],['services',guest,'/services'],...(cmsV2?[['service-details',guest,'/services/'+firstService.slug],['contact',guest,'/contact']]:[]),['calculator',customer,'/calculator'],['login',guest,'/customer?change=1'],['account',customer,'/account'],['orders',customer,'/account/orders'],['order',customer,'/account/orders/'+orderId],['ticket',customer,'/tickets/'+ticketId],['admin',admin,'/admin'],['admin-order',admin,'/admin/orders/'+orderId]];
 const onlyPage=process.argv.find(a=>a.startsWith('--perf-page='))?.split('=')[1];
 const onlyDevice=process.argv.find(a=>a.startsWith('--perf-device='))?.split('=')[1];
 for(const [name,c,url]of selected.filter(item=>!onlyPage||item[0]===onlyPage))for(const device of ['mobile','desktop'].filter(item=>!onlyDevice||item===onlyDevice))for(let run=1;run<=3;run++){
  const p=await c.newPage();await p.setViewportSize(device==='mobile'?{width:390,height:844}:{width:1440,height:1000});const cdp=await c.newCDPSession(p);await cdp.send('Network.enable');await cdp.send('Network.setCacheDisabled',{cacheDisabled:true});await cdp.send('Emulation.setCPUThrottlingRate',{rate:device==='mobile'?4:1});await cdp.send('Network.emulateNetworkConditions',{offline:false,latency:device==='mobile'?150:20,downloadThroughput:device==='mobile'?200000:12500000,uploadThroughput:device==='mobile'?93750:12500000});
  const apiRequests=[];p.on('request',r=>{const url=new URL(r.url());if(url.pathname.startsWith('/api/'))apiRequests.push(r.method()+' '+url.pathname+url.search);});
  const failedResources=[];p.on('requestfailed',r=>{if(r.failure()?.errorText!=='net::ERR_ABORTED')failedResources.push({path:new URL(r.url()).pathname,error:r.failure()?.errorText});});p.on('response',r=>{if(r.status()>=500)failedResources.push({path:new URL(r.url()).pathname,status:r.status()});});
  let bytes=0,jsBytes=0,requests=0;const types=new Map();cdp.on('Network.responseReceived',e=>types.set(e.requestId,e.type));cdp.on('Network.requestWillBeSent',()=>requests++);cdp.on('Network.loadingFinished',e=>{bytes+=e.encodedDataLength;if(types.get(e.requestId)==='Script')jsBytes+=e.encodedDataLength;});
  await p.addInitScript(()=>{window.__metrics={lcp:0,cls:0,layoutShiftSum:0,eventDurations:[]};let firstShift=0,lastShift=0,sessionScore=0;new PerformanceObserver(l=>{for(const e of l.getEntries()){window.__metrics.lcp=e.startTime;window.__metrics.lcpElement=e.element?e.element.tagName+'.'+(e.element.getAttribute('class')||''):null;}}).observe({type:'largest-contentful-paint',buffered:true});new PerformanceObserver(l=>{for(const e of l.getEntries())if(!e.hadRecentInput){window.__metrics.layoutShiftSum+=e.value;if(lastShift>0&&e.startTime-lastShift<1000&&e.startTime-firstShift<5000){sessionScore+=e.value;}else{firstShift=e.startTime;sessionScore=e.value;}lastShift=e.startTime;window.__metrics.cls=Math.max(window.__metrics.cls,sessionScore);}}).observe({type:'layout-shift',buffered:true});try{new PerformanceObserver(l=>{for(const e of l.getEntries())if(e.interactionId)window.__metrics.eventDurations.push(e.duration);}).observe({type:'event',buffered:true,durationThreshold:16});}catch{}});
  // A live subscription may still be receiving data after the screen is interactive.
  await p.goto(base+url,{waitUntil:'load'});
  await p.locator(name==='calculator' ? '.book-calculator' : 'body').waitFor({state:'visible'});
  await p.evaluate(()=>document.fonts.ready);await pause(350);
  // A deterministic focus/click is a lab interaction, not real-user INP.
  const interactionStart=performance.now();await p.keyboard.press('Tab');await p.evaluate(()=>new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r))));const tabToPaintMs=performance.now()-interactionStart;await pause(100);
  const m=await p.evaluate(()=>{const n=performance.getEntriesByType('navigation')[0];return {...window.__metrics,ttfb:n.responseStart-n.requestStart,domContentLoaded:n.domContentLoadedEventEnd,resources:performance.getEntriesByType('resource').map(r=>({path:new URL(r.name).pathname,duration:r.duration})).sort((a,b)=>b.duration-a.duration).slice(0,3)};});
  result.performance.push({page:name,device,run,...m,tabToPaintMs,failedResources,transferBytes:bytes,jsBytes,requests,apiRequests,duplicateApiRequests:apiRequests.filter((r,i)=>apiRequests.indexOf(r)!==i)});console.log(`PERF ${name} ${device} ${run} LCP=${Math.round(m.lcp)}`);save();await cdp.detach();await p.close();
 }
 }
 if(process.argv.includes('--load')){
 const profiles=[['public-page',guest,'PAGE','/services'],['public',guest,'GET','public/bootstrap'],['quote',customer,'POST','quote',{pages:120,quantity:1,size:'raghi',paper:'white',color:'bw',binding:'soft'}],['orders',customer,'GET','customer/orders'],['tickets',customer,'GET','tickets'],['profile',customer,'GET','customer/me'],['password-login',customer,'POST','customer/login',{phone:'09000000826',password:pw}]];
 for(const concurrency of [5,10,25]){
  const samples=[];let aborted=false;const before=await processStats();const started=Date.now();
  for(const [name,c,method,url,body]of profiles){const tasks=Array.from({length:concurrency},async()=>{const t=performance.now();const r=method==='PAGE'?{status:(await c.request.get(base+url)).status()}:await request(c,method,url,body);return {name,status:r.status,ms:performance.now()-t};});const rows=await Promise.all(tasks);samples.push(...rows);if(rows.some(r=>r.status!==200&&r.status!==429)){aborted=true;break;}}
  const elapsedMs=Date.now()-started;const after=await processStats();result.load.push({concurrency,elapsedMs,before,after,samples,aborted});save();console.log('LOAD concurrency='+concurrency+' requests='+samples.length+' non200='+samples.filter(r=>r.status!==200).length);if(aborted)break;
 }
 }
 if (process.argv.includes('--load-only') && result.load.length) {
  // One customer login in setup uses the first slot in the 15-request window.
  const logins=result.load.flatMap(batch=>batch.samples).filter(sample=>sample.name==='password-login');
  check(logins.length===40 && logins.filter(sample=>sample.status===200).length===14 &&
    logins.filter(sample=>sample.status===429).length===26, 'the isolated login limiter counts every concurrent request exactly once');
 }
 if(result.load.length)check(result.load.every(batch=>!batch.aborted&&batch.samples.every(sample=>sample.status===200||sample.status===429)),'controlled traffic has no unexpected HTTP errors');
 if(process.argv.includes('--final')){
  await stop(api);
  const unavailable=await request(guest,'GET','public/bootstrap');
  check(unavailable.status===502&&typeof unavailable.body.error==='string'&&!JSON.stringify(unavailable.body).includes(apiOrigin),'Next proxy maps a disconnected backend to safe HTTP 502');
 }
 if(result.accessibility.length)check(result.accessibility.every(r=>r.violations.length===0),'all audited desktop/mobile states have no automated axe violations');
 if(result.responsive.length)check(result.responsive.every(r=>r.overflow<=1),'all inspected routes fit requested viewports and 320px reflow');check(errors.length===0,'no browser runtime errors');save();
 if(process.argv.includes('--final'))assert.ok(result.checks.every(c=>c.pass),'Final audit has failed checks');
}catch(e){result.fatal=e.message;save();throw e;}
finally{await browser?.close();for(const c of children.reverse())await stop(c);for(const l of logs)l.end();}
