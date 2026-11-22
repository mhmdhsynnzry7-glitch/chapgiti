import Link from "next/link";
export default function NotFound() {
  return <div className="container page-body"><section className="panel order-list-message"><h1>سفارش پیدا نشد</h1><p>این سفارش در حساب شما در دسترس نیست.</p><Link prefetch={false} className="button" href="/account/orders">بازگشت به سفارش‌های من</Link></section></div>;
}
