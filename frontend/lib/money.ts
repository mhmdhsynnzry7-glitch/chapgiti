const amounts = new Intl.NumberFormat("en-US", { maximumFractionDigits: 20 });

export const groupedToman = (value: number | bigint) => amounts.format(value);

/** Amounts already use toman. Missing values are unknown, never silently zero. */
export function formatToman(value: number | bigint | null | undefined | ""): string {
  if (value == null || value === "" || (typeof value === "number" && !Number.isFinite(value))) return "—";
  const amount = typeof value === "number" && Object.is(value, -0) ? 0 : value;
  // Isolate the Latin number (especially its minus sign) inside Persian text.
  return "\u2066" + groupedToman(amount) + "\u2069 تومان";
}
