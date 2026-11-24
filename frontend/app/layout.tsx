import PersianFormValidation from "@/components/persian-form-validation";
import type { Metadata } from "next";
import "./globals.css";
import "./workflow.css";
export const metadata: Metadata = {
  title: {
    default: "چاپ گیتی | چاپ کتاب و خدمات انتشارات",
    template: "%s | چاپ گیتی",
  },
  description: "محاسبه هزینه چاپ کتاب و ثبت سفارش آنلاین خدمات چاپ و نشر.",
  icons: { icon: "/favicon.svg" },
  robots: { index: false, follow: false },
};
export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="fa" dir="rtl">
      <body>
        <PersianFormValidation />
        <a className="skip-link" href="#main">
          رفتن به محتوای اصلی
        </a>
        {children}
      </body>
    </html>
  );
}
