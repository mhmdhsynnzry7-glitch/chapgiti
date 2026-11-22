import { draftPrintSpecSchema } from "./print-spec";
import type { PrintSpec } from "./pricing";

const key = "chap-print-spec-v1";
type StoragePort = Pick<Storage, "getItem" | "setItem" | "removeItem">;
export function storeRequestSpec(
  storage: StoragePort,
  spec: PrintSpec,
  now = Date.now(),
) {
  // Zod strips unknown properties. Never retain title, notes, contacts, files,
  // passwords, OTPs or registration grants in this authentication-return draft.
  storage.setItem(
    key,
    JSON.stringify({
      version: 1,
      spec: draftPrintSpecSchema.parse(spec),
      savedAt: now,
    }),
  );
}
export function readRequestSpec(
  storage: StoragePort,
  now = Date.now(),
): PrintSpec | null {
  try {
    const value = JSON.parse(storage.getItem(key) || "null");
    if (
      value?.version !== 1 ||
      !Number.isFinite(value.savedAt) ||
      value.savedAt > now ||
      now - value.savedAt > 2 * 60 * 60 * 1000
    ) {
      return null;
    }
    const parsed = draftPrintSpecSchema.safeParse(value.spec);
    if (parsed.success) return parsed.data;
  } catch {
    /* Restricted storage or invalid old draft must not block the form. */
  }
  return null;
}
export function clearRequestSpec(storage: StoragePort) {
  storage.removeItem(key);
}
