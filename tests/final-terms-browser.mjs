import assert from "node:assert/strict";

export async function acceptFinalConditions(admin, customer, base, orderId) {
  const adminPath = base + "/api/admin/orders/" + orderId;
  const current = async () => (await (await admin.request.get(adminPath)).json()).order.version;
  const attempt = async (action, fields = {}) => admin.request.post(adminPath + "/" + action, {
    headers: {Origin:base}, data:{expectedVersion:await current(), idempotencyKey:crypto.randomUUID(), ...fields},
  });
  await admin.reload();
  await admin.getByText("تنظیم شرایط نهایی", {exact:true}).click();
  assert.equal(await admin.getByRole("button", {name:"ارسال پیشنهاد قیمت", exact:true}).count(), 0);
  assert.equal((await attempt("quotes",{amountToman:360000,scopeText:"قیمت جایگزین آزمایشی"})).status(),409);
  assert.equal((await attempt("start-printing")).status(),409);
  await admin.getByLabel("شرایط تحویل",{exact:true}).fill("تحویل حضوری پس از چاپ");
  await admin.getByLabel("محدوده خدمات غیرمالی").fill("چاپ مطابق آخرین فایل تأییدشده");
  const issued = admin.waitForResponse(r => r.request().method()==="POST" && r.url().endsWith("/delivery-terms"));
  await admin.getByRole("button",{name:"ارسال شرایط نهایی برای تأیید مشتری"}).click();
  assert.equal((await issued).status(),200);
  await customer.goto(base+"/account/orders/"+orderId);
  await customer.getByRole("definition").filter({hasText:"تحویل حضوری پس از چاپ"}).waitFor();
  const expected=(await (await admin.request.get(adminPath)).json()).order;
  assert.ok(expected.approved_file_id && expected.final_price>0);
  const linked = await customer.request.post(base+"/api/tickets",{headers:{Origin:base},data:{
    orderId,subject:"پیگیری شرایط نهایی",message:"پیام پشتیبانی درباره تأیید چاپ",idempotencyKey:crypto.randomUUID(),
  }});
  assert.ok([200,201].includes(linked.status()),"existing or new order-linked support thread");
  const ticket=await linked.json();
  const thread=await (await customer.request.get(base+"/api/tickets/"+ticket.id)).json();
  assert.ok(thread.messages.some(item=>item.message==="پیام پشتیبانی درباره تأیید چاپ"));
  const afterDiscussion=await (await admin.request.get(adminPath)).json();
  assert.equal(afterDiscussion.order.approved_file_id,expected.approved_file_id);
  assert.equal(afterDiscussion.order.final_price,expected.final_price);
  assert.equal(afterDiscussion.order.status,expected.status);
  assert.ok(!afterDiscussion.finalConditions.acceptedAt);
  assert.equal((await attempt("start-printing")).status(),409);
  const accepted = customer.waitForResponse(r => r.request().method()==="POST" && r.url().endsWith("/delivery-terms/accept"));
  await customer.getByRole("button",{name:"تأیید نهایی و ثبت توافق"}).click();
  assert.equal((await accepted).status(),200);
  await admin.reload();
  return ticket.id;
}
