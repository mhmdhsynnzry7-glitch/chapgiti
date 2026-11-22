# نصب و ارتقای دیتابیس چاپ گیتی

این راهنما برای Release ویندوز با کاتالوگ موجود `ChapGiti` است. اجرای هر تغییر ساختار به تأیید مقصد، فهرست تغییرات و بکاپ قابل بازیابی همان محیط نیاز دارد. بررسی بسته و اجرای معمول برنامه، migration انجام نمی‌دهند.

## ابزارهای داخل بسته

`backend/publish/ChapGiti.Api.exe` یک برنامهٔ خودکفای win-x64 است. `scripts/db-setup.mjs` تنظیمات همان نصب را می‌خواند و فقط با دستور دستی، همین برنامه را با آرگومان واقعی `--migrate` فراخوانی می‌کند. SqlClient و کتابخانه‌های EF Core، SQL Server و Relational در همان پوشه‌اند. خط مبنای اعتبارسنجی و دادهٔ اولیهٔ کاتالوگ در `ChapGiti.Infrastructure.dll` تعبیه شده‌اند. یک کپی خواندنی و هش متناظر هر دو در بسته وجود دارد. به سورس، SDK، dotnet-ef یا EF bundle جداگانه روی مقصد نیازی نیست.

| مالک تاریخچه | ترتیب در سورس بررسی‌شده |
|---|---|
| `dbo.Schema_Migrations` | `001_initial.sql`, `002_customer_otp.sql`, `003_unified_authentication.sql`, `004_administrator_login_email.sql`, `005_order_support.sql`, `006_order_quotes.sql`, `007_in_app_notifications.sql`, `008_order_corrections.sql`, `009_order_views.sql` |
| `dbo.__EFMigrationsHistory` | `20261117090230_Legacy009Baseline`, `20261117092337_CatalogAndMagazine`, `20261118113729_TicketMessageReadState` |

مالکیت اسکریپت‌های legacy در ۰۰۹ پایان می‌یابد. runner ابتدا نام و checksum فایل‌ها را می‌سنجد و فقط اسکریپت‌های معوق را با قفل و تراکنش ثبت می‌کند. قفل جداگانهٔ EF و اعتبارسنجی کل schema قدیمی قبل از اجرای migrationهای معوق EF انجام می‌شوند. baseline جدول‌های legacy را دوباره نمی‌سازد. migration کاتالوگ گزینه‌های اولیه را درج و وضعیت انتشار مقاله‌های قدیمی را از دادهٔ قبلی استخراج می‌کند. migration پشتیبانی ستون nullable `dbo.Ticket_Messages.read_at datetime2(3)` را اضافه می‌کند.

## نصب تازه، ارتقا و نسخه‌های بعدی

1. از پوشهٔ تازه‌استخراج‌شده و پیش از قراردادن فایل‌های خصوصی، `node.exe .\scripts\verify-release.mjs` را اجرا کنید. برای مشاهدهٔ موجودی **بدون اتصال SQL** از `.\backend\publish\ChapGiti.Api.exe --release-metadata` استفاده کنید. هش بایت خام و checksum یکسان‌سازی‌شدهٔ SQL در `MIGRATIONS-CHECKSUMS.json` درج می‌شوند.
2. در نصب تازه، DBA باید بانک خالی **از قبل موجود** را روی مقصد تأیید کند. در نصب داده‌دار، history باید پیشوند مرتب نسخه‌های داخل بسته باشد. توقف در نبود بانک، checksum متفاوت، تاریخچهٔ ناشناخته، schema ناسازگار یا مقصد متفاوت الزامی است. راهنمای مقصد دستورهای دقیق را دارد. [پذیرش SQL Server 2025](SQL2025-ACCEPTANCE-FA.md)
3. در نسخه‌های بعدی، fingerprint و آرایهٔ `compiled.ef` از همان باینری بسته را بررسی کنید. runner همهٔ pending همان نسخه را به ترتیب اعمال می‌کند. اگر مجوز تنها بخشی از آن‌ها را پوشش می‌دهد، دستور را اجرا نکنید. از SQL تکی، درج دستی baseline، import یا اجرای موازی ابزار EF استفاده نکنید.
4. پس از تأیید صریح همان مجموعه، بکاپ و بررسی آن، توقف نویسنده‌های آن محیط و موفقیت مرحلهٔ Before، این دستور را **در همان PowerShell و با همان اتصال** اجرا کنید. هیچ یک از این فرمان‌های تغییر ساختار در آماده‌سازی بسته اجرا نمی‌شود.

```powershell
Set-Location C:\Giti\app
$env:NODE_ENV='production'
node.exe .\scripts\db-setup.mjs
if ($LASTEXITCODE -ne 0) { throw 'Migration runner failed. Inspect history before retrying.' }
.\deploy\sql\check-target.ps1 -ExpectedServer $expectedServer -Phase After
```

در حالت دیتابیس کاملاً به‌روز، گزارش Before فهرست معوق خالی می‌دهد و می‌توان مستقیم After را اجرا کرد. در شکست، history و schema را دوباره بخوانید. اجرای دوباره با همان بسته فقط پس از رفع علت، بررسی محدوده و تأیید مالک انجام گیرد. بازگشت باینری و بازیابی دیتابیس دو تصمیم جدا هستند. ایجاد مدیر جدید یا تغییر رمز، عمل provisioning جداگانه‌ای است.
