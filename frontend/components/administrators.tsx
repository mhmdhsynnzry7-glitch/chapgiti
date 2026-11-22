"use client";
import { useApiResource } from "@/hooks/use-api-resource";
import { date } from "@/lib/pricing";

export default function Administrators() {
  const { data, loading, error, reload } = useApiResource<{ email: string; created_at: string }[]>("admin/administrators");
  return <section><div className="admin-heading"><div><h2>حساب‌های مدیران</h2><p>ورود مدیریت با نام ثابت admin انجام می‌شود. ایمیل‌های زیر اطلاعات پروفایل‌اند؛ ساخت مدیر اضافی و ویرایش اطلاعات دیگران فعال نیست.</p></div></div>
    {loading && <p role="status">در حال دریافت مدیران…</p>}
    {error && <p role="alert">{error}<button type="button" className="button button-outline" onClick={reload}>تلاش دوباره</button></p>}
    {data && <div className="table-wrap"><table><thead><tr><th>ایمیل پروفایل</th><th>تاریخ ایجاد</th></tr></thead><tbody>{data.map(item => <tr key={item.email}><td><bdi>{item.email}</bdi></td><td>{date(item.created_at)}</td></tr>)}</tbody></table></div>}
  </section>;
}
