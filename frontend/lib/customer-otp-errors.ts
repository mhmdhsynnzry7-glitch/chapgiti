export const RECOVERY_ACCOUNT_NOT_FOUND = "CUSTOMER_ACCOUNT_NOT_FOUND";

const messages: Readonly<Record<string, string>> = {
  CUSTOMER_ACCOUNT_NOT_FOUND: "حساب کاربری با این شماره موبایل پیدا نشد. برای ایجاد حساب جدید، لطفاً ثبت‌نام کنید.",
  CUSTOMER_RECOVERY_UNAVAILABLE: "اطلاعات این حساب کامل نیست. ابتدا با رمز عبور وارد شوید و مشخصات حساب را تکمیل کنید.",
  CUSTOMER_PHONE_UNVERIFIED: "شماره موبایل این حساب هنوز تأیید نشده است. با رمز فعلی وارد شوید و شماره را تأیید کنید یا با پشتیبانی تماس بگیرید.",
  OTP_INCORRECT: "کد تأیید واردشده صحیح نیست. لطفاً دوباره تلاش کنید.",
  OTP_EXPIRED: "مهلت کد تأیید تمام شده است. لطفاً کد جدید بگیرید.",
  OTP_CHALLENGE_MISSING: "درخواست تأیید پیدا نشد. شماره موبایل را دوباره وارد کنید و کد جدید بگیرید.",
  OTP_CONSUMED: "این کد قبلاً استفاده شده است. لطفاً کد جدید بگیرید.",
  OTP_ATTEMPT_LIMIT: "تعداد تلاش‌های مجاز برای این کد تمام شده است. لطفاً کد جدید بگیرید.",
  OTP_CHALLENGE_INVALID: "این درخواست تأیید دیگر معتبر نیست. لطفاً کد جدید بگیرید.",
  SMS_DELIVERY_FAILED: "ارسال پیامک انجام نشد. کمی بعد دوباره تلاش کنید.",
};

export function customerOtpErrorMessage(code: string | null, fallback: string): string {
  return code !== null && Object.hasOwn(messages, code) ? messages[code] : fallback;
}

export function recoveryNeedsRestart(code: string | null): boolean {
  return code !== null && code !== "OTP_INCORRECT" && Object.hasOwn(messages, code);
}
