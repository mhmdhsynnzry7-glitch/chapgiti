import {
  bookBindings,
  finishingServices,
  type PrintOptions,
  type BookDetails,
} from "./book-options";

/** Mirrors the existing authoritative PrintPricing validation rules. */
export function finishingUnavailable(
  key: keyof typeof finishingServices,
  details: BookDetails,
  options?: PrintOptions,
): string {
  const services = options?.finishingServices ?? finishingServices,
    bindings = options?.bookBindings ?? bookBindings,
    cover = options?.coverPrints[details.coverPrint],
    coating = options?.coverCoatings[details.coating];
  if (details.printOnly)
    return "برای این خدمت، گزینهٔ «فقط چاپ، بدون صحافی» را غیرفعال کنید.";
  if (typeof cover === "object" ? cover.none : details.coverPrint === "none")
    return "برای این خدمت، جلد انتخاب کنید.";
  const service = services[key];
  if (!service) return "این خدمت در دسترس نیست.";
  if (service.hard && !bindings[details.bindingStyle]?.hard)
    return "این خدمت به صحافی جلد سخت نیاز دارد.";
  if (
    service.matte &&
    (typeof cover === "object"
      ? cover.plastic
      : details.coverPrint === "plastic")
  )
    return "این خدمت روی جلد طلق قابل محاسبه نیست؛ جلد کاغذی با سلفون مات انتخاب کنید.";
  if (
    service.matte &&
    !(typeof coating === "object" ? coating.matte : details.coating === "matte")
  )
    return "برای این خدمت، پوشش جلد را «سلفون مات» انتخاب کنید.";
  return "";
}
