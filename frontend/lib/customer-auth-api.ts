import { requireSecureAuthenticationPage, sessionFetch } from "./session-client";
import { customerLoginErrorMessage } from "./customer-login-errors";
import { customerOtpErrorMessage } from "./customer-otp-errors";

export class CustomerAuthError extends Error {
  constructor(message: string, public readonly retryAfterSeconds: number | null = null, public readonly status: number | null = null,
    public readonly code: string | null = null) {
    super(message);
  }
}

export async function customerAuthRequest<T>(path: string, body: object, method = "POST"): Promise<T> {
  requireSecureAuthenticationPage();
  const authenticated = path === "profile" || path === "phone/verify" ||
    (path === "otp/request" && "purpose" in body && body.purpose === "verify-phone");
  let response: Response;
  try {
    response = await (authenticated ? sessionFetch : fetch)("/api/customer/" + path, {
      method, headers: { "Content-Type": "application/json" }, body: JSON.stringify(body), cache: "no-store",
    });
  } catch { throw new CustomerAuthError("ارتباط با سرویس برقرار نشد. دوباره تلاش کنید."); }
  const data = await response.json().catch(() => null);
  if (!response.ok) {
    const code = typeof data?.code === "string" ? data.code : null;
    const message = typeof data?.error === "string" ? data.error : "درخواست انجام نشد. دوباره تلاش کنید.";
    throw new CustomerAuthError(
      path === "login" ? customerLoginErrorMessage(code, message)
        : ["otp/request", "otp/verify", "password/verify", "phone/verify"].includes(path) ? customerOtpErrorMessage(code, message) : message,
      typeof data?.retryAfterSeconds === "number" ? data.retryAfterSeconds : null,
      response.status,
      code,
    );
  }
  if (data === null) throw new CustomerAuthError("پاسخ سرویس قابل پردازش نیست. دوباره تلاش کنید.");
  return data as T;
}

export type OtpChallenge = {
  challengeId: string;
  expiresAt: number;
  resendAfterSeconds: number;
  codeLength: number;
  message: string;
};

export function isOtpChallenge(value: unknown): value is OtpChallenge {
  if (!value || typeof value !== "object") return false;
  const challenge = value as Partial<OtpChallenge>;
  return typeof challenge.challengeId === "string" && /^[a-f0-9]{64}$/.test(challenge.challengeId) &&
    typeof challenge.expiresAt === "number" && Number.isFinite(challenge.expiresAt) && challenge.expiresAt > Date.now() &&
    typeof challenge.resendAfterSeconds === "number" && Number.isFinite(challenge.resendAfterSeconds) && challenge.resendAfterSeconds >= 0 &&
    typeof challenge.codeLength === "number" && Number.isInteger(challenge.codeLength) && challenge.codeLength >= 4 && challenge.codeLength <= 10 &&
    typeof challenge.message === "string";
}

export type PasswordRecoveryGrant = { resetToken: string; expiresAt: number };
export type RegistrationGrant = { registrationToken: string; phone: string; expiresAt: number };
export type RegistrationStart = { ok: boolean; step: "register" | "password"; phone: string };
