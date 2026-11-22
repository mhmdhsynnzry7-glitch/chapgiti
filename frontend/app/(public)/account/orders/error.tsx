"use client";
import Link from "next/link";
export default function Error({ reset }: { reset: () => void }) {
  return <div className="container page-body"><section className="panel order-list-message"><h1>دریافت سفارش انجام نشد</h1><p role="alert">ارتباط با سرویس برقرار نشد. دوباره تلاش کنید.</p><button type="button" className="button" onClick={reset}>تلاش دوباره</button><Link prefetch={false} className="text-link" href="/account/orders">بازگشت به سفارش‌های من</Link></section></div>;
}
