# خطاهای قابل پیگیری چاپ گیتی

درخواست ناموفق API متن فارسی را در `error` و شناسهٔ پایدار را در `code` برمی‌گرداند. `requestId` با هدر `X-Request-Id` همان پاسخ برابر است. رابط بر اساس وضعیت HTTP و کد تصمیم می‌گیرد. برای خطاهای OTP، ورود و تعرفه، کد تخصصی موجود اولویت دارد.

| HTTP | کد نمونه | رفتار |
|---|---|---|
| 400 | `VALIDATION_REQUIRED`، `VALIDATION_MIN_LENGTH`، `VALIDATION_MAX_LENGTH`، `PHONE_INVALID` | اصلاح فیلد و ارسال مجدد |
| 400 | `TICKET_ATTACHMENT_INVALID_TYPE`، `TICKET_ATTACHMENT_COUNT`، `TICKET_ATTACHMENT_NAME` | انتخاب فایل با نوع، نام و تعداد مجاز |
| 401 / 403 | `AUTH_UNAUTHORIZED`، `AUTH_FORBIDDEN` | ورود با نقش مربوط یا بررسی مجوز |
| 404 | `RESOURCE_NOT_FOUND` | بررسی نشانی یا مالکیت منبع |
| 409 | `ORDER_VERSION_CONFLICT`، `QUOTE_STALE`، `FINAL_TERMS_STALE`، `FINAL_TERMS_UNCHANGED`، `TARIFF_CONFLICT` | دریافت نسخهٔ تازه و مرور دوبارهٔ اقدام |
| 409 | `TICKET_MESSAGE_CONFLICT` | کلید هر ارسال به متن و پیوست‌های همان درخواست تعلق دارد |
| 413 / 415 | `TICKET_ATTACHMENT_TOO_LARGE`، `REQUEST_TOO_LARGE`، `UNSUPPORTED_MEDIA` | کاهش حجم یا انتخاب فرمت مجاز |
| 429 | `RATE_LIMITED` یا کد اختصاصی OTP | رعایت `Retry-After` |
| 500 / 502 / 503 | `INTERNAL_SERVER_ERROR`، `UPSTREAM_UNAVAILABLE`، `SERVICE_UNAVAILABLE` | پیگیری با شناسهٔ درخواست |

## فایل خطا

بک‌اند خطاهای داخلی و خطاهای ۵۰۳ دریافتی از سرویس کاربردی را در `Storage:Root/logs/errors-YYYYMMDD-NNNNNN.jsonl` ثبت می‌کند. در نصب معمول ویندوز، این پوشه `C:\Giti\data\logs` است. فیلدهای مجاز زمان UTC، سطح، کد، شناسهٔ درخواست، نام کنترلر و عملیات و وضعیت HTTP هستند. خطای اتصال در پروکسی Next شناسهٔ محلی خود را دارد. وقتی اتصال به بک‌اند برقرار نشده باشد، رویداد متناظری در فایل بک‌اند وجود ندارد.

`Diagnostics__FileBytes` به‌طور پیش‌فرض ۱ MiB و `Diagnostics__RetainFiles` به‌طور پیش‌فرض ۱۲ است. حد پیکربندی ۱۰۲۴ بایت تا ۱۶ MiB و ۲ تا ۶۰ فایل است. تغییر تنظیم با راه‌اندازی دورهٔ بعدی فرایند اعمال می‌شود. فایل با تغییر روز UTC یا رسیدن به حد حجم عوض می‌شود. شکست نوشتن فایل روی پاسخ اصلی اثر نمی‌گذارد. لاگ عادی سرویس و WinSW جداگانه مدیریت می‌شوند.

در نصب دستی، حساب اجرای API باید روی پوشهٔ خصوصی مجوز Modify داشته باشد. دسترسی خواندن را به مدیران مجاز محدود کنید. پوشهٔ داده، پیوست و لاگ نباید Physical Path عمومی IIS باشد. بررسی مجوز فعلی در PowerShell با `icacls "C:\Giti\data\logs"` انجام می‌شود. برای پیگیری از شناسهٔ درخواست استفاده کنید. رمز، شماره موبایل، کوکی، متن فرم، نام فایل و محتوای پیوست ورودی ابزار جست‌وجوی لاگ نیستند.
