"use client";
import Link from "next/link";
import { useEffect, useRef, useState, type FormEvent } from "react";
import { ArrowLeft, Smartphone, ShieldCheck } from "lucide-react";
import NewPasswordFields from "./new-password-fields";
import { passwordError, PASSWORD_GUIDANCE } from "@/lib/password-policy";
import { normalizeDigits } from "@/lib/pricing";
import { CustomerAuthError, customerAuthRequest, isOtpChallenge, type OtpChallenge, type PasswordRecoveryGrant, type RegistrationGrant, type RegistrationStart } from "@/lib/customer-auth-api";
import { requireSecureAuthenticationPage } from "@/lib/session-client";
import { CUSTOMER_LOGIN_ERRORS } from "@/lib/customer-login-errors";
import { RECOVERY_ACCOUNT_NOT_FOUND, recoveryNeedsRestart } from "@/lib/customer-otp-errors";

type View = "password" | "register" | "profile" | "request" | "verify" | "reset" | "success";
const seconds = (until: number, now: number) => Math.max(0, Math.ceil((until - now) / 1000));

export default function CustomerAuthForm({ next, recovery, initialMethod = "password" }: {
  next: string; recovery: boolean; initialMethod?: "password" | "register";
}) {
  const [recovering, setRecovering] = useState(recovery);
  const [view, setView] = useState<View>(recovering ? "request" : initialMethod);
  const [phone, setPhone] = useState("");
  const [firstName, setFirstName] = useState(""), [lastName, setLastName] = useState("");
  const [password, setPassword] = useState(""), [confirmation, setConfirmation] = useState("");
  const [code, setCode] = useState("");
  const [challenge, setChallenge] = useState<OtpChallenge | null>(null);
  const [grant, setGrant] = useState<PasswordRecoveryGrant | null>(null);
  const [registrationGrant, setRegistrationGrant] = useState<RegistrationGrant | null>(null);
  const [now, setNow] = useState(0), [resendAt, setResendAt] = useState(0);
  const [busy, setBusy] = useState(false), [error, setError] = useState(""), [notice, setNotice] = useState("");
  const [errorCode, setErrorCode] = useState<string | null>(null);
  const sending = useRef(false), codeInput = useRef<HTMLInputElement>(null);
  const loginUrl = "/customer?next=" + encodeURIComponent(next);
  const codeSeconds = seconds(challenge?.expiresAt ?? 0, now), resendSeconds = seconds(resendAt, now);
  const resetSeconds = seconds(grant?.expiresAt ?? 0, now);
  const registrationSeconds = seconds(registrationGrant?.expiresAt ?? 0, now);

  useEffect(() => {
    if (!challenge && !grant && !registrationGrant && !resendAt) return;
    const timer = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, [challenge, grant, registrationGrant, resendAt]);
  useEffect(() => { if (view === "verify") codeInput.current?.focus(); }, [view]);

  function switchView(method: View) {
    setView(method); setError(""); setErrorCode(null); setNotice(""); setPassword(""); setConfirmation("");
    setCode(""); setChallenge(null); setGrant(null);
    setRegistrationGrant(null); setFirstName(""); setLastName("");
    setResendAt(0);
  }
  async function perform(action: () => Promise<void>) {
    if (sending.current) return;
    sending.current = true; setBusy(true); setError(""); setErrorCode(null);
    try { requireSecureAuthenticationPage(); await action(); }
    catch (cause) {
      if (recovering && cause instanceof CustomerAuthError && recoveryNeedsRestart(cause.code)) {
        const cooldown = ["OTP_CONSUMED", "OTP_ATTEMPT_LIMIT"].includes(cause.code ?? "") ? resendAt : 0;
        switchView("request"); setResendAt(cooldown);
      }
      setError(cause instanceof Error ? cause.message : "ارتباط برقرار نشد. دوباره تلاش کنید.");
      setErrorCode(cause instanceof CustomerAuthError ? cause.code : null);
      if (cause instanceof CustomerAuthError && cause.retryAfterSeconds) {
        setNow(Date.now()); setResendAt(Date.now() + cause.retryAfterSeconds * 1000);
      }
    } finally { sending.current = false; setBusy(false); }
  }
  async function sendCode(requestPhone = phone) {
    let result: OtpChallenge;
    try {
      result = await customerAuthRequest<OtpChallenge>("otp/request", {
        phone: requestPhone, purpose: recovering ? "reset" : "register",
      });
    } catch (cause) {
      if (!recovering && cause instanceof CustomerAuthError && cause.status === 409 && cause.code !== "OTP_CHALLENGE_INVALID") {
        switchView("password"); setNotice(cause.message); return;
      }
      throw cause;
    }
    if (!isOtpChallenge(result)) throw new CustomerAuthError("پاسخ درخواست کد معتبر نیست. دوباره تلاش کنید.", null, null, "OTP_CHALLENGE_MISSING");
    setChallenge(result); setCode(""); setNow(Date.now());
    setResendAt(Date.now() + result.resendAfterSeconds * 1000); setNotice(result.message); setView("verify");
  }
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await perform(async () => {
      if (view === "register") {
        const result = await customerAuthRequest<RegistrationStart>("start", { phone });
        setPhone(result.phone);
        if (result.step === "password") {
          switchView("password"); setNotice("این شماره قبلاً ثبت‌نام کرده است. با رمز عبور وارد شوید.");
        } else await sendCode(result.phone);
        return;
      }
      if (view === "request") return sendCode();
      if (view === "profile") {
        if (!registrationGrant || registrationSeconds === 0) throw new Error("مهلت تکمیل ثبت‌نام تمام شده است. دوباره شماره موبایل را تأیید کنید.");
        if (passwordError(password, confirmation)) throw new Error(passwordError(password, confirmation));
        await customerAuthRequest("registration/complete", {
          registrationToken: registrationGrant.registrationToken, firstName: firstName.trim(), lastName: lastName.trim(), password, confirmation,
        });
        setRegistrationGrant(null); setPassword(""); setConfirmation(""); setFirstName(""); setLastName(""); setView("success");
        return;
      }
      if (view === "password") {
        await customerAuthRequest("login", { phone, password });
        window.location.assign(next); return;
      }
      if (view === "verify" && challenge) {
        if (codeSeconds === 0) throw new Error("مهلت کد تمام شده است. دوباره کد بگیرید.");
        if (recovering) {
          const result = await customerAuthRequest<PasswordRecoveryGrant>("password/verify", { challengeId: challenge.challengeId, code });
          setGrant(result); setChallenge(null); setCode(""); setPassword(""); setConfirmation("");
          setNow(Date.now()); setNotice(""); setView("reset");
        } else {
          const verified = await customerAuthRequest<RegistrationGrant>("otp/verify", { challengeId: challenge.challengeId, code });
          setRegistrationGrant(verified); setPhone(verified.phone); setNow(Date.now()); setNotice("");
          setChallenge(null); setPassword(""); setConfirmation(""); setCode(""); setView("profile");
        }
        return;
      }
      if (view === "reset" && grant) {
        if (resetSeconds === 0) throw new Error("مهلت تغییر رمز تمام شده است. دوباره کد بازیابی بگیرید.");
        if (passwordError(password, confirmation)) throw new Error(passwordError(password, confirmation));
        await customerAuthRequest("password/reset", { resetToken: grant.resetToken, password, confirmation });
        setGrant(null); setPassword(""); setConfirmation(""); setView("success");
      }
    });
  }
  const title = view === "success" ? (recovering ? "رمز عبور تغییر کرد" : "ثبت‌نام تکمیل شد؛ وارد شوید")
    : view === "verify" ? "کد تأیید را وارد کن" : view === "reset" ? "رمز جدید را انتخاب کن"
    : view === "register" ? "ثبت‌نام با شماره موبایل" : view === "profile" ? "اطلاعات حساب را تکمیل کن"
    : recovering ? "بازیابی رمز عبور" : "ورود به حساب";
  const description = view === "success" ? "برای ادامه، با شماره موبایل و رمز عبور وارد شوید."
    : recovering ? "بازیابی خودکار برای شماره‌های تأییدشده فعال است. اگر شماره قبلاً تأیید نشده، ابتدا با رمز فعلی وارد شوید یا با پشتیبانی تماس بگیرید."
    : view === "register" ? "ابتدا شماره موبایل خود را وارد کنید."
    : view === "profile" ? "شماره موبایل تأیید شد. برای تکمیل ثبت‌نام، نام و رمز حساب را تعیین کنید."
    : "برای ورود، شماره موبایل و رمز عبور لازم است.";
  const registrationLink = <Link prefetch={false} href={"/customer?mode=register&next=" + encodeURIComponent(next)}
    className="text-link" aria-disabled={busy} onNavigate={event => {
      event.preventDefault();
      if (!sending.current) { setRecovering(false); switchView("register"); }
    }}>ثبت‌نام کنید</Link>;
  return <div className="entry-form-panel">
    <span className="step-pill">{recovering ? "بازیابی حساب" : "ورود / ثبت‌نام"}</span>
    <h1>{title}</h1><p>{description}</p>
    {!recovering && ["password", "register", "profile"].includes(view) && <div className="auth-methods" aria-label="ورود یا ثبت‌نام">
      <button type="button" aria-pressed={view === "password"} disabled={busy} onClick={() => switchView("password")}>ورود</button>
      <button type="button" aria-pressed={view !== "password"} disabled={busy} onClick={() => switchView("register")}>ثبت‌نام</button>
    </div>}
    {view === "success" ? <div className="auth-success" role="status">
      <ShieldCheck size={42} aria-hidden="true" />
      {recovering ? <Link prefetch={false} className="button button-full" href={loginUrl}>ورود<ArrowLeft size={18} /></Link>
        : <button type="button" disabled={busy} className="button button-full" onClick={() => switchView("password")}>ورود<ArrowLeft size={18} /></button>}
    </div> : <form key={view} onSubmit={submit} aria-busy={busy}
      onInput={event => { if (event.target instanceof HTMLInputElement) event.target.setCustomValidity(""); }}
      onInvalid={event => {
        if (!(event.target instanceof HTMLInputElement)) return;
        const input = event.target;
        input.setCustomValidity(input.name === "phone" ? "شماره موبایل ۱۱ رقمی با ۰۹ را وارد کنید."
          : input.name === "code" ? "کد تأیید را با تعداد رقم خواسته‌شده وارد کنید."
          : input.name === "password" && (view === "profile" || view === "reset") ? PASSWORD_GUIDANCE
          : input.name === "password" || input.name === "confirmation" ? "رمز عبور و تکرار آن را وارد کنید."
          : "این قسمت را کامل و معتبر وارد کنید.");
      }}>
      {["password", "register", "request"].includes(view) && <label className="field">شماره موبایل
        <div className="phone-input"><Smartphone size={20} aria-hidden="true" /><input name="phone" type="tel" autoComplete="tel-national" required pattern="09[0-9]{9}" maxLength={11} placeholder="09XXXXXXXXX" dir="ltr" value={phone} disabled={busy} onChange={e => setPhone(normalizeDigits(e.target.value))} /></div>
      </label>}
      {view === "profile" && <><div className="auth-profile-phone"><span>شماره موبایل: <bdi>{phone}</bdi></span><button type="button" disabled={busy} className="text-link" onClick={() => switchView("register")}>شروع دوباره</button></div><div className="entry-name-grid">
        <label className="field">نام<input name="firstName" autoComplete="given-name" required minLength={2} maxLength={80} value={firstName} disabled={busy} onChange={e => setFirstName(e.target.value)} /></label>
        <label className="field">نام خانوادگی<input name="lastName" autoComplete="family-name" required minLength={2} maxLength={80} value={lastName} disabled={busy} onChange={e => setLastName(e.target.value)} /></label>
      </div></>}
      {view === "password" && <label className="field">رمز عبور
        <input name="password" type="password" dir="ltr" autoComplete="current-password" required minLength={1} maxLength={128} value={password} disabled={busy} onChange={e => setPassword(e.target.value)} />
      </label>}
      {(view === "profile" || view === "reset") && <NewPasswordFields purpose={view === "profile" ? "initial" : "change"} password={password} confirmation={confirmation} onPassword={setPassword} onConfirmation={setConfirmation} disabled={busy} />}
      {view === "verify" && challenge && <>
        <p className="auth-phone">شماره موبایل: <bdi>{phone}</bdi></p>
        <label className="field">کد تأیید<input ref={codeInput} className="otp-code-input" name="code" dir="ltr" inputMode="numeric" autoComplete="one-time-code" required pattern={"[0-9]{" + challenge.codeLength + "}"} maxLength={challenge.codeLength} value={code} onChange={e => setCode(normalizeDigits(e.target.value).replace(/[^0-9]/g, ""))} aria-describedby="otp-validity" /></label>
        <p id="otp-validity" className="auth-timer">{codeSeconds > 0 ? "اعتبار کد: " + codeSeconds.toLocaleString("fa-IR") + " ثانیه" : "مهلت کد تمام شده است؛ دوباره کد بگیرید."}</p>
      </>}
      {view === "reset" && <p className="auth-timer">{resetSeconds > 0 ? "مهلت تغییر رمز: " + resetSeconds.toLocaleString("fa-IR") + " ثانیه" : "مهلت تمام شده است. دوباره کد بگیرید."}</p>}
      {view === "profile" && <p className="auth-timer" role="status">{registrationSeconds > 0 ? "مهلت تکمیل ثبت‌نام: " + registrationSeconds.toLocaleString("fa-IR") + " ثانیه" : "مهلت تمام شده است. دوباره شماره موبایل را تأیید کنید."}</p>}
      {notice && <p className="auth-notice" role="status">{notice}</p>}
      {error && <p className="error-message" role="alert">{recovering && errorCode === RECOVERY_ACCOUNT_NOT_FOUND
        ? <>حساب کاربری با این شماره موبایل پیدا نشد. برای ایجاد حساب جدید، لطفاً {registrationLink}.</>
        : view === "password" && errorCode === CUSTOMER_LOGIN_ERRORS.accountNotFound
          ? <>حساب کاربری با این شماره موبایل پیدا نشد. لطفاً ابتدا {registrationLink}.</> : error}</p>}
      <button type="submit" className="button button-full" disabled={busy || (view === "verify" && codeSeconds === 0) || (view === "reset" && resetSeconds === 0) || (view === "profile" && (!registrationGrant || registrationSeconds === 0)) || (["register", "request"].includes(view) && resendSeconds > 0)}>
        {busy ? "در حال انجام…" : view === "verify" ? "تأیید کد و ادامه" : view === "reset" ? "ثبت رمز جدید" : view === "register" ? "ادامه ثبت‌نام" : view === "profile" ? "تکمیل ثبت‌نام" : view === "request" ? "دریافت کد تأیید" : "ورود"}<ArrowLeft size={18} />
      </button>
      {view === "verify" && <div className="auth-resend"><button type="button" className="text-link" disabled={busy || resendSeconds > 0} onClick={() => void perform(sendCode)}>ارسال مجدد کد</button>{resendSeconds > 0 && <span>ارسال دوباره پس از {resendSeconds.toLocaleString("fa-IR")} ثانیه</span>}</div>}
      {view === "verify" && <button type="button" className="text-link auth-alternative" disabled={busy} onClick={() => switchView(recovering ? "request" : "register")}>اصلاح اطلاعات</button>}
      {view === "profile" && errorCode === "REGISTRATION_GRANT_INVALID" && <button type="button" disabled={busy} className="text-link auth-alternative" onClick={() => switchView("register")}>تأیید دوباره شماره موبایل</button>}
      {view === "reset" && resetSeconds === 0 && <button type="button" className="text-link" onClick={() => switchView("request")}>دریافت کد بازیابی جدید</button>}
      {view === "password" && <Link prefetch={false} className="text-link auth-alternative" href={"/customer/forgot-password?next=" + encodeURIComponent(next)} onNavigate={event => {
        if (recovery) { event.preventDefault(); setRecovering(true); switchView("request"); }
      }}>رمز عبور را فراموش کرده‌ام</Link>}
      {recovering && <Link prefetch={false} className="text-link auth-alternative" href={loginUrl}>بازگشت به ورود</Link>}
    </form>}
    {view === "password" && <p className="auth-alternative">حساب کاربری ندارید؟ {registrationLink}</p>}
    <Link prefetch={false} href="/" className="text-link entry-back">بازگشت به صفحه اصلی</Link>
  </div>;
}
