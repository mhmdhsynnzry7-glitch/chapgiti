import assert from "node:assert/strict";
import test from "node:test";
import { browserPort } from "../browser-port.mjs";

test("browser server skips OS-assigned ports rejected by Next.js", async () => {
  const ports = [6667, 10080, 52000];
  assert.equal(await browserPort(() => ports.shift()), 52000);
  assert.equal(ports.length, 0);
});

test("browser server reports exhaustion when every port is reserved", async () => {
  await assert.rejects(() => browserPort(() => 6667), /loopback port allowed by Next/);
});
