export const orderServices = {
  "book-printing":"چاپ کتاب", "publishing":"چاپ و خدمات نشر", "cover-design":"چاپ و طراحی جلد",
  "page-layout":"چاپ و صفحه‌آرایی", "catalog-printing":"کاتالوگ و نشریه",
} as const;
export const optionalOrderPdfs = Object.fromEntries(Object.keys(orderServices).map(key=>[key,false]));

export function orderServiceLabel(code?: string) {
  return code && Object.hasOwn(orderServices, code)
    ? orderServices[code as keyof typeof orderServices] : "خدمت ثبت‌شده ناشناخته است";
}
