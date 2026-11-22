import Link from "next/link";
import {
  BookOpen,
  CheckCircle2,
  ArrowUpLeft,
  FileText,
  SlidersHorizontal,
  PackageCheck,
  Send,
} from "lucide-react";
import ServiceCard from "@/components/service-card";
import BannerShowcase from "@/components/banner-showcase";
import { publicBanners } from "@/lib/banners";
import { SectionTitle } from "@/components/ui";
import { faqs } from "@/lib/content";
import { siteSettings, publicServices, origin, indexed } from "@/lib/server";
export const dynamic = "force-dynamic";
export async function generateMetadata() {
  const s = await siteSettings();
  return {
    title: { absolute: s.seoTitle },
    description: s.seoDescription,
    alternates: { canonical: origin() },
    robots: { index: indexed(), follow: indexed() },
    openGraph: {
      title: s.seoTitle,
      description: s.seoDescription,
      url: origin(),
      locale: "fa_IR",
      type: "website",
      images: [origin() + "/og.png"],
    },
  };
}
export default async function Home() {
  const [site, banners, services] = await Promise.all([siteSettings(), publicBanners(), publicServices()]);
  const schema = {
    "@context": "https://schema.org",
    "@type": "Organization",
    name: site.brand,
    url: origin(),
    ...(site.phone ? { telephone: site.phone } : {}),
    ...(site.email ? { email: site.email } : {}),
  };
  return (
    <>
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{
          __html: JSON.stringify(schema).replace(/</g, "\\u003c"),
        }}
      />
      <BannerShowcase banners={banners} />
      <section className="promise-strip">
        <div className="container promises">
          <div>
            <span className="promise-icon">
              <BookOpen />
            </span>
            <p>
              <b>به اندازه نیازتان</b>
              <small>امکان سفارش از یک جلد</small>
            </p>
          </div>
          <div>
            <span className="promise-icon">
              <SlidersHorizontal />
            </span>
            <p>
              <b>انتخاب با شماست</b>
              <small>قطع، کاغذ، رنگ و صحافی</small>
            </p>
          </div>
          <div>
            <span className="promise-icon">
              <FileText />
            </span>
            <p>
              <b>شفاف، از همان ابتدا</b>
              <small>برآورد جزئیات هزینه چاپ</small>
            </p>
          </div>
          <div>
            <span className="promise-icon">
              <PackageCheck />
            </span>
            <p>
              <b>همراه تا آخرین صفحه</b>
              <small>پیگیری مرحله‌به‌مرحله سفارش</small>
            </p>
          </div>
        </div>
      </section>

      <section className="container section">
        <SectionTitle
          eyebrow="هر آنچه کتاب شما نیاز دارد"
          title="یک مسیر کامل، از کلمه تا کتاب"
          description="هر جای مسیر که باشید، برای قدم بعدی کنارتان هستیم."
          link="همه خدمات ما"
          href="/services"
        />
        <div className="services-grid">
          {services.slice(0, 4).map(service => <ServiceCard key={service.id} service={service} />)}
        </div>
      </section>
      <section className="container section finishing-showcase">
        <SectionTitle eyebrow="کتاب شما، با امضای شما" title="جزئیاتی که فرق می‌سازند" description="آشنایی با جزئیات تکمیلی چاپ و صحافی کتاب." />
        <div className="finishing-service-grid">{[
          ["طلاکوب و نقره‌کوب", "درخشش روی جلد کتاب", "foil", "✦"],
          ["گوشه گرد", "ظرافت در آخرین برش", "rounded", "⌜"],
          ["شیرینک", "بسته‌بندی و محافظت", "shrink", "▱"],
          ["آستر بدرقه چاپی", "زیبایی از اولین صفحه", "endpaper", "▦"],
        ].map(([title, subtitle, style, symbol]) => <article className={"finishing-service-card " + style} key={style}>
          <div className="finishing-service-art" aria-hidden="true"><i /><span>{symbol}</span></div><h3>{title}</h3><p>{subtitle}</p>
        </article>)}</div>
      </section>
      <section className="container calculator-banner">
        <div className="calc-banner-art">
          <div className="paper-slip">
            <span>مشخصات کتاب شما</span>
            <div>
              <span>تعداد صفحات</span>
              <b>۱۲۰ صفحه</b>
            </div>
            <div>
              <span>تیراژ</span>
              <b>۱۰۰ جلد</b>
            </div>
            <div>
              <span>نوع صحافی</span>
              <b>شومیز</b>
            </div>
            <div className="slip-total">
              <span>آماده محاسبه</span>
              <b>↖</b>
            </div>
          </div>
          <span className="calc-symbol">+</span>
        </div>
        <div>
          <span className="eyebrow">ساده، روشن، بدون حدس</span>
          <h2>
            قبل از شروع،
            <br />
            هزینه‌اش را بدانید.
          </h2>
          <p>
            مشخصات کتابتان را انتخاب کنید و همان لحظه
            <br />
            برآورد هزینه چاپ را ببینید. تصمیم با شماست.
          </p>
          <Link prefetch={false} href="/calculator" className="button button-white">
            محاسبه آنلاین هزینه چاپ <ArrowUpLeft size={19} />
          </Link>
        </div>
        <span className="banner-watermark">چاپ</span>
      </section>
      <section className="container section process-section">
        <SectionTitle
          eyebrow="از همین‌جا شروع کنید"
          title="چهار قدم تا کتاب شما"
          description="یک مسیر مشخص، بدون سردرگمی."
        />
        <div className="steps">
          {[
            {
              n: "۰۱",
              icon: SlidersHorizontal,
              t: "مشخصات را انتخاب کنید",
              d: "قطع، کاغذ، تیراژ و صحافی دلخواهتان را مشخص کنید.",
            },
            {
              n: "۰۲",
              icon: Send,
              t: "فایل و درخواست را بفرستید",
              d: "اطلاعات سفارش را ثبت و فایل PDF کتاب را بارگذاری کنید.",
            },
            {
              n: "۰۳",
              icon: CheckCircle2,
              t: "با هم نهایی می‌کنیم",
              d: "پس از بررسی فایل، قیمت و جزئیات تولید تأیید می‌شود.",
            },
            {
              n: "۰۴",
              icon: PackageCheck,
              t: "کتابتان را تحویل بگیرید",
              d: "روند چاپ و صحافی را تا آماده شدن سفارش دنبال کنید.",
            },
          ].map((s) => (
            <div className="step" key={s.n}>
              <div className="step-top">
                <span>{s.n}</span>
                <s.icon size={23} />
              </div>
              <h3>{s.t}</h3>
              <p>{s.d}</p>
            </div>
          ))}
        </div>
      </section>

      <section className="container section faq-section">
        <div>
          <span className="eyebrow">
            <i /> پاسخ سؤال‌های شما
          </span>
          <h2>
            شاید سؤال شما
            <br />
            هم همین باشد.
          </h2>
          <p>
            هنوز چیزی برایتان روشن نیست؟
            <br />
            خوشحال می‌شویم راهنمایی‌تان کنیم.
          </p>
          <Link prefetch={false} className="text-link" href="/contact">
            گفت‌وگو با ما <ArrowUpLeft size={18} />
          </Link>
        </div>
        <div className="faq-list">
          {faqs.map(([q, a], i) => (
            <details key={q} open={i === 0}>
              <summary>
                {q}
                <span>+</span>
              </summary>
              <p>{a}</p>
            </details>
          ))}
        </div>
      </section>
      <section className="container closing-cta">
        <span className="cta-star">✳</span>
        <div>
          <span>وقت آن رسیده که کتابتان را ورق بزنید.</span>
          <h2>داستان بعدی، داستان شماست.</h2>
        </div>
        <Link prefetch={false} className="button" href="/order">
          سفارش چاپ را شروع کنید <ArrowUpLeft size={20} />
        </Link>
      </section>
    </>
  );
}
