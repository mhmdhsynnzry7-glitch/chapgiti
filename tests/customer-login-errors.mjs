import assert from "node:assert/strict";
import { CUSTOMER_LOGIN_ERRORS, customerLoginErrorMessage } from "../frontend/lib/customer-login-errors.ts";

let checks = 0;
const check = (condition, name) => { assert.ok(condition, name); checks++; console.log("PASS " + name); };
const missing = "حساب کاربری با این شماره موبایل پیدا نشد. لطفاً ابتدا ثبت‌نام کنید.";
const incorrect = "رمز عبور واردشده صحیح نیست. لطفاً دوباره تلاش کنید.";
check(customerLoginErrorMessage(CUSTOMER_LOGIN_ERRORS.accountNotFound, "unrelated backend text") === missing,
  "account-not-found codes map to the exact Persian registration guidance");
check(customerLoginErrorMessage(CUSTOMER_LOGIN_ERRORS.incorrectPassword, missing) === incorrect,
  "incorrect-password codes map independently of conflicting response text");
for (const code of [null, "", "UNKNOWN_ERROR", "__proto__", "constructor"])
  check(customerLoginErrorMessage(code, "Existing validation or service error") === "Existing validation or service error",
    "unknown or missing codes preserve unrelated error handling");
check(customerLoginErrorMessage(null, missing) === missing && customerLoginErrorMessage(null, incorrect) === incorrect,
  "legacy messages are preserved without inferring their error type");
console.log(`${checks} customer login error checks passed.`);
