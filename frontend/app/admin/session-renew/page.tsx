import RenewSession from "@/components/renew-session";
import { safeAdminNext } from "@/lib/auth";
export const metadata = { title: "تمدید نشست مدیریت", robots: { index: false, follow: false } };
export default async function Page({ searchParams }: { searchParams: Promise<{ next?: string }> }) {
  return <RenewSession next={safeAdminNext((await searchParams).next)} context="administrator" />;
}
