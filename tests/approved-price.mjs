import assert from "node:assert/strict";
import { normalizedAmountInput, parseApprovedAmount } from "../frontend/lib/amount-input.ts";
let checks = 0;
for (const [text,value] of [["440,000",440000],["۴۴۰٬۰۰۰",440000],["٤٤٠٬٠٠٠",440000],[" 0 ",0],["100,000,000,000",100000000000]]) {
  assert.equal(parseApprovedAmount(text),value); checks++;
}
for (const text of ["", "1.5", "-1", "1e3", "abc", "100000000001", "۴۴۰٫۵"]) { assert.equal(parseApprovedAmount(text), null); checks++; }
assert.equal(normalizedAmountInput("۱۲٣,۴٥٦"),"123456"); checks++;
console.log(`${checks} approved-price checks passed.`);
