import assert from "node:assert/strict";
import { paginationState } from "../frontend/lib/pagination.ts";

let checks = 0;
for (const [page, size, total, expected] of [
  [1, 20, 0, [1, 1, 0, 1, 1]],
  [1, 20, 20, [1, 1, 20, 1, 1]],
  [1, 20, 21, [1, 2, 21, 1, 2]],
  [2, 20, 21, [2, 2, 21, 1, 2]],
  [2, 10, 23, [2, 3, 23, 1, 3]],
  [3, 10, 23, [3, 3, 23, 2, 3]],
  [3, 10, 7, [1, 1, 7, 1, 1]], // the result set shrank
  [0, 20, 25, [1, 2, 25, 1, 2]],
  [-10, 20, 25, [1, 2, 25, 1, 2]],
  [99, 20, 25, [2, 2, 25, 1, 2]],
  [NaN, 20, 25, [1, 2, 25, 1, 2]],
  [Infinity, 20, 25, [1, 2, 25, 1, 2]],
  [1, 0, 25, [1, 2, 25, 1, 2]],
  [1, -1, 25, [1, 2, 25, 1, 2]],
  [1, NaN, 25, [1, 2, 25, 1, 2]],
  [1.9, 10.9, 23.9, [1, 3, 23, 1, 2]],
  [1, 20, -1, [1, 1, 0, 1, 1]],
  [1, 20, NaN, [1, 1, 0, 1, 1]],
  [1, 20, Infinity, [1, 1, 0, 1, 1]],
]) {
  assert.deepEqual(Object.values(paginationState(page, size, total)), expected);
  checks++;
}
console.log(`${checks} pagination boundary checks passed; no database or network used.`);
