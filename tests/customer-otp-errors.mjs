import assert from "node:assert/strict";
import { customerOtpErrorMessage, recoveryNeedsRestart } from "../frontend/lib/customer-otp-errors.ts";

let checks = 0;
const check = (condition, name) => { assert.ok(condition, name); checks++; console.log("PASS " + name); };
const cases = [
  ["CUSTOMER_ACCOUNT_NOT_FOUND", "حساب کاربری با این شماره موبایل پیدا نشد. برای ایجاد حساب جدید، لطفاً ثبت‌نام کنید."],
  ["CUSTOMER_RECOVERY_UNAVAILABLE", "اطلاعات این حساب کامل نیست. ابتدا با رمز عبور وارد شوید و مشخصات حساب را تکمیل کنید."],
  ["CUSTOMER_PHONE_UNVERIFIED", "شماره موبایل این حساب هنوز تأیید نشده است. با رمز فعلی وارد شوید و شماره را تأیید کنید یا با پشتیبانی تماس بگیرید."],
  ["OTP_INCORRECT", "کد تأیید واردشده صحیح نیست. لطفاً دوباره تلاش کنید."],
  ["OTP_EXPIRED", "مهلت کد تأیید تمام شده است. لطفاً کد جدید بگیرید."],
  ["OTP_CHALLENGE_MISSING", "درخواست تأیید پیدا نشد. شماره موبایل را دوباره وارد کنید و کد جدید بگیرید."],
  ["OTP_CONSUMED", "این کد قبلاً استفاده شده است. لطفاً کد جدید بگیرید."],
  ["OTP_ATTEMPT_LIMIT", "تعداد تلاش‌های مجاز برای این کد تمام شده است. لطفاً کد جدید بگیرید."],
  ["OTP_CHALLENGE_INVALID", "این درخواست تأیید دیگر معتبر نیست. لطفاً کد جدید بگیرید."],
  ["SMS_DELIVERY_FAILED", "ارسال پیامک انجام نشد. کمی بعد دوباره تلاش کنید."],
];
for (const [code, message] of cases) {
  check(customerOtpErrorMessage(code, "Unrelated backend text") === message, code + " selects the correct Persian copy by code");
  check(recoveryNeedsRestart(code) === (code !== "OTP_INCORRECT"), code + " has the appropriate recovery-state behavior");
}
for (const code of [null, "UNKNOWN", "__proto__", "constructor"])
  check(customerOtpErrorMessage(code, cases[4][1]) === cases[4][1] && !recoveryNeedsRestart(code), "unknown codes never infer an error type or state transition from message text");
console.log(`${checks} customer OTP error checks passed.`);
