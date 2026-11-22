"use client";
import Link from "next/link";
import { useEffect, useState } from "react";
import { ensureSession, type SessionContext } from "@/lib/session-client";

export default function RenewSession({ next, context = "customer" }: { next: string; context?: SessionContext }) {
  const [error, setError] = useState(false);
  useEffect(() => {
    let active = true;
    void ensureSession(context).then(ok => {
      if (!active) return;
      if (ok) window.location.assign(next);
      else setError(true);
    });
    return () => { active = false; };
  }, [next, context]);
  return <main className="admin-login" id="main">
    <h1>{error ? "دوباره وارد حساب شوید" : "در حال تمدید نشست…"}</h1>
    {error ? <><p role="alert">نشست قابل تمدید نیست. برای ادامه با رمز عبور وارد شوید.</p>
      <Link className="button" href={(context === "administrator" ? "/admin/login?next=" : "/customer?next=") + encodeURIComponent(next)}>ورود</Link></>
      : <p role="status">چند لحظه صبر کنید.</p>}
  </main>;
}
