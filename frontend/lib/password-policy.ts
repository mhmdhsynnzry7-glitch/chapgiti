export const PASSWORD_MIN_LENGTH = 6;
export const PASSWORD_MAX_LENGTH = 128;
export const PASSWORD_GUIDANCE = "رمز عبور باید حداقل ۶ کاراکتر، شامل یک حرف بزرگ انگلیسی (A-Z) و یک کاراکتر خاص مانند ! یا @ باشد.";
export const PASSWORD_PATTERN = "(?=.*[A-Z])(?=.*[\\x21-\\x2F\\x3A-\\x40\\x5B-\\x60\\x7B-\\x7E])[\\s\\S]{6,128}";
export function validNewPassword(value: string) {
  return value.length >= PASSWORD_MIN_LENGTH && value.length <= PASSWORD_MAX_LENGTH &&
    /[A-Z]/.test(value) && /[\x21-\x2f\x3a-\x40\x5b-\x60\x7b-\x7e]/.test(value);
}
export function passwordError(value: string, confirmation: string) {
  if (!validNewPassword(value)) return PASSWORD_GUIDANCE;
  return value === confirmation ? "" : "تکرار رمز عبور یکسان نیست.";
}
