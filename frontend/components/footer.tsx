import Link from "next/link";
import type { initialSettings } from "@/lib/content";
import { Brand, ArrowUpLeft } from "./ui";
export default function Footer({ site }: { site: typeof initialSettings }) {
  return (
    <footer className="footer">
      <div className="container footer-main">
        <div className="footer-about">
          <Brand name={site.brand} light />
          <p>
            از اولین جرقه یک ایده تا عطر کاغذ تازه؛
            <br />
            برای قصه‌هایی که باید ماندگار شوند.
          </p>
          <span className="footer-note">چاپ کتاب و خدمات نشر، یک‌جا</span>
        </div>
        <div>
          <h3>کشف کنید</h3>
          <Link prefetch={false} href="/services">خدمات چاپ و نشر</Link>
          <Link prefetch={false} href="/blog">مجله چاپ گیتی</Link>
          <Link prefetch={false} href="/calculator">محاسبه هزینه چاپ</Link>
        </div>
        <div>
          <h3>کنار شما هستیم</h3>
          <Link prefetch={false} href="/order">ثبت سفارش جدید</Link>
          <Link prefetch={false} href="/track">پیگیری سفارش</Link>
          <Link prefetch={false} href="/account">حساب کاربری</Link>
          <Link prefetch={false} href="/contact">مشاوره و ارتباط</Link>
          <Link prefetch={false} href="/privacy">حریم خصوصی</Link>
        </div>
        <div className="footer-contact">
          <h3>از کتابتان برای ما بگویید.</h3>
          <p>{site.address || "برای شروع، از راه‌های ارتباطی چاپخانه استفاده کنید."}</p>
          {site.phone && (
            <Link prefetch={false} dir="ltr" href={"tel:" + site.phone}>
              {site.phone}
            </Link>
          )}
          {site.email && <Link prefetch={false} href={"mailto:" + site.email}>{site.email}</Link>}
          <Link prefetch={false} className="footer-cta" href="/contact">
            شروع یک گفت‌وگو <ArrowUpLeft size={18} />
          </Link>
        </div>
      </div>
      <div className="container footer-bottom">
        <span>
          ©{" "}
          {new Intl.DateTimeFormat("fa-IR", { year: "numeric" }).format(
            new Date(),
          )}{" "}
          · تمامی حقوق برای {site.brand} محفوظ است.
        </span>
        <span>ساخته‌شده برای ماندگاری.</span>
      </div>
    </footer>
  );
}
