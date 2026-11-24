import Header from "@/components/header";
import Footer from "@/components/footer";
import { siteSettings } from "@/lib/server";
import { currentCustomer, customerIsReady } from "@/lib/customer";
import SessionRenewal from "@/components/session-renewal";
import { NotificationProvider } from "@/components/notification-center";
import { cookies } from "next/headers";
export const dynamic = "force-dynamic";
export default async function PublicLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const [site, customer] = await Promise.all([siteSettings(), currentCustomer()]);
  const jar = await cookies();
  const content = (
    <>
      {jar.has("giti_customer_refresh") && <SessionRenewal />}
      <Header brand={site.brand} customerName={customerIsReady(customer) ? [customer.firstName.trim(), customer.lastName.trim()].filter(Boolean).join(" ") : undefined} hasCustomerAccount={customerIsReady(customer)} />
      <main id="main">{children}</main>
      <Footer site={site} />
    </>
  );
  return customerIsReady(customer) ? <NotificationProvider key={customer.id} scope="customer">{content}</NotificationProvider> : content;
}
