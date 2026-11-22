import { mkdir, writeFile, readFile, unlink } from "node:fs/promises";
import path from "node:path";
import { dataDirectory } from "./database.mjs";

function filePath(key) {
  if (!/^[a-f0-9-]{36}\/[a-f0-9-]{36}\.pdf$/i.test(key)) throw new Error("Invalid object key");
  return path.join(dataDirectory(), "uploads", ...key.split("/"));
}
export const bucket = {
  async put(key, bytes) {
    const file = filePath(key);
    await mkdir(path.dirname(file), { recursive: true });
    await writeFile(file, bytes, { flag: "wx" });
  },
  async get(key) {
    try { return { body: new Uint8Array(await readFile(filePath(key))) }; }
    catch (e) { if (e.code === "ENOENT") return null; throw e; }
  },
  async delete(key) {
    try { await unlink(filePath(key)); }
    catch (e) { if (e.code !== "ENOENT") throw e; }
  }
};
