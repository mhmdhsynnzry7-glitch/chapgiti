import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { validNewPassword, passwordError, PASSWORD_GUIDANCE, PASSWORD_PATTERN } from "../frontend/lib/password-policy.ts";
const cases = JSON.parse(readFileSync(new URL("./password-policy-cases.json", import.meta.url), "utf8"));
let checks = 0;
for (const item of cases) {
  assert.equal(validNewPassword(item.value), item.valid, item.name);
  assert.equal(new RegExp("^(?:" + PASSWORD_PATTERN + ")$", "v").test(item.value), item.valid, "HTML pattern: " + item.name);
  checks += 2;
}
assert.equal(passwordError("A!1234", "A!1234"), "");
assert.equal(passwordError("A!1234", "B!1234"), "تکرار رمز عبور یکسان نیست.");
assert.equal(passwordError("123456", "123456"), PASSWORD_GUIDANCE);
checks += 3;
console.log(checks + " frontend password-policy checks passed against the shared backend fixtures; no network was used.");
