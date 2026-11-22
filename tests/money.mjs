import assert from "node:assert/strict";
import { formatToman } from "../frontend/lib/money.ts";
let count = 0;
for (const [value, expected] of [
  [200000, "200,000"], [1250000, "1,250,000"], [0, "0"], [-0, "0"],
  [-1250000, "-1,250,000"], [999999999999, "999,999,999,999"],
  [9007199254740993123n, "9,007,199,254,740,993,123"], [1234.25, "1,234.25"],
]) { assert.equal(formatToman(value), `\u2066${expected}\u2069 تومان`); count++; }
for (const value of [null, undefined, "", NaN, Infinity, -Infinity]) { assert.equal(formatToman(value), "—"); count++; }
console.log(`${count} money checks passed; existing toman amounts are not converted or rounded.`);
