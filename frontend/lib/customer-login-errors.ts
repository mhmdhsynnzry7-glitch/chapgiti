export const CUSTOMER_LOGIN_ERRORS = {
  accountNotFound: "CUSTOMER_ACCOUNT_NOT_FOUND",
  incorrectPassword: "CUSTOMER_PASSWORD_INCORRECT",
} as const;

export function customerLoginErrorMessage(code: string | null, fallback: string): string {
  switch (code) {
    case CUSTOMER_LOGIN_ERRORS.accountNotFound:
      return "حساب کاربری با این شماره موبایل پیدا نشد. لطفاً ابتدا ثبت‌نام کنید.";
    case CUSTOMER_LOGIN_ERRORS.incorrectPassword:
      return "رمز عبور واردشده صحیح نیست. لطفاً دوباره تلاش کنید.";
    default:
      return fallback;
  }
}
