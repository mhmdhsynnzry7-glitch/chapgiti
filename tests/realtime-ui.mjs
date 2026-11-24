import assert from "node:assert/strict";
import { submitPrintRequest } from "./pdf-fixture.mjs";

// Existing loopback/memory API harness only. Both roles share one browser's independent cookie families.
export async function realtimeChecks({browser,page:admin,base,check,control}) {
  const context=admin.context();
  // Model two simultaneously visible actors; visibility lifecycle is covered separately in unit tests.
  await context.addInitScript(()=>Object.defineProperty(document,"visibilityState",{configurable:true,get:()=>"visible"}));
  // Exercise standard SignalR transport negotiation with WebSocket unavailable at the proxy.
  await context.routeWebSocket("**/api/realtime/**",socket=>socket.close());
  const customer=await context.newPage(),outsiderContext=await browser.newContext(),errors=[];
  await outsiderContext.route("**/*",route=>route.request().url().startsWith(base)?route.continue():route.abort());
  const outsider=await outsiderContext.newPage();
  for(const target of [admin,customer,outsider])target.on("pageerror",error=>errors.push(error.message));
  const center=target=>target.locator(".notification-popover:visible");
  async function api(target,method,path,body,expected=200){
    const result=await target.evaluate(async({method,path,body})=>{
      const response=await fetch("/api/"+path,{method,headers:{"Content-Type":"application/json"},...(body?{body:JSON.stringify(body)}:{})});
      return{status:response.status,body:await response.json()};
    },{method,path,body});
    assert.equal(result.status,expected,path);return result.body;
  }
  async function until(predicate,label,timeout=5000){
    const end=Date.now()+timeout;
    while(Date.now()<end){if(await predicate())return;await new Promise(resolve=>setTimeout(resolve,80));}
    assert.fail(label);
  }
  async function connected(target,selector){
    if (selector===center && !await center(target).isVisible()) await target.locator(".notification-bell").click();
    await until(()=>selector(target).getAttribute("data-realtime-state").then(value=>value==="connected"),"SignalR establishes its authorized subscription",20000);
  }
  async function notice(target,scope,entityId,eventType){
    const list=await api(target,"GET",scope+"/notifications?pageSize=50");
    return list.items.find(item=>item.entityId===entityId&&item.eventType===eventType);
  }
  try{
    await admin.reload();await customer.goto(base+"/account");
    await connected(admin,center);await connected(customer,center);
    check(true,"realtime browser negotiates a fallback transport when the proxy cannot upgrade WebSocket");
    const cookieNames=(await context.cookies()).map(cookie=>cookie.name);
    check(cookieNames.includes("giti_customer_access")&&cookieNames.includes("giti_admin_access"),"realtime browser keeps customer/admin authentication independent in the same browser");
    await outsider.goto(base+"/customer?change=1&next=/account");
    await outsider.locator("input[name=phone]").fill("09100000000");await outsider.locator("input[name=password]").fill("Old.Test.Password@1");
    await outsider.locator("form").getByRole("button",{name:"ورود",exact:true}).click();await outsider.waitForURL("**/account");await connected(outsider,center);
    const requestKey=crypto.randomUUID(),creation={idempotencyKey:requestKey,title:"SignalR browser request",spec:{pages:120,quantity:1,size:"raghi",paper:"white",color:"bw",binding:"soft"}};
    // Raw REST calls intentionally do not fire the frontend's local refresh event.
    const created = await submitPrintRequest(customer, base, creation);
    assert.equal(created.status, 201);
    const { id: order } = created.body;
    const adminNotice=await notice(admin,"admin",order,"order.created");assert.ok(adminNotice);
    await until(()=>center(admin).locator('[data-notification-id="'+adminNotice.id+'"]').isVisible(),"admin receives committed notification without focus, polling or reload");
    check(true,"realtime administrator notification and badge update from another view in under five seconds");
    check(await center(outsider).locator(".notification-item").count()===0,"realtime notification never appears in another customer's center");
    assert.equal((await submitPrintRequest(customer, base, creation)).status, 200);
    check(await center(admin).locator('[data-notification-id="'+adminNotice.id+'"]').count()===1,"realtime replayed order creates one stable notification item");
    await api(admin,"POST","admin/orders/"+order+"/open",{});
    const detail=await api(admin,"GET","admin/orders/"+order);
    await api(admin,"POST","admin/orders/"+order+"/quotes",{idempotencyKey:crypto.randomUUID(),expectedVersion:detail.order.version,amountToman:220000,scopeText:"Realtime test scope",deliveryTerms:"",exclusionsText:""});
    const customerNotice=await notice(customer,"customer",order,"quote.issued");assert.ok(customerNotice);
    await until(()=>center(customer).locator('[data-notification-id="'+customerNotice.id+'"]').isVisible(),"customer receives quote notification live");
    check(true,"realtime customer notification arrives without manual refresh");
    await center(customer).locator('[data-notification-id="'+customerNotice.id+'"] .notification-read').click();
    await until(()=>center(customer).locator('[data-notification-id="'+customerNotice.id+'"].is-read').isVisible(),"read state reconciles with the live item");
    check((await api(customer,"GET","customer/notifications/unread-count")).unreadCount===0,"realtime mark-read keeps persisted unread count correct");
    await customer.reload();await connected(customer,center);
    await until(()=>center(customer).locator('[data-notification-id="'+customerNotice.id+'"].is-read').isVisible(),"persisted read survives reload");
    check(await center(customer).locator('[data-notification-id="'+customerNotice.id+'"]').count()===1,"realtime reload retains persistence and never duplicates live items");
    for (const [target, scope] of [[customer, "customer"], [admin, "admin"]]) {
      const negotiation = await api(target, "POST", "realtime/" + scope + "/support/negotiate?negotiateVersion=1", {});
      assert(negotiation.availableTransports.some(t => t.transport === "WebSockets"));
    }
    const ticket = await api(customer, "POST", "tickets", {
      subject: "General support", message: "Please review this support request.", idempotencyKey: crypto.randomUUID()
    }, 201);
    const messageId = crypto.randomUUID();
    await api(admin, "POST", "admin/tickets/" + ticket.id + "/messages", { message: "Reply from the print shop", idempotencyKey: messageId }, 201);
    await api(outsider, "GET", "tickets/" + ticket.id, undefined, 404);
    const threadPath = "tickets/" + ticket.id;
    assert.equal((await api(customer, "GET", threadPath)).ticket.unread_customer, 1);
    await api(customer, "POST", threadPath + "/read", { throughId: messageId });
    const read = await api(customer, "GET", threadPath);
    check(read.ticket.unread_customer === 0 && !!read.messages.find(m => m.id === messageId)?.read_at,
      "authorized general support persists a customer read receipt and isolates the thread");
    async function newQuote(amount){const state=await api(admin,"GET","admin/orders/"+order);await api(admin,"POST","admin/orders/"+order+"/quotes",{idempotencyKey:crypto.randomUUID(),expectedVersion:state.order.version,amountToman:amount,scopeText:"Updated realtime scope",deliveryTerms:"",exclusionsText:""});return notice(customer,"customer",order,"quote.issued");}
    let negotiations=0;const negotiated=r=>{if(r.url().includes('/api/realtime/')&&r.url().includes('/negotiate'))negotiations++;};customer.on('request',negotiated);
    await control({disconnectRealtime:true});await until(()=>Promise.resolve(negotiations>0),"transport reconnect negotiates a fresh notification connection",12000);
    await connected(customer,center);await connected(admin,center);customer.off('request',negotiated);
    let item=await newQuote(240000);await until(()=>center(customer).locator('[data-notification-id="'+item.id+'"]').isVisible(),"notification delivery resumes after reconnect");
    check(true,"actual notification transport loss reconnects and restores the correct recipient subscription");
    const unavailable=r=>r.fulfill({status:503,json:{error:"isolated realtime outage"}});await customer.route("**/api/realtime/**",unavailable);
    await customer.reload();await customer.locator('.notification-bell').click();await until(()=>center(customer).getAttribute('data-realtime-state').then(v=>v==='offline'),"unavailable hub leaves HTTP reads usable",12000);
    item=await newQuote(260000);await until(()=>center(customer).locator('[data-notification-id="'+item.id+'"]').isVisible(),"polling recovers persisted notifications during transport outage",25000);
    check(true,"complete realtime outage retains the lightweight HTTP notification polling fallback");
    await customer.unroute("**/api/realtime/**",unavailable);await customer.evaluate(()=>window.dispatchEvent(new Event('online')));await connected(customer,center);
    check(await center(customer).locator('[data-notification-id="'+item.id+'"]').count()===1,"recovered notification connection does not duplicate persisted rows");
    const longPolling=async route=>{const response=await route.fetch(),body=await response.json();if(response.status()===200)body.availableTransports=body.availableTransports.filter(t=>t.transport==='LongPolling');await route.fulfill({response,json:body});};
    await customer.route('**/api/realtime/**/negotiate*',longPolling);await customer.reload();await connected(customer,center);
    item=await newQuote(280000);await until(()=>center(customer).locator('[data-notification-id="'+item.id+'"]').isVisible(),"LongPolling forwards committed notification events");
    check(true,"standard SignalR LongPolling works when WebSocket and SSE are unavailable");await customer.unroute('**/api/realtime/**/negotiate*',longPolling);
    await customer.setViewportSize({width:390,height:844});check(await customer.evaluate(()=>document.documentElement.dir==='rtl'&&document.documentElement.scrollWidth<=innerWidth+1),"realtime notifications remain responsive in RTL on mobile");
    check(errors.length===0,"realtime browser interactions have no uncaught JavaScript errors");
  }finally{await customer.close();await outsiderContext.close();}
}
