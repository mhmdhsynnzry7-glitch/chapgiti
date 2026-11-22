"use client";
import { useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { customerAuthRequest, type OtpChallenge } from "@/lib/customer-auth-api";
import { normalizeDigits } from "@/lib/pricing";
export default function PhoneVerification({ phone }: { phone: string }) {
  const router = useRouter();
  const [challenge, setChallenge] = useState<OtpChallenge | null>(null);
  const [code, setCode] = useState(""), [error, setError] = useState(""), [busy, setBusy] = useState(false);
  const [now, setNow] = useState(0), [resendAt, setResendAt] = useState(0), [done, setDone] = useState(false);
  const sending = useRef(false), codeInput = useRef<HTMLInputElement>(null);
  useEffect(() => { if (!challenge) return; const timer = setInterval(() => setNow(Date.now()), 1000); codeInput.current?.focus(); return () => clearInterval(timer); }, [challenge]);
  async function perform(action: () => Promise<void>) {
    if (sending.current) return;
    sending.current = true; setBusy(true); setError("");
    try { await action(); } catch (cause) { setError(cause instanceof Error ? cause.message : "درخواست انجام نشد."); }
    finally { sending.current = false; setBusy(false); }
  }
  const remaining = Math.max(0, Math.ceil(((challenge?.expiresAt ?? 0) - now) / 1000));
  return <section className="phone-verification">
    <h2>تأیید شماره موبایل</h2><p>برای فعال‌شدن بازیابی رمز، شمارهٔ همین حساب را با پیامک تأیید کن.</p>
    {done ? <p role="status">شماره موبایل تأیید شد.</p> : <>
      <button type="button" className="button button-outline" disabled={busy || resendAt > now} onClick={() => void perform(async () => {
        const result = await customerAuthRequest<OtpChallenge>("otp/request", { phone, purpose: "verify-phone" });
        setChallenge(result); setCode(""); setNow(Date.now()); setResendAt(Date.now() + result.resendAfterSeconds * 1000);
      })}>{challenge ? "ارسال مجدد کد" : "دریافت کد تأیید شماره"}</button>
      {challenge && <form onSubmit={event => { event.preventDefault(); void perform(async () => {
        await customerAuthRequest("phone/verify", { challengeId: challenge.challengeId, code });
        setDone(true); setChallenge(null); router.refresh();
      }); }}>
        <label className="field">کد تأیید شماره<input ref={codeInput} inputMode="numeric" autoComplete="one-time-code" dir="ltr" required pattern={"[0-9]{" + challenge.codeLength + "}"} maxLength={challenge.codeLength} value={code} onChange={e => setCode(normalizeDigits(e.target.value))} /></label>
        <p className="auth-timer">{remaining > 0 ? remaining.toLocaleString("fa-IR") + " ثانیه اعتبار" : "کد منقضی شده است."}</p>
        <button className="button" disabled={busy || remaining === 0}>تأیید شماره</button>
      </form>}
    </>}
    {error && <p role="alert" className="error-message">{error}</p>}
  </section>;
}
