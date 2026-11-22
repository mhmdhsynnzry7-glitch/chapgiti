import Link from "next/link";
import {
  BookOpen,
  ArrowUpLeft,
  Printer,
  Feather,
  Palette,
  PanelsTopLeft,
  PenTool,
  Layers,
  Check,
  ArrowLeft,
  ShieldCheck,
  FileCheck,
  Truck,
  ChevronLeft,
} from "lucide-react";
export {
  ArrowUpLeft,
  ArrowLeft,
  Check,
  ShieldCheck,
  FileCheck,
  Truck,
  ChevronLeft,
};
export function Brand({
  name = "چاپ گیتی",
  light = false,
}: {
  name?: string;
  light?: boolean;
}) {
  return (
    <Link prefetch={false}
      className={"brand " + (light ? "light" : "")}
      href="/"
      aria-label={name + "، صفحه اصلی"}
    >
      <span className="brand-icon">
        <BookOpen size={27} />
      </span>
      <span>
        <strong>{name}</strong>
        <small>چاپ، خلق، ماندگاری</small>
      </span>
    </Link>
  );
}
export function ServiceIcon({
  name,
  size = 25,
}: {
  name: string;
  size?: number;
}) {
  const Icon =
    (
      {
        book: Printer,
        feather: Feather,
        palette: Palette,
        layout: PanelsTopLeft,
        pen: PenTool,
        layers: Layers,
      } as Record<string, typeof BookOpen>
    )[name] || BookOpen;
  return <Icon size={size} />;
}
export function SectionTitle({
  eyebrow,
  title,
  description,
  link,
  href,
}: {
  eyebrow: string;
  title: string;
  description?: string;
  link?: string;
  href?: string;
}) {
  return (
    <div className="section-heading">
      <div>
        <span className="eyebrow">
          <i />
          {eyebrow}
        </span>
        <h2>{title}</h2>
        {description && <p>{description}</p>}
      </div>
      {href && (
        <Link prefetch={false} className="text-link" href={href}>
          {link}
          <ArrowUpLeft size={19} />
        </Link>
      )}
    </div>
  );
}
export function BookArt({ small = false }: { small?: boolean }) {
  return (
    <div
      className={"book-stage " + (small ? "small" : "")}
      role="img"
      aria-label="تصویرسازی نمونه جلدهای کتاب در رنگ‌های سرمه‌ای، نارنجی و کرم"
    >
      <div className="stage-orbit" />
      <span className="stage-caption">هر کتاب، آغاز یک جهان تازه</span>
      <div className="book book-back">
        <div className="cover-type">
          روایتِ
          <br />
          روزهای
          <br />
          <b>روشن</b>
        </div>
        <div className="sun-art" />
        <small>مجموعه داستان · طرح نمونه</small>
      </div>
      <div className="book book-main">
        <span className="book-edition">چاپ گیتی / مجموعه ادبیات</span>
        <div className="book-title">
          آن‌سوی
          <br />
          <em>کلمات</em>
        </div>
        <div className="wave-art">
          <span />
          <span />
          <span />
          <span />
          <span />
          <span />
          <span />
        </div>
        <span className="book-author">داستانی که از شما آغاز می‌شود</span>
      </div>
      <div className="book-flat">
        <span>برای قصه‌هایی که باید خوانده شوند.</span>
      </div>
      <div className="floating-label">
        <span className="green-dot" />
        <div>
          <b>از ایده تا نسخه چاپی</b>
          <small>همراه شما، صفحه به صفحه</small>
        </div>
        <BookOpen size={21} />
      </div>
      <div className="edition-badge">
        با عشق
        <br />
        <b>چاپ می‌شود</b>
        <span>✳</span>
      </div>
    </div>
  );
}
export function Empty({
  title = "هنوز موردی ثبت نشده",
  text = "موارد جدید اینجا نمایش داده می‌شوند.",
}: {
  title?: string;
  text?: string;
}) {
  return (
    <div className="empty">
      <BookOpen size={36} />
      <h3>{title}</h3>
      <p>{text}</p>
    </div>
  );
}
export function Status({ value, label }: { value: string; label?: string }) {
  return (
    <span className={"status status-" + value}>
      <i />
      {label || value}
    </span>
  );
}
