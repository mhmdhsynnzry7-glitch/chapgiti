"use client";
import { useRef, useState, type FormEvent } from "react";
import { requireSecureAuthenticationPage } from "@/lib/session-client";
import { ADMIN_USERNAME } from "@/lib/admin-auth";

export default function AdminLogin({ next }: { next: string }) {
  const [error, setError] = useState(""), [pending, setPending] = useState(false);
  const sending = useRef(false);
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (sending.current) return;
    const fields = new FormData(event.currentTarget);
    sending.current = true; setPending(true); setError("");
    try {
      requireSecureAuthenticationPage();
      const res = await fetch("/api/auth/login", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ username: ADMIN_USERNAME, password: fields.get("password") }),
      });
      const data = await res.json();
      if (!res.ok) throw new Error(data.error || "ورود انجام نشد.");
      window.location.assign(next);
    } catch (cause) { setError(cause instanceof TypeError ? "ارتباط برقرار نشد. دوباره تلاش کنید." : cause instanceof Error ? cause.message : "ورود انجام نشد."); }
    finally { sending.current = false; setPending(false); }
  }
  return <form onSubmit={submit} className="local-login-form" aria-busy={pending}
    onInput={e => { if (e.target instanceof HTMLInputElement) e.target.setCustomValidity(""); }}
    onInvalid={e => { if (e.target instanceof HTMLInputElement) e.target.setCustomValidity("رمز عبور را وارد کنید."); }}>
    <label className="field">نام کاربری<input name="username" type="text" value={ADMIN_USERNAME} readOnly autoComplete="username" dir="ltr" /><small>نام کاربری مدیر ثابت است.</small></label>
    <label className="field">رمز عبور<input name="password" type="password" required minLength={1} maxLength={128} disabled={pending} autoComplete="current-password" dir="ltr" /></label>
    {error && <p className="error-message" role="alert">{error}</p>}
    <button type="submit" className="button button-full" disabled={pending}>{pending ? "در حال ورود…" : "ورود به مدیریت"}</button>
  </form>;
}
