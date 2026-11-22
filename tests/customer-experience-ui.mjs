import assert from "node:assert/strict";
import { pdfFixture } from "./pdf-fixture.mjs";
import { writeFileSync } from "node:fs";
import { formatToman } from "../frontend/lib/money.ts";

// Called by the existing browser harness: all data and SMS live in TestApiHost.
export async function customerExperienceChecks({ browser, base, control, check, browserErrors, adminPage }) {
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
  await context.route("**/*", route => route.request().url().startsWith(base) ? route.continue() : route.abort());
  const page = await context.newPage();
  page.on("pageerror", error => browserErrors.push(error.message));
  const checks = [];
  const verify = (condition, name) => { check(condition, name); checks.push(name); };
  const phone = "09188888888", password = "Customer.Experience@1";
  let otpRequests = 0, loginRequests = 0, anonymous;
  page.on("request", request => {
    if (request.method() === "POST" && request.url().endsWith("/api/customer/otp/request")) otpRequests++;
    if (request.method() === "POST" && request.url().endsWith("/api/customer/login")) loginRequests++;
  });
  try {
    await page.goto(base + "/");
    const nav = page.getByRole("navigation", { name: "منوی اصلی", exact: true });
    verify(await nav.getByRole("link", { name: "پیگیری سفارش", exact: true }).count() === 0, "public main navigation no longer exposes legacy tracking");
    await page.goto(base + "/track");
    verify(new URL(page.url()).searchParams.get("next")==="/track" && await page.locator("input[name=password]").isVisible(), "historical tracking entry redirects guests to customer login");

    await page.goto(base + "/customer?change=1&next=" + encodeURIComponent("/account/orders"));
    verify(await page.locator(".auth-methods").getByRole("button", { name: "ورود", exact: true }).isVisible() && await page.getByText("ورود مدیران قدیمی با نام کاربری", { exact: true }).count() === 0,
      "the ordinary login UI uses ورود and has no legacy-admin chooser");
    const missingMessage = "حساب کاربری با این شماره موبایل پیدا نشد. لطفاً ابتدا ثبت‌نام کنید.";
    const incorrectMessage = "رمز عبور واردشده صحیح نیست. لطفاً دوباره تلاش کنید.";
    const registrationLink = page.locator("form + p").getByRole("link", { name: "ثبت‌نام کنید", exact: true });
    verify(await registrationLink.isVisible(), "a registration link is visible below the password form before any error");
    await page.locator("input[name=phone]").fill("۰۹۱۸۸۸۸۸۸۸۸");
    await page.locator("input[name=password]").fill("Wrong.Experience@1");
    const missingResponse = page.waitForResponse(response => response.url().endsWith("/api/customer/login") && response.request().method() === "POST");
    await page.locator("form").getByRole("button", { name: "ورود", exact: true }).click();
    const missingLogin = await missingResponse;
    await page.locator("form [role=alert]").waitFor();
    verify(missingLogin.status() === 401 && (await missingLogin.json()).code === "CUSTOMER_ACCOUNT_NOT_FOUND" &&
      await page.locator("form [role=alert]").textContent() === missingMessage && await registrationLink.isVisible(),
      "unknown mobile login displays the exact account-not-found message from its structured API code");
    await page.locator("form [role=alert]").getByRole("link", { name: "ثبت‌نام کنید", exact: true }).click();
    verify(await page.locator("input[name=phone]").inputValue() === phone && await page.locator("input[name=password]").count() === 0 &&
      !new URL(page.url()).searchParams.has("phone") && otpRequests === 0,
      "the error registration link retains the normalized mobile without password fields, navigation PII or OTP requests");
    await page.locator(".auth-methods").getByRole("button", { name: "ورود", exact: true }).click();
    await registrationLink.click();
    verify(await page.locator("input[name=phone]").inputValue() === phone && await page.locator("input[name=password]").count() === 0,
      "the permanent registration link also opens registration with the entered mobile prefilled");
    verify(await page.locator("form input").count() === 1 && await page.locator("input[name=phone]").isVisible(), "registration starts with a mobile-only form");
    await page.locator("input[name=phone]").fill("۰۹۰۰۰۰۰۰۸۲۶");
    await page.getByRole("button", { name: "ادامه ثبت‌نام", exact: true }).click();
    await page.locator("input[name=password]").waitFor();
    verify(await page.locator("input[name=phone]").inputValue() === "09000000826" && await page.locator("input[name=firstName]").count() === 0 && otpRequests === 0,
      "existing normalized mobile numbers return to password login without sending OTP or showing a duplicate profile");
    await page.locator("input[name=password]").fill("Wrong.Experience@1");
    const incorrectResponse = page.waitForResponse(response => response.url().endsWith("/api/customer/login") && response.request().method() === "POST");
    await page.locator("form").getByRole("button", { name: "ورود", exact: true }).click();
    const incorrectLogin = await incorrectResponse;
    await page.locator("form [role=alert]").waitFor();
    verify(await page.locator("form").getByRole("button", { name: "ورود", exact: true }).isEnabled(), "invalid credentials show a Persian error and leave the submit button usable");
    verify(incorrectLogin.status() === 401 && (await incorrectLogin.json()).code === "CUSTOMER_PASSWORD_INCORRECT" &&
      await page.locator("form [role=alert]").textContent() === incorrectMessage && await registrationLink.isVisible() &&
      await page.getByRole("link", { name: "رمز عبور را فراموش کرده‌ام", exact: true }).isVisible(),
      "wrong passwords display the exact distinct message while registration and password recovery remain available");

    for (const [code, expected] of [["CUSTOMER_ACCOUNT_NOT_FOUND", missingMessage], ["CUSTOMER_PASSWORD_INCORRECT", incorrectMessage]]) {
      const routePattern = "**/api/customer/login";
      await page.route(routePattern, route => route.fulfill({ status: 401, contentType: "application/json",
        body: JSON.stringify({ code, error: "Unrelated provider message; do not infer the login error from this text." }) }));
      await page.locator("form").getByRole("button", { name: "ورود", exact: true }).click();
      await page.waitForFunction(message => document.querySelector("form [role=alert]")?.textContent === message, expected);
      verify(await page.locator("form [role=alert]").textContent() === expected,
        "regression: frontend login errors are selected by " + code + " rather than backend text");
      await page.unroute(routePattern);
    }
    await page.getByRole("link", { name: "رمز عبور را فراموش کرده‌ام", exact: true }).click();
    await page.waitForURL(url => url.pathname === "/customer/forgot-password");
    verify(await page.getByRole("heading", { name: "بازیابی رمز عبور", exact: true }).isVisible() &&
      new URL(page.url()).searchParams.get("next") === "/account/orders",
      "password recovery remains actionable after incorrect-password errors and preserves the destination");
    await page.goto(base + "/customer?change=1&next=" + encodeURIComponent("/account/orders"));

    await page.getByRole("button", { name: "ثبت‌نام", exact: true }).click();
    await page.locator("input[name=phone]").fill("٠٩١٨٨٨٨٨٨٨٨");
    const challengeRequest = page.waitForRequest(request => request.url().endsWith("/api/customer/otp/request") && request.method() === "POST");
    await page.getByRole("button", { name: "ادامه ثبت‌نام", exact: true }).click();
    await page.getByLabel("کد تأیید", { exact: true }).waitFor();
    verify(await page.locator("input[name=firstName],input[name=password]").count() === 0 && otpRequests === 1 &&
      JSON.stringify((await challengeRequest).postDataJSON()) === JSON.stringify({ phone, purpose: "register" }),
      "a normalized new mobile requests OTP without collecting or sending profile/password fields");
    const noGrant = await page.evaluate(async () => (await fetch("/api/customer/registration/complete", {
      method: "POST", headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ firstName: "Test", lastName: "Customer", password: "Customer.Experience@1", confirmation: "Customer.Experience@1" }),
    })).status);
    verify(noGrant === 400, "the actual API rejects profile submission before OTP verification");
    await page.getByLabel("کد تأیید", { exact: true }).fill("9999");
    await page.getByRole("button", { name: "تأیید کد و ادامه", exact: true }).click();
    await page.locator("form [role=alert]").waitFor();
    verify(otpRequests === 1 && await page.locator("input[name=firstName]").count() === 0,
      "incorrect OTP keeps profile and password fields hidden without requesting another code");
    await page.getByLabel("کد تأیید", { exact: true }).fill("٠١٢٣");
    const verificationResponse = page.waitForResponse(response => response.url().endsWith("/api/customer/otp/verify") && response.request().method() === "POST");
    await page.getByRole("button", { name: "تأیید کد و ادامه", exact: true }).click();
    const verifiedResponse = await verificationResponse;
    const verificationBody = verifiedResponse.request().postDataJSON();
    verify(Object.keys(verificationBody).sort().join(",") === "challengeId,code" &&
      /^[a-f0-9]{64}$/.test(verificationBody.challengeId) && verificationBody.code === "0123",
      "registration verification payload matches the Controller contract: challengeId and normalized code only");
    const registrationGrant = await verifiedResponse.json();
    await page.locator("input[name=firstName]").waitFor();
    verify(registrationGrant.phone === phone && typeof registrationGrant.registrationToken === "string" &&
      await page.locator(".auth-profile-phone").innerText().then(text => text.includes(phone)) &&
      (await context.cookies()).every(cookie => !cookie.name.startsWith("giti_")),
      "verified OTP issues a grant and reveals profile completion without creating an authenticated session");
    await page.locator("input[name=firstName]").fill("مشتری");
    await page.locator("input[name=lastName]").fill("آزمایشی");
    await page.locator("input[name=password]").fill(password);
    await page.locator("input[name=confirmation]").fill("Different.Password@1");
    await page.getByRole("button", { name: "تکمیل ثبت‌نام", exact: true }).click();
    await page.locator("form [role=alert]").waitFor();
    verify(otpRequests === 1, "password confirmation is validated after OTP without issuing another SMS");
    await page.locator("input[name=confirmation]").fill(password);
    await page.getByRole("button", { name: "تکمیل ثبت‌نام", exact: true }).click();
    await page.getByRole("heading", { name: "ثبت‌نام تکمیل شد؛ وارد شوید" }).waitFor();
    verify((await context.cookies()).every(cookie => !cookie.name.startsWith("giti_")), "profile completion does not silently create a passwordless session");
    const replayStatus = await page.evaluate(async token => (await fetch("/api/customer/registration/complete", {
      method: "POST", headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ registrationToken: token, firstName: "Replay", lastName: "Attempt", password: "Replay.Password@1", confirmation: "Replay.Password@1" }),
    })).status, registrationGrant.registrationToken);
    verify(replayStatus === 400 && !page.url().includes(registrationGrant.registrationToken) &&
      await page.evaluate(token => ![...Object.values(localStorage), ...Object.values(sessionStorage)].some(value => value.includes(token)), registrationGrant.registrationToken),
      "a consumed registration grant cannot be replayed and is absent from navigation and browser storage");
    await page.locator(".auth-success").getByRole("button", { name: "ورود", exact: true }).click();
    const passwordInput = page.locator("input[name=password]");
    await passwordInput.waitFor();
    verify(await page.locator("input[name=phone]").inputValue() === phone && await page.locator("input[name=confirmation]").count() === 0,
      "regression: the success action opens a fresh login form without page reload or stale registration fields");
    await passwordInput.fill(password);
    const beforeLogin = loginRequests;
    await page.locator("form").getByRole("button", { name: "ورود", exact: true }).click();
    await page.waitForURL(url => url.pathname === "/account/orders");
    const cookies = await context.cookies();
    verify(loginRequests === beforeLogin + 1 && cookies.filter(cookie => ["giti_customer_access", "giti_customer_refresh"].includes(cookie.name)).every(cookie => cookie.httpOnly) && cookies.some(cookie => cookie.name === "giti_customer_refresh"),
      "the real login button submits once, persists HttpOnly session cookies and restores the original order-list destination");
    await page.getByText("هنوز سفارشی ثبت نکرده‌اید.", { exact: true }).waitFor();
    verify(await page.getByRole("link", { name: "ثبت اولین سفارش", exact: true }).isVisible(), "the authenticated order list has an actionable empty state");

    await control({ failNextOrderList: true }); await page.reload();
    await page.locator(".customer-orders [role=alert]").waitFor();
    await page.getByRole("button", { name: "تلاش دوباره", exact: true }).click();
    await page.getByText("هنوز سفارشی ثبت نکرده‌اید.", { exact: true }).waitFor();
    verify(true, "order-list failures show a retry action and recover without losing authentication");
    await control({ orderListDelayMs: 1200 }); await page.reload({ waitUntil: "domcontentloaded" });
    await page.getByRole("status").filter({ hasText: "در حال دریافت سفارش‌ها" }).waitFor();
    verify(true, "order listing exposes a loading state while the API is pending");
    await page.getByText("هنوز سفارشی ثبت نکرده‌اید.", { exact: true }).waitFor();
    await control({ orderListDelayMs: 0, seedOrdersFor: phone, orderCount: 23 });
    await control({ seedOrdersFor: "09000000826", orderCount: 1 });
    const table = page.getByRole("table", {name: "فهرست سفارش‌ها"});
    const rows = table.locator("tbody tr");
    await page.reload(); await rows.first().waitFor();
    verify(await rows.count() === 10 && (await page.locator(".order-pagination").innerText()).includes("۲۳"), "list paginates all 23 owned orders and excludes foreign orders");
    const headers = await table.getByRole("columnheader").allTextContents();
    verify(["عنوان","کد پیگیری","آخرین تغییر","قیمت پذیرفته‌شده","وضعیت"].every(label=>headers.includes(label)), "order table identifies tracking, date, status and accepted amount");
    const reloadList = page.waitForResponse(r=>r.url().includes("/api/customer/orders?") && r.ok());
    await page.getByRole("toolbar",{name:"ابزار فهرست سفارش‌ها"}).getByRole("button",{name:"تازه‌سازی فهرست سفارش‌ها"}).click();
    await reloadList; await rows.first().waitFor();
    await page.getByRole("button", { name: "صفحه بعد", exact: true }).click();
    await page.locator(".order-pagination").getByText(/صفحه ۲ از/).waitFor();
    await page.getByRole("button", { name: "صفحه بعد", exact: true }).click();
    await page.locator(".order-pagination").getByText(/صفحه ۳ از/).waitFor();
    await rows.first().waitFor();
    verify(await rows.count() === 3 && await page.getByRole("button", { name: "صفحه بعد", exact: true }).isDisabled(), "customers can reach the last page beyond the former twenty-order limit");
    const orders = await page.evaluate(async () => (await fetch("/api/customer/orders?page=1&pageSize=50")).json());
    const confirmed = orders.items.find(order => order.status === "printing"), cancelled = orders.items.find(order => order.status === "cancelled");
    await rows.first().getByRole("link").click();
    await page.getByRole("heading", { name: "جزئیات سفارش", exact: true }).waitFor();
    const activeOrder = page.locator(".customer-order-details:visible");
    await activeOrder.waitFor();
    verify(await activeOrder.locator(".timeline li").count() >= 1 && (await activeOrder.locator(".order-price-totals").innerText()).includes("قیمت برآوردی") && await activeOrder.locator(".quote-row").count() === 0,
      "the order-card action opens owned details with public history and aggregate prices only");
    await page.goto(base + "/account/orders/" + confirmed.id);
    await activeOrder.waitFor();
    verify(await activeOrder.getByRole('list',{name:'مراحل سفارش'}).locator('[aria-current="step"]').innerText().then(text => text === "در صف / حال چاپ") && (await activeOrder.locator(".confirmed-order-price").innerText()).includes("246,800"), "confirmed order details show the current production step and final price");
    await page.goto(base + "/account/orders/" + cancelled.id);
    await activeOrder.locator(".order-cancelled").waitFor();
    verify(await activeOrder.locator(".order-cancelled").isVisible() && await activeOrder.getByRole("list",{name:"مراحل سفارش"}).count() === 0, "cancelled orders have a dedicated state without inventing a production transition");
    await page.getByRole("link", { name: /بازگشت به سفارش‌های من/ }).click();
    await page.waitForURL(url => url.pathname === "/account/orders");
    verify(true, "order details provide a clear route back to all customer orders");

    await page.goto(base + "/calculator");
    const quoteResponse = page.waitForResponse(response => response.request().method() === "POST" && response.url().endsWith("/api/quote"));
    await page.getByRole("button", { name: "محاسبه هزینه", exact: true }).click();
    const quote = await (await quoteResponse).json();
    await page.getByRole("link", { name: "ارسال درخواست چاپ" }).waitFor();
    verify(await page.getByRole("definition").filter({hasText:formatToman(quote.total)}).last().innerText() === formatToman(quote.total) && await page.locator(".result-values .quote-row,.finishing-breakdown").count() === 0,
      "calculator shows the unchanged authoritative total without internal cost rows");
    await page.getByRole("link", { name: "ارسال درخواست چاپ" }).click();
    await page.waitForURL(url => url.pathname === "/order");
    await page.getByRole("complementary").getByText(formatToman(quote.total), {exact:true}).waitFor();
    verify(await page.locator(".quote-panel .quote-row").count() === 0 && await page.locator(".quote-panel .quote-total strong").innerText() === formatToman(quote.total), "the order form preserves the total and hides cost breakdowns consistently");
    await page.locator("input[name=title]").fill("سفارش آزمون تجربه مشتری");
    await page.getByLabel(/فایل متن و جلد را انتخاب کنید/).setInputFiles({name:"book.pdf",mimeType:"application/pdf",buffer:pdfFixture()});
    await page.getByRole("checkbox",{name:/شرایط ثبت سفارش/}).check();
    const creation = page.waitForResponse(response => response.request().method() === "POST" && response.url().endsWith("/api/orders"));
    await page.getByRole("button", { name: "ارسال درخواست چاپ", exact: true }).click();
    const receiptResponse = await creation, receipt = await receiptResponse.json();
    assert.equal(receiptResponse.status(), 201);
    await page.getByRole("heading", { name: "درخواست شما ثبت شد.", exact: true }).waitFor();
    verify(receipt.estimate === quote.total && await page.locator("main").getByRole("link", { name: "پیگیری سفارش", exact: true }).getAttribute("href") === "/account/orders/" + receipt.id,
      "order confirmation keeps the backend total and links directly to authenticated tracking");
    await adminPage.goto(base + "/admin/orders/" + receipt.id);
    await adminPage.locator(".admin-cost-breakdown").waitFor();
    verify(await adminPage.locator(".admin-cost-breakdown .quote-row").count() > 0, "administrators can still inspect the stored calculation breakdown");

    anonymous = await browser.newContext({ viewport: { width: 390, height: 844 } });
    await anonymous.route("**/*", route => route.request().url().startsWith(base) ? route.continue() : route.abort());
    const publicPage = await anonymous.newPage(); publicPage.on("pageerror", error => browserErrors.push(error.message));
    await publicPage.goto(base + "/");
    let toggle = publicPage.getByRole("button", { name: "باز کردن منو", exact: true });
    await toggle.click(); await publicPage.keyboard.press("Escape");
    verify(await toggle.getAttribute("aria-expanded") === "false" && await toggle.evaluate(element => element === document.activeElement), "the mobile menu supports Escape and restores focus to its toggle");
    await toggle.click();
    const mobileNav = publicPage.getByRole("navigation", { name: "منوی اصلی", exact: true });
    verify(await mobileNav.getByRole("link", { name: "پیگیری سفارش", exact: true }).count() === 0, "legacy tracking is absent from mobile navigation");
    await mobileNav.locator('a[href="/services"]').click();
    await publicPage.waitForURL(url => url.pathname === "/services");
    toggle = publicPage.getByRole("button", { name: "باز کردن منو", exact: true });
    verify(await toggle.getAttribute("aria-expanded") === "false", "mobile navigation closes after services navigation");
    await publicPage.goto(base + "/track");
    verify(new URL(publicPage.url()).searchParams.get("next")==="/track" && await publicPage.locator("input[name=code]").count()===0, "anonymous tracking cannot retrieve customer history");
    await page.goto(base+"/track");
    await page.locator("input[name=code]").fill(receipt.code.toLowerCase());
    await page.getByRole("button",{name:"مشاهده وضعیت سفارش",exact:true}).click();
    await page.locator(".tracking-result").waitFor();
    verify(await page.locator(".timeline li").count()>=1 && await page.locator("input[name=phone]").count()===0, "the owner can track the immutable code without re-entering their phone");
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto(base + "/account/orders/" + confirmed.id);
    verify(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), "customer order details fit a mobile viewport without horizontal scrolling");
    await page.screenshot({ path: ".qa/customer-order-mobile.png", fullPage: true });
    await page.goto(base + "/account/orders");
    await rows.first().waitFor();
    verify(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), "customer order cards and pagination fit mobile widths");
    await page.screenshot({ path: ".qa/customer-orders-mobile.png", fullPage: true });
    await publicPage.goto(base + "/customer?mode=register&next=/calculator");
    await publicPage.locator("input[name=phone]").fill("09188888886");
    await publicPage.getByRole("button", { name: "ادامه ثبت‌نام", exact: true }).click();
    await publicPage.getByLabel("کد تأیید", { exact: true }).waitFor();
    await control({ advanceSeconds: 120 });
    await publicPage.getByLabel("کد تأیید", { exact: true }).fill("0123");
    await publicPage.getByRole("button", { name: "تأیید کد و ادامه", exact: true }).click();
    await publicPage.locator("form [role=alert]").waitFor();
    verify(await publicPage.locator("input[name=firstName],input[name=password]").count() === 0,
      "an expired OTP cannot reveal profile/password fields in the actual browser flow");
    await publicPage.getByRole("button", { name: "اصلاح اطلاعات", exact: true }).click();
    await publicPage.locator("input[name=phone]").fill("09188888885");
    await publicPage.getByRole("button", { name: "ادامه ثبت‌نام", exact: true }).click();
    await publicPage.getByLabel("کد تأیید", { exact: true }).fill("0123");
    await publicPage.getByRole("button", { name: "تأیید کد و ادامه", exact: true }).click();
    await publicPage.locator("input[name=firstName]").fill("مشتری");
    await publicPage.locator("input[name=lastName]").fill("آزمایشی");
    await publicPage.locator("input[name=password]").fill(password);
    await publicPage.locator("input[name=confirmation]").fill(password);
    await control({ advanceSeconds: 300 });
    await publicPage.getByRole("button", { name: "تکمیل ثبت‌نام", exact: true }).click();
    await publicPage.locator("form [role=alert]").waitFor();
    const reverify = publicPage.getByRole("button", { name: "تأیید دوباره شماره موبایل", exact: true });
    verify(await reverify.isVisible() && await publicPage.locator(".auth-success").count() === 0,
      "expired registration grants show an actionable reverification path instead of completing an account");
    await reverify.click();
    verify(await publicPage.locator("input[name=phone]").inputValue() === "09188888885" &&
      await publicPage.locator("input[name=firstName],input[name=password]").count() === 0,
      "reverification discards the expired profile/grant while retaining the mobile number");
    writeFileSync(".qa/customer-experience-ui-result.json", JSON.stringify({ checks, database: "memory only", realSms: false, published: false }, null, 2));
  } catch (error) {
    await page.screenshot({ path: ".qa/customer-experience-failure.png", fullPage: true }).catch(() => {});
    throw error;
  } finally { await anonymous?.close(); await context.close(); }
}

