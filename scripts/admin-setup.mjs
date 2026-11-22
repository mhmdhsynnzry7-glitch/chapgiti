import {existsSync as releaseFileExists} from "node:fs";
import { emitKeypressEvents } from "node:readline";
import { parseArgs } from "node:util";
import { environment,buildBackend,cli } from "./dotnet-runtime.mjs";

const { values, positionals } = parseArgs({
  options: { username: { type: "string" }, "password-stdin": { type: "boolean", default: false } },
  allowPositionals: true,
});
if (positionals.length !== 1) throw new Error("Provide the actual administrator email explicitly. Existing accounts are never selected by a default email.");
if (values.username !== undefined && values.username !== "admin") throw new Error("The administrator username is fixed as admin.");
const email = positionals[0].trim().toLowerCase();
if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) throw new Error("Invalid email");
async function secret(prompt) {
  if (!process.stdin.isTTY) throw new Error("Run admin:setup from an interactive terminal.");
  process.stdout.write(prompt);
  emitKeypressEvents(process.stdin); process.stdin.setRawMode(true); process.stdin.resume();
  return await new Promise((resolve) => {
    let value = "";
    function finish() { process.stdin.off("keypress", listener); process.stdin.setRawMode(false); process.stdin.pause(); process.stdout.write("\n"); }
    function listener(text, key) {
      if (key?.ctrl && key.name === "c") { finish(); process.exit(1); }
      if (key?.name === "return") { finish(); resolve(value); return; }
      if (key?.name === "backspace") { if (value) { value=value.slice(0,-1); process.stdout.write("\b \b"); } }
      else if (text && !key?.ctrl && !key?.meta && !/[\x00-\x1f\x7f]/.test(text)) { value += text; process.stdout.write("*".repeat(text.length)); }
    }
    process.stdin.on("keypress", listener);
  });
}
let password;
if (values["password-stdin"]) {
  let input = "";
  for await (const chunk of process.stdin) {
    input += chunk.toString("utf8");
    if (input.length > 1024) throw new Error("Password input is too long.");
  }
  password = input.replace(/\r?\n$/, "");
} else {
  password = await secret("New admin password (6-128 characters, A-Z and ASCII punctuation): ");
  const confirmation = await secret("Confirm password: ");
  if (password !== confirmation) throw new Error("Passwords did not match.");
}
if (password.length < 6 || password.length > 128 || !/[A-Z]/.test(password) || !/[\x21-\x2f\x3a-\x40\x5b-\x60\x7b-\x7e]/.test(password))
  throw new Error("Password must be 6-128 characters and include A-Z and ASCII punctuation.");
const development = process.env.NODE_ENV !== "production" && !releaseFileExists("RELEASE-MANIFEST.json");
if (development) await buildBackend(true);
await cli(["--admin-setup",email,values.username || "admin"],environment(development),{development,input:password+"\n"});
console.log("Admin credentials saved in SQL Server; existing sessions revoked.");
