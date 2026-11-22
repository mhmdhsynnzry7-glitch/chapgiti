export function normalizedAmountInput(value: string): string {
  return value.replace(/[۰-۹]/g, d => String("۰۱۲۳۴۵۶۷۸۹".indexOf(d)))
    .replace(/[٠-٩]/g, d => String("٠١٢٣٤٥٦٧٨٩".indexOf(d))).replace(/[,٬\s]/g, "");
}
export function parseApprovedAmount(value: string): number | null {
  const normalized = normalizedAmountInput(value);
  if (!/^\d{1,12}$/.test(normalized)) return null;
  const amount = Number(normalized);
  return Number.isSafeInteger(amount) && amount <= 100000000000 ? amount : null;
}
