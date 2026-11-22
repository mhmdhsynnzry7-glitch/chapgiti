using ChapGiti.Application.Common;

namespace ChapGiti.Application.Authentication;

public static class CustomerOtpErrors
{
    public static UseCaseException AccountNotFound() => new(ErrorKind.NotFound,
        "حساب کاربری با این شماره موبایل پیدا نشد. برای ایجاد حساب جدید، لطفاً ثبت‌نام کنید.", code: CustomerLoginErrors.AccountNotFound);
    public static UseCaseException IncompleteAccount() => new(ErrorKind.Forbidden,
        "اطلاعات این حساب کامل نیست. ابتدا با رمز عبور وارد شوید و مشخصات حساب را تکمیل کنید.", code: "CUSTOMER_RECOVERY_UNAVAILABLE");
    public static UseCaseException UnverifiedPhone() => new(ErrorKind.Forbidden,
        "شماره موبایل این حساب هنوز تأیید نشده است. با رمز فعلی وارد شوید و شماره را تأیید کنید یا با پشتیبانی تماس بگیرید.", code: "CUSTOMER_PHONE_UNVERIFIED");
    public static UseCaseException Incorrect() => new(ErrorKind.Invalid,
        "کد تأیید واردشده صحیح نیست. لطفاً دوباره تلاش کنید.", code: "OTP_INCORRECT");
    public static UseCaseException Expired() => new(ErrorKind.Invalid,
        "مهلت کد تأیید تمام شده است. لطفاً کد جدید بگیرید.", code: "OTP_EXPIRED");
    public static UseCaseException Missing() => new(ErrorKind.Invalid,
        "درخواست تأیید پیدا نشد. شماره موبایل را دوباره وارد کنید و کد جدید بگیرید.", code: "OTP_CHALLENGE_MISSING");
    public static UseCaseException Consumed() => new(ErrorKind.Invalid,
        "این کد قبلاً استفاده شده است. لطفاً کد جدید بگیرید.", code: "OTP_CONSUMED");
    public static UseCaseException AttemptLimit() => new(ErrorKind.Invalid,
        "تعداد تلاش‌های مجاز برای این کد تمام شده است. لطفاً کد جدید بگیرید.", code: "OTP_ATTEMPT_LIMIT");
    public static UseCaseException InvalidChallenge() => new(ErrorKind.Invalid,
        "این درخواست تأیید دیگر معتبر نیست. لطفاً کد جدید بگیرید.", code: "OTP_CHALLENGE_INVALID");
    public static UseCaseException DeliveryFailed() => new(ErrorKind.Unavailable,
        "ارسال پیامک انجام نشد. کمی بعد دوباره تلاش کنید.", code: "SMS_DELIVERY_FAILED");
}
