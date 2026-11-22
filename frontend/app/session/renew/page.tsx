import { safeCustomerNext } from "@/lib/customer";
import RenewSession from "@/components/renew-session";
export const metadata = { title: "تمدید نشست", robots: { index: false, follow: false } };
export default async function Page({ searchParams }: { searchParams: Promise<{ next?: string }> }) {
  const params = await searchParams;
  return <RenewSession next={safeCustomerNext(params.next)} />;
}
