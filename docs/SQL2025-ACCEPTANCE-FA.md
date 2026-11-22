# پذیرش دستی SQL Server 2025 Express روی Windows Server

پذیرش SQL Server 2025 Express با سطح سازگاری ۱۷۰ هنوز اجرا نشده است. نسخهٔ provider در سورس ۱۰٫۰٫۱۵ است. تنظیم `UseCompatibilityLevel(150)` در زمان تولید SQL اعمال می‌شود. سطح واقعی بانک را موتور SQL Server گزارش می‌کند. این تنظیم برنامه سطح دیتابیس را تغییر نمی‌دهد. هدف ۱۷۰ با تولید SQL برای ۱۵۰ تعارض شناخته‌شده‌ای در migrationهای بررسی‌شده ندارد. هزینهٔ اجرای query، اختیارات حساب سرویس، collation و صحت اجرای واقعی باید روی مقصد تأیید شوند. [provider رسمی EF](https://learn.microsoft.com/en-us/ef/core/providers/sql-server/) · [نسخهٔ موتور و سطوح پشتیبانی‌شده](https://learn.microsoft.com/en-us/sql/t-sql/statements/alter-database-transact-sql-compatibility-level?view=sql-server-ver17)

## پذیرش بدون نوشتن داده

1. از DBA نام دقیق instance و مقدار مورد انتظار `SERVERPROPERTY('ServerName')` را بگیرید. برای TLS نام اتصال باید با گواهی معتبر مطابقت داشته باشد. در نصب تازه بانک خالی و در ارتقا بانک قبلی `ChapGiti` باید وجود داشته باشد. حساب بررسی به CONNECT، VIEW DEFINITION و SELECT لازم دسترسی دارد. با حساب runtime و DBA جداگانه طبق سیاست مقصد کار کنید.
2. در یک PowerShell عادی روی سرور، ابتدا صحت فایل‌های staging را بررسی کنید. مثال زیر از Windows Authentication استفاده می‌کند. اگر از احراز SQL استفاده می‌کنید، اتصال خصوصی را از محل امن در env همان فرایند تنظیم کنید. اسکریپت مقدار اتصال را چاپ نمی‌کند.

```powershell
Set-Location C:\Giti\app
$expectedServer = Read-Host 'نام دقیق ServerName تأییدشده توسط DBA'
$sqlEndpoint = Read-Host 'نام DNS یا instance منطبق با گواهی TLS'
$cs = [System.Data.SqlClient.SqlConnectionStringBuilder]::new()
$cs['Data Source'] = $sqlEndpoint
$cs['Initial Catalog'] = 'ChapGiti'
$cs['Integrated Security'] = $true
$cs['Encrypt'] = $true
$cs['TrustServerCertificate'] = $false
$env:SQLSERVER_CONNECTION_STRING = $cs.ConnectionString
.\deploy\sql\check-target.ps1 -ExpectedServer $expectedServer -Phase Before
```

3. خروجی باید همان server، بانک ChapGiti، موتور ۱۷.x، edition Express و سطح ۱۷۰ را نشان دهد. checksum هر SQL اعمال‌شده، ترتیب history هر دو مالک و baseline ساختار تا ۰۰۹ بررسی می‌شود. mismatch، مجوز ناکافی، schema بدون تاریخچه یا DB offline نیازمند توقف و بررسی DBA است. هویت SQL، نسخه و collation و شناسه‌های pending را در گزارش مقصد نگه دارید.
4. در صورت وجود pending، پذیرش نهایی تا اجرای فرایند جداگانهٔ مجاز متوقف می‌شود. [دستورهای نصب تازه و ارتقا](DATABASE-MIGRATION-FA.md)
5. وقتی هر دو تاریخچه کامل شد، فرمان زیر را در همان PowerShell و با همان اتصال اجرا کنید. اسکریپت فقط metadata و شمارش‌های تجمعی را برمی‌گرداند. read_at، ایندکس‌های کاتالوگ و مجله، پیوند سفارش با فایل تأییدشده، نسخه‌های قیمت، اسلاگ‌های مجله و پیام‌های خوانده‌نشده بررسی می‌شوند. صفر ردیف در بانک تازه معتبر است.

```powershell
.\deploy\sql\check-target.ps1 -ExpectedServer $expectedServer -Phase After
Remove-Item Env:\SQLSERVER_CONNECTION_STRING
```

برای مجوز و عملکرد API پس از نصب، مسیر عمومی مجله و سلامت را آزمایش کنید. ورود حساب، نوشتن سفارش، تغییر وضعیت خوانده‌شدن یا ارسال پیام به مجوز همان UAT نیاز دارند. نتیجهٔ آزمون SQL محلی قدیمی را به ۲۰۲۵ منتقل نکنید.
