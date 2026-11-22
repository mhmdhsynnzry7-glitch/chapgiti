import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import AdminApp from "@/components/admin";
import SessionRenewal from "@/components/session-renewal";
import { NotificationProvider } from "@/components/notification-center";
import { backendApi, BackendError } from "@/lib/backend";
import type { OverviewResult } from "@/lib/api-contracts";
import type { AdminOrderDetailData } from "@/components/admin-order-detail";
export const dynamic = "force-dynamic";
export const metadata = { title: "پنل مدیریت چاپخانه", robots: { index: false, follow: false } };
type AdminIdentity = { userId: string; email: string; displayName: string; legacy: boolean };
export default async function Page({ params }: { params: Promise<{ section?: string[] }> }) {
  const { section = [] } = await params;
  const destination = "/admin" + (section.length ? "/" + section.map(encodeURIComponent).join("/") : "");
  let user: AdminIdentity;
  try { user = await backendApi<AdminIdentity>("auth/me"); }
  catch (error) {
    if (!(error instanceof BackendError)) throw error;
    if (error.status === 401) {
      const route = (await cookies()).has("giti_admin_refresh") ? "/admin/session-renew" : "/admin/login";
      redirect(route + "?next=" + encodeURIComponent(destination));
    }
    throw error;
  }
  if (section[0] === "users") redirect("/admin/customers");
  if (section[0] === "portfolio") redirect("/admin/services");
  let initialOverview: OverviewResult | undefined;
  let initialOrder: AdminOrderDetailData | undefined;
  if (!section.length || section[0] === "overview") {
    try { initialOverview = await backendApi<OverviewResult>("admin/overview"); }
    catch { /* Keep the existing client error/retry experience if this read fails. */ }
  }
  if (section[0] === "orders" && /^[a-f0-9-]{36}$/.test(section[1] || "")) {
    try { initialOrder = await backendApi<AdminOrderDetailData>("admin/orders/" + section[1]); }
    catch { /* The existing explicit POST open retains its own error/retry state. */ }
  }
  return <NotificationProvider key={user.userId} scope="admin"><SessionRenewal context="administrator" /><AdminApp section={section[0] || "overview"} initialOrderId={section[1]} initialOverview={initialOverview} initialOrder={initialOrder}
    user={{ id: user.userId, name: user.displayName, email: user.email, legacy: user.legacy }} /></NotificationProvider>;
}
