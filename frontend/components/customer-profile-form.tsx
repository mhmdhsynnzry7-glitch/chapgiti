"use client";
import Link from "next/link";
import { useRef, useState, type FormEvent } from "react";
import { ArrowLeft } from "lucide-react";
import type { CustomerProfile } from "@/lib/customer";
import { customerAuthRequest } from "@/lib/customer-auth-api";
import PhoneVerification from "./phone-verification";

export default function CustomerProfileForm({ next, customer }: { next: string; customer: CustomerProfile }) {
  const [firstName, setFirstName] = useState(customer.firstName), [lastName, setLastName] = useState(customer.lastName);
  const [busy, setBusy] = useState(false), [error, setError] = useState("");
  const sending = useRef(false);
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (sending.current) return;
    setError("");
    if (firstName.trim().length < 2 || lastName.trim().length < 2) { setError("نام و نام خانوادگی باید حداقل دو نویسه باشند."); return; }
    sending.current = true; setBusy(true);
    try {
      await customerAuthRequest("profile", { firstName: firstName.trim(), lastName: lastName.trim() }, "PUT");
      window.location.assign(next);
    } catch (cause) { setError(cause instanceof Error ? cause.message : "ارتباط برقرار نشد."); }
    finally { sending.current = false; setBusy(false); }
  }
  return <div className="entry-form-panel">
    <span className="step-pill">مشخصات حساب</span><h1>ویرایش مشخصات</h1>
    <p>نام و نام خانوادگی خودت را ویرایش کن.</p>
    <form onSubmit={submit} aria-busy={busy}>
      <div className="entry-name-grid">
        <label className="field">نام<input name="firstName" autoComplete="given-name" required minLength={2} maxLength={80} value={firstName} onChange={e => setFirstName(e.target.value)} /></label>
        <label className="field">نام خانوادگی<input name="lastName" autoComplete="family-name" required minLength={2} maxLength={80} value={lastName} onChange={e => setLastName(e.target.value)} /></label>
      </div>
      {error && <p role="alert" className="error-message">{error}</p>}
      <button className="button button-full" disabled={busy}>{busy ? "در حال ذخیره…" : "ذخیره و ادامه"}<ArrowLeft size={18} /></button>
    </form>
    {!customer.phoneVerified && <PhoneVerification phone={customer.phone} />}
    <Link prefetch={false} href="/account" className="text-link entry-back">بازگشت به حساب کاربری</Link>
  </div>;
}
