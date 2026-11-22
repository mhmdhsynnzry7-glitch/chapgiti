import { publicBootstrap } from "./backend";
import type { Banner } from "@/lib/banners";
export type PortfolioItem = Banner & { demo: boolean };
export const defaultPortfolio: PortfolioItem[] = [
  { id:"hardcover",title:"روایت ماندگار",subtitle:"جلد سخت با روکش مات و جزئیات طلایی؛ ترکیبی برای کتاب‌های نفیس و یادگاری.",eyebrow:"جلد سخت و طلاکوب",image:"/portfolio/hardcover.svg",link:"/calculator",label:"محاسبه چاپ مشابه",theme:"sage",kind:"portfolio",active:true,demo:true },
  { id:"paperback",title:"در امتداد دریا",subtitle:"جلد شومیز و صحافی چسب گرم؛ طرحی ساده برای رمان، داستان و مجموعه شعر.",eyebrow:"چاپ کتاب و جلد شومیز",image:"/portfolio/paperback.svg",link:"/calculator",label:"محاسبه چاپ مشابه",theme:"navy",kind:"portfolio",active:true,demo:true },
  { id:"catalog",title:"فرم و رنگ",subtitle:"نمونه طراحی کاتالوگ تمام‌رنگی با چیدمان تصویری و صحافی منگنه.",eyebrow:"کاتالوگ و چاپ رنگی",image:"/portfolio/catalog.svg",link:"/order",label:"ثبت درخواست چاپ",theme:"orange",kind:"portfolio",active:true,demo:true },
];
export async function allPortfolio(): Promise<PortfolioItem[]> { return (await publicBootstrap()).portfolio; }
export async function publicPortfolio() { return (await publicBootstrap()).portfolio; }
