"use client";
import { useEffect, useId, useRef } from "react";
import { PASSWORD_GUIDANCE, PASSWORD_MIN_LENGTH, PASSWORD_MAX_LENGTH, PASSWORD_PATTERN, validNewPassword } from "@/lib/password-policy";

export default function NewPasswordFields({ password, confirmation, onPassword, onConfirmation, disabled = false, purpose = "change" }: {
  password: string; confirmation: string; onPassword: (value: string) => void;
  onConfirmation: (value: string) => void; disabled?: boolean; purpose?: "initial" | "change";
}) {
  const id = useId(), passwordInput = useRef<HTMLInputElement>(null), confirmationInput = useRef<HTMLInputElement>(null);
  const invalid = !!password && !validNewPassword(password), mismatch = !!confirmation && confirmation !== password;
  useEffect(() => {
    passwordInput.current?.setCustomValidity(""); confirmationInput.current?.setCustomValidity("");
  }, [password, confirmation]);
  return <>
    <label className="field" htmlFor={id + "-password"}><span id={id + "-password-label"}>{purpose === "initial" ? "رمز عبور" : "رمز عبور جدید"}</span>
      <input ref={passwordInput} id={id + "-password"} aria-labelledby={id + "-password-label"} name="password" type="password" dir="ltr" autoComplete="new-password" required
        minLength={PASSWORD_MIN_LENGTH} maxLength={PASSWORD_MAX_LENGTH} pattern={PASSWORD_PATTERN}
        value={password} disabled={disabled} onChange={e => onPassword(e.target.value)}
        aria-describedby={id + "-guidance"} aria-invalid={invalid || undefined}
        onInvalid={e => e.currentTarget.setCustomValidity(PASSWORD_GUIDANCE)} />
      <small id={id + "-guidance"} className={invalid ? "error-message" : undefined}>{PASSWORD_GUIDANCE}</small>
    </label>
    <label className="field" htmlFor={id + "-confirmation-input"}><span id={id + "-confirmation-label"}>{purpose === "initial" ? "تکرار رمز عبور" : "تکرار رمز عبور جدید"}</span>
      <input ref={confirmationInput} id={id + "-confirmation-input"} aria-labelledby={id + "-confirmation-label"} name="confirmation" type="password" dir="ltr" autoComplete="new-password" required
        minLength={PASSWORD_MIN_LENGTH} maxLength={PASSWORD_MAX_LENGTH} value={confirmation} disabled={disabled}
        onChange={e => onConfirmation(e.target.value)} aria-invalid={mismatch || undefined}
        aria-describedby={mismatch ? id + "-confirmation" : undefined}
        onInvalid={e => e.currentTarget.setCustomValidity("تکرار رمز عبور را وارد کنید.")} />
      {mismatch && <small className="error-message" id={id + "-confirmation"}>تکرار رمز عبور یکسان نیست.</small>}
    </label>
  </>;
}
