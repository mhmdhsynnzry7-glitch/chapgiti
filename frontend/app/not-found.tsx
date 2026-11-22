import Link from "next/link";
export default function NotFound() {
  return (
    <main id="main" className="container error-page">
      <span className="eyebrow">۴۰۴</span>
      <h1>این صفحه از داستان پیدا نشد.</h1>
      <p>نشانی را بررسی کنید یا به صفحه اصلی برگردید.</p>
      <Link prefetch={false} className="button" href="/">
        بازگشت به خانه
      </Link>
    </main>
  );
}
