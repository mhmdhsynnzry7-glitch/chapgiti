import { requestToPrintChecks } from "../request-to-print-ui.mjs";
import { notificationChecks } from "../notifications-ui.mjs";
import assert from 'node:assert/strict';
import {spawn} from 'node:child_process';
import path from 'node:path';
import {existsSync,createWriteStream,mkdirSync,writeFileSync} from 'node:fs';
import {pathToFileURL} from 'node:url';
import {servicesCmsChecks,bannerContactChecks,publicCleanupChecks} from '../cms-v2-ui.mjs';
const [api,base]=process.argv.slice(2);
for(const url of [api,base])assert.equal(new URL(url).hostname,'127.0.0.1');
mkdirSync('.qa/full-audit',{recursive:true});
const env={...process.env};for(const key of Object.keys(env))if(/^(SQLSERVER_|ConnectionStrings__|Storage__|Sms__|Otp__|OTP_|MELIPAYAMAK_|DATA_DIR$)/i.test(key))delete env[key];
const output=createWriteStream('.qa/full-audit/sql-browser-next.log');
const web=spawn(process.execPath,[path.resolve('frontend/node_modules/next/dist/bin/next'),'start','--hostname','127.0.0.1','--port',new URL(base).port],{cwd:path.resolve('frontend'),env:{...env,NODE_ENV:'production',CHAP_GITI_VERIFY_BUILD:'true',SITE_URL:base,DOTNET_API_URL:api,INDEXING_ENABLED:'false'},windowsHide:true,stdio:['ignore','pipe','pipe']});web.stdout.pipe(output);web.stderr.pipe(output);
const runtime=['frontend/node_modules/playwright/index.mjs','.qa/browser-tools/node_modules/playwright-core/index.mjs'].find(existsSync);
const {chromium}=await import(pathToFileURL(path.resolve(runtime)).href);let browser;const checks=[];
function check(value,name){assert.ok(value,name);checks.push(name);console.log('PASS SQL BROWSER '+name);}
try{
 let ready=false;for(let n=0;n<120;n++){try{ready=(await fetch(base+'/customer',{signal:AbortSignal.timeout(2000)})).ok;}catch{}if(ready)break;await new Promise(r=>setTimeout(r,250));}assert.ok(ready);
 browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});
 const c=await browser.newContext();await c.route('**/*',r=>r.request().url().startsWith(base)?r.continue():r.abort());const p=await c.newPage();
 await p.goto(base+'/customer?change=1');await p.locator('input[name=phone]').fill('09100000001');await p.locator('input[name=password]').fill('Sql.Acceptance@2026');await p.locator('form').getByRole('button',{name:'ورود',exact:true}).click();await p.waitForURL('**/account');check(true,'customer password form authenticates through Next and SQL');
 async function request(method,route,body){return p.evaluate(async({method,route,body})=>{const r=await fetch('/api/'+route,{method,headers:{'Content-Type':'application/json'},...(body?{body:JSON.stringify(body)}:{})});return {status:r.status,body:await r.json()};},{method,route,body});}
 const created=await request('POST','orders',{idempotencyKey:crypto.randomUUID(),title:'SQL browser audit book',spec:{pages:120,quantity:1,size:'raghi',paper:'white',color:'bw',binding:'soft'}});check(created.status===201,'browser order request returns 201 with real SQL persistence');const orderId=created.body.id;
 const uploaded=await p.evaluate(async({id,code})=>{const r=await fetch('/api/orders/'+id+'/files',{method:'POST',headers:{'Content-Type':'application/pdf','X-Order-Code':code,'X-File-Name':'synthetic-audit.pdf'},body:'%PDF-1.4\n% isolated browser fixture\n%%EOF'});return {status:r.status,body:await r.json()};},{id:orderId,code:created.body.code});check(uploaded.status===201,'PDF bytes and metadata pass through the Next proxy into isolated storage/SQL');
 await p.goto(base+'/account/orders/'+orderId);await p.locator('.customer-order-details').waitFor();check((await p.locator('main').innerText()).includes('synthetic-audit.pdf'),'customer order details read the SQL file metadata');
 await p.goto(base+'/admin/login');await p.locator('input[name=password]').fill('Sql.Acceptance@2026');await p.getByRole('button',{name:'ورود به مدیریت',exact:true}).click();await p.waitForURL(url=>url.pathname==='/admin');check(true,'independent admin password form authenticates against isolated SQL');
 await p.goto(base+'/admin/orders/'+orderId);await p.getByRole('button',{name:'ارسال پیشنهاد قیمت',exact:true}).waitFor();check(true,'administrator explicit opening persists review through the real API');
 const note=await request('POST','admin/orders/'+orderId+'/notes',{idempotencyKey:crypto.randomUUID(),note:'Synthetic browser internal note'});check(note.status===200,'administrator internal note travels through Browser/Next/API/SQL');
 const ticket=await request('POST','tickets',{idempotencyKey:crypto.randomUUID(),orderId,subject:'SQL browser order question',message:'Synthetic question from the isolated browser audit.'});check(ticket.status===201,'independent customer session can create an owned-order ticket while admin is logged in');
 await p.goto(base+'/tickets/'+ticket.body.id);await p.locator('.ticket-thread').waitFor();check((await p.locator('main').innerText()).includes('Synthetic question'),'ticket conversation is read back through real SQL joins');
 await p.goto(base+'/account/orders/'+orderId);check(!(await p.locator('main').innerText()).includes('Synthetic browser internal note'),'private SQL note is absent from the customer page');
 await servicesCmsChecks({page:p,base,check});
 await bannerContactChecks({page:p,base,check});
 await publicCleanupChecks({page:p,base,check});
 const workflow=await requestToPrintChecks({browser,page:p,base,check,customerPhone:'09100000001',password:'Sql.Acceptance@2026'});
 await notificationChecks({browser,page:p,base,check,customerPhone:'09100000001',password:'Sql.Acceptance@2026'});
 writeFileSync('.qa/full-audit/sql-browser.json',JSON.stringify({checks,failed:0,orderId,fileId:uploaded.body.id,workflowFiles:workflow.workflowFiles,database:'guarded disposable LocalDB only',realSms:false},null,2));
}finally{
 await browser?.close();if(web.exitCode===null)await new Promise(r=>spawn('taskkill.exe',['/PID',String(web.pid),'/T','/F'],{windowsHide:true,stdio:'ignore'}).once('exit',r));output.end();
}
