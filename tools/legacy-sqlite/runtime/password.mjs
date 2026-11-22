import { randomBytes, scrypt, timingSafeEqual } from "node:crypto";
import { promisify } from "node:util";
const derive = promisify(scrypt);
export async function hashPassword(password) {
  if (typeof password !== "string" || password.length < 12 || password.length > 128)
    throw new Error("Password must be between 12 and 128 characters.");
  const salt = randomBytes(16).toString("hex");
  const key = await derive(password, salt, 64, { N: 32768, r: 8, p: 1, maxmem: 64 * 1024 * 1024 });
  return "scrypt$" + salt + "$" + key.toString("hex");
}
export async function verifyPassword(password, encoded) {
  if (typeof password !== "string" || password.length > 128) return false;
  const parts = encoded.split("$");
  if (parts.length !== 3 || parts[0] !== "scrypt") return false;
  const key = await derive(password, parts[1], 64, { N: 32768, r: 8, p: 1, maxmem: 64 * 1024 * 1024 });
  const expected = Buffer.from(parts[2], "hex");
  return expected.length === key.length && timingSafeEqual(key, expected);
}
