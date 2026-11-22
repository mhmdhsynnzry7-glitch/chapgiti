import { Phone,Mail,MapPin,Clock,ArrowUpLeft,Send,MessageCircle } from "lucide-react";
function Instagram({size=21}:{size?:number}){return <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.8} aria-hidden="true"><rect x={3} y={3} width={18} height={18} rx={5}/><circle cx={12} cy={12} r={4}/><circle cx={17.5} cy={6.5} r={1} fill="currentColor" stroke="none"/></svg>;}
import { siteSettings } from "@/lib/server";
import { seo } from "@/lib/seo";
export async function generateMetadata(){return seo("ارتباط با چاپ گیتی","راه‌های ارتباط با چاپخانه و ساعات پاسخ‌گویی.","/contact");}
export default async function Page(){
  const settings=await siteSettings();
  const channels=[{key:"instagram" as const,label:"اینستاگرام",Icon:Instagram},{key:"telegram" as const,label:"تلگرام",Icon:Send},{key:"bale" as const,label:"بله",Icon:MessageCircle}].filter(c=>settings[c.key]);
  return <div className="container page-body contact-page">
    <header className="contact-intro"><span className="eyebrow">چاپ گیتی</span><h1>ارتباط با چاپ گیتی</h1><p>برای راهنمایی دربارهٔ چاپ و نشر یا پیگیری درخواستتان، از راه‌های ارتباطی زیر با ما در تماس باشید.</p></header>
    <section className="contact-card-grid" aria-label="اطلاعات ارتباط با چاپخانه">
      {settings.phone&&<article className="contact-card"><span className="contact-card-icon"><Phone aria-hidden="true"/></span><h2>تلفن چاپخانه</h2><p>برای ارتباط مستقیم با چاپخانه</p><a className="contact-value" href={"tel:"+settings.phone}><bdi>{settings.phone}</bdi><ArrowUpLeft size={18} aria-hidden="true"/></a></article>}
      {settings.email&&<article className="contact-card"><span className="contact-card-icon"><Mail aria-hidden="true"/></span><h2>ایمیل</h2><p>برای مکاتبه با چاپخانه</p><a className="contact-value" href={"mailto:"+settings.email}><bdi>{settings.email}</bdi><ArrowUpLeft size={18} aria-hidden="true"/></a></article>}
      {settings.address&&<article className="contact-card"><span className="contact-card-icon"><MapPin aria-hidden="true"/></span><h2>نشانی چاپخانه</h2><p className="contact-address">{settings.address}</p></article>}
      {settings.hours&&<article className="contact-card"><span className="contact-card-icon"><Clock aria-hidden="true"/></span><h2>ساعات پاسخ‌گویی</h2><p className="contact-hours">{settings.hours}</p></article>}
    </section>
    {channels.length>0&&<section className="contact-channels" aria-labelledby="contact-channels-title"><div><h2 id="contact-channels-title">در ارتباط بمانیم</h2><p>راه‌های ارتباطی چاپ گیتی در شبکه‌های اجتماعی</p></div><div className="contact-social-links">{channels.map(({key,label,Icon})=><a key={key} href={settings[key]} target="_blank" rel="noopener noreferrer" aria-label={label+" (در برگهٔ جدید)"}><Icon size={21} aria-hidden="true"/><span>{label}</span><ArrowUpLeft size={17} aria-hidden="true"/></a>)}</div></section>}
    {!settings.phone&&!settings.email&&!settings.address&&!settings.hours&&!channels.length&&<p className="notice">اطلاعات ارتباطی چاپخانه هنوز ثبت نشده است.</p>}
  </div>;
}
