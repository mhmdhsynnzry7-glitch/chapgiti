"use client";
import { sessionFetch } from "@/lib/session-client";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { LogOut } from "lucide-react";
export default function CustomerLogout() {
  const router = useRouter();
  const [busy,setBusy] = useState(false), [error,setError] = useState("");
  return <div><button className="button button-outline button-sm" disabled={busy} onClick={async()=>{
    setBusy(true); setError("");
    try {
      const response=await sessionFetch("/api/customer/logout",{method:"POST"});
      if (!response.ok) throw new Error("خروج انجام نشد؛ دوباره تلاش کنید.");
      router.replace("/");
      router.refresh();
    } catch (cause) { setError(cause instanceof Error ? cause.message : "ارتباط برقرار نشد."); setBusy(false); }
  }}><LogOut size={16}/>{busy ? "در حال خروج…" : "خروج از حساب"}</button>
  {error && <p className="error-message" role="alert">{error}</p>}</div>;
}
