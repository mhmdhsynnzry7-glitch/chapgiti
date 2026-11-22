"use client";
import Link from "next/link";
import { useRef, useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { api } from "@/lib/api-client";
import { passwordError } from "@/lib/password-policy";
import NewPasswordFields from "./new-password-fields";
import { ADMIN_USERNAME } from "@/lib/admin-auth";

export default function AdministratorProfile({ email: initialEmail }: { email: string }) {
  const router = useRouter();
  const [email, setEmail] = useState(initialEmail), [currentEmail, setCurrentEmail] = useState(initialEmail);
  const [currentPassword, setCurrentPassword] = useState(""), [password, setPassword] = useState(""), [confirmation, setConfirmation] = useState("");
  const [busy, setBusy] = useState(""), [error, setError] = useState(""), [message, setMessage] = useState(""), [changed, setChanged] = useState(false);
  const sending = useRef(false);
  async function submit(event: FormEvent<HTMLFormElement>, operation: "email" | "password") {
    event.preventDefault(); if (sending.current) return;
    setError(""); setMessage("");
    if (operation === "password" && passwordError(password, confirmation)) { setError(passwordError(password, confirmation)); return; }
    sending.current = true; setBusy(operation);
    try {
      await api("admin/profile/" + operation, { method: "PUT", body: JSON.stringify(operation === "email" ? { email } : { currentPassword, password, confirmation }) });
      if (operation === "email") { setCurrentEmail(email.trim().toLowerCase()); setMessage("ایمیل شما تغییر کرد."); router.refresh(); }
      else { setCurrentPassword(""); setPassword(""); setConfirmation(""); setChanged(true); }
    } catch (cause) { setError(cause instanceof Error ? cause.message : "تغییرات ذخیره نشد. دوباره تلاش کنید."); }
    finally { sending.current = false; setBusy(""); }
  }
  return <section className="administrator-profile">
    <div className="admin-heading"><div><h2>پروفایل مدیر</h2><p>فقط ایمیل و رمز حساب مدیریت خودتان را تغییر دهید.</p></div></div>
    <div className="panel"><label className="field">نام کاربری<input name="username" value={ADMIN_USERNAME} readOnly dir="ltr" autoComplete="username" /></label><p>نام کاربری ثابت است؛ تغییر ایمیل روی نام ورود اثری ندارد.</p></div>
    {changed ? <div className="panel"><p role="status">رمز عبور تغییر کرد و نشست‌های مدیریت شما باطل شدند. دوباره وارد شوید.</p><Link className="button" href="/admin/login">ورود دوباره به مدیریت</Link></div> : <>
      <form className="panel" aria-label="تغییر ایمیل مدیر" aria-busy={busy === "email"} onSubmit={e => void submit(e, "email")}>
        <h3>ایمیل پروفایل</h3><p>ایمیل فعلی: <bdi>{currentEmail}</bdi></p>
        <label className="field">ایمیل جدید<input name="email" type="email" dir="ltr" autoComplete="email" required maxLength={254} disabled={!!busy} value={email} onChange={e => { e.target.setCustomValidity(""); setEmail(e.target.value); }} onInvalid={e => e.currentTarget.setCustomValidity("ایمیل معتبر وارد کنید.")} /></label>
        <button type="submit" className="button" disabled={!!busy}>{busy === "email" ? "در حال ذخیره…" : "تغییر ایمیل"}</button>
      </form>
      <form className="panel" aria-label="تغییر رمز مدیر" aria-busy={busy === "password"} onSubmit={e => void submit(e, "password")}>
        <h3>تغییر رمز عبور</h3><p>پس از تغییر رمز، ورود دوباره به مدیریت لازم است. نشست مشتری مستقل باقی می‌ماند.</p>
        <label className="field">رمز عبور فعلی<input name="currentPassword" type="password" dir="ltr" autoComplete="current-password" required maxLength={128} disabled={!!busy} value={currentPassword} onChange={e => { e.target.setCustomValidity(""); setCurrentPassword(e.target.value); }} onInvalid={e => e.currentTarget.setCustomValidity("رمز فعلی را وارد کنید.")} /></label>
        <NewPasswordFields password={password} confirmation={confirmation} onPassword={setPassword} onConfirmation={setConfirmation} disabled={!!busy} />
        <button type="submit" className="button" disabled={!!busy}>{busy === "password" ? "در حال ذخیره…" : "تغییر رمز عبور"}</button>
      </form>
      {message && <p className="notice" role="status">{message}</p>}{error && <p className="error-message" role="alert">{error}</p>}
    </>}
  </section>;
}
