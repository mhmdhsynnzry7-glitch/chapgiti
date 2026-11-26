# راه‌اندازی توسعه و تشخیص خطاهای دیتابیس

راه‌انداز ریشه، فرانت Next.js و API دات‌نت را روی loopback اجرا می‌کند. برای نصب وابستگی‌ها و ساخت فایل خصوصی `.env.local`، دستورهای [شروع توسعه](../README.md) را اجرا کنید. SQL Server محلی باید در دسترس باشد و هویت ویندوز یا حساب خصوصی انتخاب‌شده به دیتابیس توسعه دسترسی داشته باشد.

1. مقدار `SQLSERVER_CONNECTION_STRING` را در فایل خصوصی تنظیم کنید. راه‌انداز توسعهٔ اصلی instance پیش‌فرض همین ماشین و دیتابیس موجود `ChapGiti` را بررسی می‌کند.
2. با مجوز خواندنی، نام instance، نام دیتابیس و وضعیت migrationهای SQL و EF آن را بررسی کنید. برای تفسیر baseline و ترتیب اجرای migrationها از [راهنمای migration](DATABASE-MIGRATION-FA.md) استفاده کنید. اعمال migration یک عملیات جداگانه است و به تأیید مقصد، سابقه و دامنهٔ تغییر نیاز دارد.
3. از ریشه در PowerShell دستور `npm.cmd run dev` را اجرا کنید. اگر preflight خطا داد، اولین خطای API را بخوانید. برای خطای `DATABASE_CONFIG` وجود تنظیم اتصال، هویت فرایند و دسترسی به بانک را بررسی کنید. برای اختلاف تاریخچه یا checksum، ابتدا فایل نسخه‌دار و وضعیت واقعی بانک را مقایسه کنید.
4. آدرس پیش‌فرض فرانت `http://127.0.0.1:3000` و آدرس API `http://127.0.0.1:5100` است. دستور `npm.cmd run dev:status` وضعیت فرایندهای همین checkout را نشان می‌دهد و `npm.cmd run dev:stop` آن‌ها را متوقف می‌کند.

برای اجرای آزمون‌های حافظه‌ای، دستورات [ساخت و آزمون محلی](../README.md) را اجرا کنید. شواهد، کلیدها و اطلاعات محیطی متعلق به هر نصب باید خصوصی بمانند.

### تشخیص سورس و فرایند فعال در ویندوز

در این محیط پوشهٔ اجرای روزمره `D:\Project\chap\naghsh-publishing` است. پیش از مقایسهٔ رابط با نسخهٔ انتشار، در PowerShell همان پوشه را باز کنید.

```powershell
Set-Location 'D:\Project\chap\naghsh-publishing'
git status --short --branch
git rev-parse HEAD
git worktree list
npm.cmd run dev:status
```

پورت‌های ۳۰۰۰ و ۵۱۰۰ باید به فرانت و بک‌اند همین checkout تعلق داشته باشند. برای جابه‌جایی بین checkoutها، `npm.cmd run dev:stop` را در پوشه‌ای اجرا کنید که `dev:status` آن فرایندها را متعلق به پروژه تشخیص داده است. سپس در پوشهٔ روزمره `npm.cmd run dev` را اجرا کنید. این فرمان بک‌اند Debug را از سورس می‌سازد و Next را در حالت توسعه اجرا می‌کند. اجرای بستهٔ استخراج‌شده با `npm.cmd start` از فایل‌های منتشرشدهٔ همان بسته استفاده می‌کند.

اگر `MIGRATION_HISTORY` می‌بینید، ابتدا مقصد اتصال، نسخهٔ اسمبلی و ترتیب history را با سورس تطبیق دهید. `MIGRATION_REQUIRED` به مهاجرت معتبرِ معوق اشاره دارد. برای اجرای آن، ابتدا Full Backup با CHECKSUM و سپس RESTORE VERIFYONLY WITH CHECKSUM را روی مقصد تأییدشده انجام دهید و از runner رسمی استفاده کنید. وقتی تعداد معوق صفر است، مسیر اجرا مستقیماً به تست و راه‌اندازی می‌رسد.

برای بررسی ورود، نام کامل و پیوست‌های پشتیبانی با Chrome و حالت توسعهٔ Next، فرمان زیر از اعتبارنامه و SQL یک‌بارمصرف آزمون استفاده می‌کند. متغیر فقط در فرایند PowerShell فرزند تنظیم می‌شود.

```powershell
powershell.exe -NoProfile -Command { $env:CHAP_SQL_BROWSER_DEVELOPMENT='true'; dotnet run --project tests/sql005 -c Verification -- --authorize-disposable-ef-activation; exit $LASTEXITCODE }
```
