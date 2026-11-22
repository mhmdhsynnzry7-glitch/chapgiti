import { publicBootstrap } from "./backend";
export type Banner = {
  id: string; title: string; subtitle: string; eyebrow: string; image: string;
  link: string; label: string; theme: "navy" | "orange" | "sage"; kind: "promotion" | "portfolio"; active: boolean; demo?: boolean;
};
export const defaultBanners: Banner[] = [
  { id: "print", eyebrow: "چاپ دیجیتال و افست", title: "کتابت را همین‌جا\nبه چاپ برسان.", subtitle: "از یک نسخه تا تیراژ دلخواه؛ قطع، کاغذ و صحافی را انتخاب کن و هزینه را پیش از سفارش ببین.", image: "/banners/print.svg", link: "/calculator", label: "محاسبه هزینه چاپ", theme: "navy", kind: "promotion", active: true },
  { id: "binding", eyebrow: "جزئیات کوچک، تفاوت‌های بزرگ", title: "چاپی که\nدیده می‌شود.", subtitle: "جلد سخت، طلاکوب، لبه‌رنگی و روکش‌های متنوع؛ انتخاب‌های بیشتری برای کتاب شما.", image: "/banners/binding.svg", link: "/calculator", label: "انتخاب خدمات تکمیلی", theme: "orange", kind: "promotion", active: true },
  { id: "portfolio", eyebrow: "گالری چاپ و طراحی", title: "برای ایده‌ات\nالهام بگیر.", subtitle: "طرح‌های نمایشی جلد و صحافی را ببین و ترکیب مناسب کتابت را انتخاب کن.", image: "/banners/portfolio.svg", link: "/portfolio", label: "مشاهده گالری", theme: "sage", kind: "portfolio", active: true },
];
export async function allBanners(): Promise<Banner[]> { return (await publicBootstrap()).banners; }
export async function publicBanners() { return (await publicBootstrap()).banners; }
