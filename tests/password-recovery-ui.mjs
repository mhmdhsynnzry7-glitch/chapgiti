import assert from "node:assert/strict";
import { passwordLabelChecks } from "./registration-form-ui.mjs";

export async function passwordRecoveryChecks({ browser, base, control, check, browserErrors }) {
  await control({ advanceSeconds: 901, smsAvailable: true });
  const context = await browser.newContext();
  await context.route("**/*", route => route.request().url().startsWith(base) ? route.continue() : route.abort());
  const page = await context.newPage();
  page.on("pageerror", error => browserErrors.push(error.message));
  const unknown = "09166666667", customer = "09100000001";
  const missingMessage = "حساب کاربری با این شماره موبایل پیدا نشد. برای ایجاد حساب جدید، لطفاً ثبت‌نام کنید.";
  const incorrectMessage = "کد تأیید واردشده صحیح نیست. لطفاً دوباره تلاش کنید.";
  const requestPattern = "**/api/customer/otp/request", verifyPattern = "**/api/customer/password/verify";
  const mobile = () => page.locator("input[name=phone]");
  const requestButton = () => page.getByRole("button", { name: "دریافت کد تأیید", exact: true });
  const verifyButton = () => page.getByRole("button", { name: "تأیید کد و ادامه", exact: true });
  async function open(phone) {
    await page.goto(base + "/customer/forgot-password?next=/calculator");
    await mobile().fill(phone);
  }
  async function submitCode(code = "0123") {
    await page.getByLabel("کد تأیید", { exact: true }).fill(code);
    await verifyButton().click();
  }
  async function expectError(message) {
    await page.waitForFunction(expected => document.querySelector("form [role=alert]")?.textContent === expected, message);
  }
  try {
    const before = await control({});
    await open("۰۹۱۶۶۶۶۶۶۶۷");
    const missingResponse = page.waitForResponse(response => response.url().endsWith("/api/customer/otp/request") && response.request().method() === "POST");
    await requestButton().click();
    const missing = await missingResponse;
    await expectError(missingMessage);
    const after = await control({});
    check(missing.status() === 404 && (await missing.json()).code === "CUSTOMER_ACCOUNT_NOT_FOUND" &&
      before.smsSends === after.smsSends && before.otpChallenges === after.otpChallenges && before.accounts === after.accounts,
      "unknown recovery mobile returns a structured account-not-found error without SMS, OTP state or account writes");
    check(await mobile().inputValue() === unknown && await page.locator("input[name=code],#otp-validity,input[name=password]").count() === 0,
      "unknown recovery remains on mobile entry without an OTP countdown or password-reset form");
    await page.route(requestPattern, route => route.fulfill({ status: 404, contentType: "application/json",
      body: JSON.stringify({ code: "CUSTOMER_ACCOUNT_NOT_FOUND", error: "Misleading invalid or expired OTP text" }) }));
    await requestButton().click();
    await expectError(missingMessage);
    check(true, "recovery account-not-found copy is selected by code despite unrelated backend text");
    await page.unroute(requestPattern);
    await page.locator("form [role=alert]").getByRole("link", { name: "ثبت‌نام کنید", exact: true }).click();
    check(await page.getByRole("heading", { name: "ثبت‌نام با شماره موبایل", exact: true }).isVisible() &&
      await mobile().inputValue() === unknown && !page.url().includes(unknown),
      "the recovery registration link retains the normalized mobile in memory without navigation PII");
    const registrationRequest = page.waitForRequest(request => request.url().endsWith("/api/customer/otp/request") && request.method() === "POST");
    await page.getByRole("button", { name: "ادامه ثبت‌نام", exact: true }).click();
    await page.getByLabel("کد تأیید", { exact: true }).waitFor();
    check((await registrationRequest).postDataJSON().purpose === "register" && await page.locator("input[name=firstName],input[name=password]").count() === 0,
      "registration opened from recovery uses the approved phone-first registration purpose, not reset");

    await control({ smsAvailable: false }); await open(customer); await requestButton().click();
    await expectError("ارسال پیامک انجام نشد. کمی بعد دوباره تلاش کنید.");
    check(await mobile().isVisible() && await page.locator("#otp-validity,input[name=code]").count() === 0,
      "SMS integration failure shows a delivery error without opening an OTP screen");
    await control({ smsAvailable: true });
    await mobile().fill("٠٩١٠٠٠٠٠٠٠١");
    const response = page.waitForResponse(item => item.url().endsWith("/api/customer/otp/request") && item.request().method() === "POST");
    await requestButton().click();
    const challenge = await (await response).json();
    await submitCode("9999"); await expectError(incorrectMessage);
    check(await page.getByLabel("کد تأیید", { exact: true }).isVisible() && await page.locator("input[name=password]").count() === 0,
      "an actual incorrect recovery OTP shows its distinct message and retains the verification form");

    const terminalCases = [
      ["OTP_EXPIRED", "مهلت کد تأیید تمام شده است. لطفاً کد جدید بگیرید."],
      ["OTP_CHALLENGE_MISSING", "درخواست تأیید پیدا نشد. شماره موبایل را دوباره وارد کنید و کد جدید بگیرید."],
      ["OTP_CONSUMED", "این کد قبلاً استفاده شده است. لطفاً کد جدید بگیرید."],
      ["OTP_ATTEMPT_LIMIT", "تعداد تلاش‌های مجاز برای این کد تمام شده است. لطفاً کد جدید بگیرید."],
      ["OTP_CHALLENGE_INVALID", "این درخواست تأیید دیگر معتبر نیست. لطفاً کد جدید بگیرید."],
    ];
    for (const [code, message] of terminalCases) {
      await open(customer);
      await page.route(requestPattern, route => route.fulfill({ status: 200, contentType: "application/json",
        body: JSON.stringify({ ...challenge, expiresAt: Date.now() + 120000 }) }));
      await requestButton().click();
      await page.route(verifyPattern, route => route.fulfill({ status: 400, contentType: "application/json",
        body: JSON.stringify({ code, error: "The same misleading fallback for every mocked error" }) }));
      await submitCode(); await expectError(message);
      check(await mobile().inputValue() === customer && await page.locator("#otp-validity,input[name=code],input[name=password]").count() === 0,
        code + " clears stale recovery state, preserves the mobile and uses its own Persian message");
      await page.unroute(requestPattern); await page.unroute(verifyPattern);
    }
    await open(customer);
    await page.route(requestPattern, route => route.fulfill({ status: 200, contentType: "application/json", body: "{}" }));
    await requestButton().click(); await page.locator("form [role=alert]").waitFor();
    check(await mobile().isVisible() && await page.locator("#otp-validity,input[name=code]").count() === 0,
      "a malformed successful response cannot start OTP verification or its countdown");
    await page.unroute(requestPattern);

    const historical = await control({ seedStaleRecovery: unknown });
    await open(unknown);
    await page.route(requestPattern, route => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(historical.staleRecovery) }));
    await requestButton().click();
    const staleVerification = page.waitForResponse(item => item.url().endsWith("/api/customer/password/verify") && item.request().method() === "POST");
    await submitCode("9999");
    assert.equal((await staleVerification).status(), 404);
    await expectError(missingMessage);
    check(await mobile().inputValue() === unknown && await page.locator("input[name=code],input[name=password],#otp-validity").count() === 0,
      "the real verification API rejects a historical unknown-account challenge and returns the browser to mobile entry");
    await page.unroute(requestPattern);

    await control({ advanceSeconds: 61 }); await open(customer); await requestButton().click();
    await submitCode("۰۱۲۳");
    await page.getByRole("heading", { name: "رمز جدید را انتخاب کن", exact: true }).waitFor();
    await passwordLabelChecks(page, check, "change");
    await page.locator("input[name=password]").fill("Recovery.Browser@1");
    await page.locator("input[name=confirmation]").fill("Recovery.Browser@1");
    await page.getByRole("button", { name: "ثبت رمز جدید", exact: true }).click();
    await page.getByRole("heading", { name: "رمز عبور تغییر کرد", exact: true }).waitFor();
    check((await context.cookies()).every(cookie => !cookie.name.startsWith("giti_")), "successful isolated recovery does not create a passwordless session");
    await page.locator(".auth-success").getByRole("link", { name: "ورود", exact: true }).click();
    await mobile().fill(customer); await page.locator("input[name=password]").fill("Recovery.Browser@1");
    await page.locator("form").getByRole("button", { name: "ورود", exact: true }).click();
    await page.waitForURL(url => url.pathname === "/calculator");
    check(true, "eligible customers complete the real mocked-SMS recovery flow and sign in with the new isolated password");
  } catch (error) {
    await page.screenshot({ path: ".qa/password-recovery-failure.png", fullPage: true }).catch(() => {});
    throw error;
  } finally { await context.close(); }
}
