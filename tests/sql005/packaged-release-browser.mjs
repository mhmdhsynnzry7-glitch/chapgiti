import assert from "node:assert/strict";
import { createHash, X509Certificate } from "node:crypto";
import { readFileSync } from "node:fs";
import { createRequire } from "node:module";
import { checkQuoteSummary, checkSignedInName } from "../customer-estimate-ui.mjs";
import { pdfFixture } from "../pdf-fixture.mjs";

const requireFrontend = createRequire(new URL("../../frontend/package.json", import.meta.url));
const { chromium } = requireFrontend("playwright");

export async function checkPackagedBrowser(site, password, fileBytes) {
  assert.equal(site.hostname, "127.0.0.1");
  assert.equal(site.protocol, "https:");
  // Trust only the self-signed certificate that the disposable fixture just created.
  const certificate = new X509Certificate(readFileSync(process.env.NODE_EXTRA_CA_CERTS));
  const key = certificate.publicKey.export({ type: "spki", format: "der" });
  const pin = createHash("sha256").update(key).digest("base64");
  const browser = await chromium.launch({
    headless: true,
    executablePath: process.env.CHROME_PATH || "C:/Program Files/Google/Chrome/Application/chrome.exe",
    args: ["--ignore-certificate-errors-spki-list=" + pin],
  });
  const errors = [];
  function check(value, message) { assert.ok(value, message); console.log("PASS packaged browser: " + message); }
  async function newTab() {
    const context = await browser.newContext({ viewport: { width: 1280, height: 900 } });
    await context.route("**/*", route => new URL(route.request().url()).origin === site.origin ? route.continue() : route.abort());
    const page = await context.newPage();
    page.on("pageerror", err => errors.push(err.name));
    page.on("console", msg => { if (msg.type() === "error" && /hydrat|Minified React error #41[89]/i.test(msg.text())) errors.push("hydration"); });
    page.setDefaultTimeout(20000);
    return page;
  }
  async function loginCustomer(page, phone) {
    await page.goto(site.origin + "/customer?next=%2Faccount");
    await page.locator("input[name=phone]").fill(phone);
    await page.locator("input[name=password]").fill(password);
    await page.locator("form").getByRole("button", {name: "ورود", exact: true}).click();
    await page.waitForURL(site.origin + "/account");
  }
  const posted = (page, route) => page.waitForResponse(response =>
    response.request().method() === "POST" && response.url() === site.origin + route);
  try {
    const customer = await newTab(), admin = await newTab(), other = await newTab();
    let notificationFrames = 0;
    admin.on("websocket", socket => {
      if (new URL(socket.url()).pathname === "/api/realtime/admin/notifications") {
        socket.on("framereceived", ({payload}) => {
          if (String(payload).includes('"target":"Changed"')) notificationFrames++;
        });
      }
    });
    await admin.goto(site.origin + "/admin/login");
    await admin.locator("input[name=password]").fill(password);
    await admin.getByRole("button",{name:"ورود به مدیریت",exact:true}).click();
    await admin.waitForURL(site.origin + "/admin");
    await loginCustomer(customer, "09100000001");
    await checkSignedInName(customer, site.origin, check);
    await customer.reload();
    await customer.locator(".customer-header-name").waitFor();
    await customer.goto(site.origin + "/calculator");
    const pendingQuote = posted(customer, "/api/quote");
    await customer.getByRole("button",{name:"محاسبه هزینه",exact:true}).click();
    const quote = await pendingQuote;
    assert.equal(quote.status(), 200);
    await checkQuoteSummary(customer, await quote.json(), check);
    await customer.getByRole("link",{name:"ارسال درخواست چاپ",exact:true}).click();
    await customer.locator("input[name=title]").fill("سفارش آزمایشی مرورگر بسته");
    await customer.getByLabel(/فایل متن و جلد را انتخاب کنید/).setInputFiles({
      name:"browser-order.pdf", mimeType:"application/pdf", buffer:pdfFixture("packaged browser order"),
    });
    await customer.getByRole("checkbox",{name:/شرایط ثبت سفارش/}).check();
    const pendingOrder = posted(customer, "/api/orders");
    await customer.getByRole("button",{name:"ارسال درخواست چاپ",exact:true}).click();
    const order = await pendingOrder;
    assert.equal(order.status(), 201);
    const {id:orderId} = await order.json();
    await customer.getByRole("main").getByRole("link", {name:"پیگیری سفارش", exact:true}).click();
    await customer.waitForURL(site.origin + "/account/orders/" + orderId);
    await customer.getByText("سفارش آزمایشی مرورگر بسته", {exact:true}).first().waitFor();
    check(true, "signed-in customer submits an order PDF and opens the resulting order");
    await customer.setViewportSize({width:390,height:844});
    await customer.goto(site.origin + "/tickets");
    const form = customer.locator("#new-ticket");
    await form.getByLabel("موضوع تیکت").fill("پشتیبانی آزمایشی مرورگر بسته");
    await form.getByLabel("متن درخواست").fill("فایل پیام اول از داخل مرورگر ارسال شد.");
    await form.getByLabel("پیوست فایل (اختیاری)", {exact:true}).setInputFiles({
      name:"release-smoke.pdf", mimeType:"application/pdf", buffer:fileBytes,
    });
    check(await form.getByText("release-smoke.pdf",{exact:true}).isVisible(), "initial general-ticket attachment is selected in the mobile form");
    const previousFrames = notificationFrames;
    const pendingTicket = posted(customer,"/api/tickets/attachments");
    await form.getByRole("button",{name:"ارسال تیکت",exact:true}).click();
    const created = await pendingTicket;
    assert.equal(created.status(), 201);
    const {id} = await created.json();
    await customer.waitForURL(site.origin + "/tickets/" + id);
    await customer.reload();
    await customer.getByRole("link",{name:"release-smoke.pdf",exact:true}).waitFor();
    for (let i = 0; i < 60 && notificationFrames === previousFrames; i++) {
      await new Promise(done => setTimeout(done, 100));
    }
    check(notificationFrames > previousFrames, "the administrator browser received the new ticket notification over the packaged WebSocket route");
    await admin.goto(site.origin + "/admin/tickets/" + id);
    const link = admin.getByRole("link",{name:"release-smoke.pdf",exact:true});
    const [download] = await Promise.all([admin.waitForEvent("download"),link.click()]);
    assert.equal(download.suggestedFilename(),"release-smoke.pdf");
    assert.deepEqual(readFileSync(await download.path()),fileBytes);
    check(true, "independent administrator downloads the byte-identical initial attachment");
    await loginCustomer(other, "09100000002");
    await other.goto(site.origin + "/tickets");
    assert.equal(await other.getByText("پشتیبانی آزمایشی مرورگر بسته",{exact:true}).count(),0);
    assert.deepEqual(errors,[]);
    check(true,"private conversations remain separated after navigation and refresh, with no hydration or page errors");
    return id;
  } finally { await browser.close(); }
}
