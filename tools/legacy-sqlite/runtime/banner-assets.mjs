import { mkdir, writeFile, readFile, unlink } from "node:fs/promises";
import path from "node:path";
import { dataDirectory } from "./database.mjs";
const checked = id => { if (!/^[a-f0-9-]{36}$/.test(id)) throw new Error("Invalid asset ID"); return path.join(dataDirectory(), "banner-assets", id); };
export function imageMime(bytes) {
  if (bytes.length >= 8 && Buffer.from(bytes.subarray(0, 8)).equals(Buffer.from([137,80,78,71,13,10,26,10]))) return "image/png";
  if (bytes.length >= 3 && bytes[0] === 255 && bytes[1] === 216 && bytes[2] === 255) return "image/jpeg";
  if (bytes.length >= 12 && Buffer.from(bytes.subarray(0,4)).toString() === "RIFF" && Buffer.from(bytes.subarray(8,12)).toString() === "WEBP") return "image/webp";
  return null;
}
export async function putBannerAsset(id, bytes) {
  const target = checked(id); await mkdir(path.dirname(target), {recursive: true}); await writeFile(target, bytes, {flag: "wx"});
}
export async function getBannerAsset(id) { try { return await readFile(checked(id)); } catch (error) { if (error.code === "ENOENT") return null; throw error; } }
export async function deleteBannerAsset(id) { await unlink(checked(id)).catch(error => { if (error.code !== "ENOENT") throw error; }); }
