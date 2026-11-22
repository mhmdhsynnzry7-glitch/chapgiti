namespace ChapGiti.Application.Authentication;

public static class CustomerLoginErrors
{
    public const string AccountNotFound = "CUSTOMER_ACCOUNT_NOT_FOUND";
    public const string IncorrectPassword = "CUSTOMER_PASSWORD_INCORRECT";

    public const string AccountNotFoundMessage = "حساب کاربری با این شماره موبایل پیدا نشد. لطفاً ابتدا ثبت‌نام کنید.";
    public const string IncorrectPasswordMessage = "رمز عبور واردشده صحیح نیست. لطفاً دوباره تلاش کنید.";
}
