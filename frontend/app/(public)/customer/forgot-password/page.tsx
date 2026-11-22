import CustomerEntry from "@/components/customer-entry";
import { safeCustomerNext } from "@/lib/customer";

export const metadata = { title: "بازیابی رمز عبور", robots: { index: false, follow: false } };

export default async function Page({ searchParams }: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const params = await searchParams;
  return <div className="container customer-page">
    <CustomerEntry next={safeCustomerNext(params.next)} recovery />
  </div>;
}
