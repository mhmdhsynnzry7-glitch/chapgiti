import Link from "next/link";
import { redirect } from "next/navigation";
import AdminLogin from "@/components/admin-login";
import { Brand } from "@/components/ui";
import { currentAdmin, safeAdminNext } from "@/lib/auth";
export const metadata = { title: "ورود مدیر", robots: { index: false, follow: false } };
export default async function Page({ searchParams }: { searchParams: Promise<{ next?: string }> }) {
  const next = safeAdminNext((await searchParams).next);
  if (await currentAdmin()) redirect(next);
  return <main id="main" className="admin-login"><Brand /><h1>ورود مدیریت چاپ گیتی</h1>
    <p>با نام کاربری ثابت admin و رمز حساب مدیریت وارد شوید.</p><AdminLogin next={next} />
    <Link href="/" className="text-link">بازگشت به وب‌سایت</Link></main>;
}
