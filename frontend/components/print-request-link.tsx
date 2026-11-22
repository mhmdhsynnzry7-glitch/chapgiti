"use client";
import Link from "next/link";
import { useState, type ReactNode } from "react";
import { storeRequestSpec } from "@/lib/request-draft";
import type { PrintSpec } from "@/lib/pricing";

export default function PrintRequestLink({ spec, children, className }: { spec: PrintSpec; children: ReactNode; className?: string }) {
  const [error, setError] = useState("");
  return <><Link prefetch={false} className={className} href="/order?draft=1" onClick={event => {
    try { storeRequestSpec(sessionStorage, spec); }
    catch { event.preventDefault(); setError("ذخیرهٔ موقت مشخصات ممکن نشد. ذخیره‌سازی این برگه را در مرورگر فعال کنید یا از فرم درخواست چاپ وارد شوید."); }
  }}>{children}</Link>{error && <p role="alert" className="error-message">{error}</p>}</>;
}
