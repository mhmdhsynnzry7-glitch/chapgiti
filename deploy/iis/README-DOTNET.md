# انتشار چاپ گیتی روی Windows Server و IIS

> این دستورالعمل سابقهٔ نصب قبلی است. دستور جاری ساخت تک‌ZIP و بررسی آفلاین، نصب Giti و کنترل‌های مقصد در راهنمای Release و [نصب ویندوز](../../docs/WINDOWS-SERVER-INSTALL-FA.md) قرار دارد.

> یادداشت نسخهٔ نوسازی: این راهنما تنظیم تاریخی HTTP روی 8081 را هم ثبت کرده است. ورود، ثبت‌نام و بازیابی در نسخهٔ جدید فقط با HTTPS کار می‌کنند. migration افزایشی 003 باید جداگانه بازبینی شود؛ شروع برنامه دیگر migration خودکار ندارد. در مرحلهٔ نوسازی هیچ publish، نصب IIS، تغییر binding یا migration عملیاتی انجام نشده است. قبل از انتشار آینده، گزارش نوسازی و [راهنمای احراز هویت](../../docs/CUSTOMER-AUTH-OTP-FA.md) را بخوانید.


آدرس عمومی فعلی HTTP روی پورت 8081 و IP برابر 203.0.113.10 است. فرانت روی 127.0.0.1:3000 و API روی 127.0.0.1:5100 اجرا می‌شوند. دیتابیس موجود ChapGiti حفظ می‌شود و دیتابیس جدیدی ساخته نمی‌شود.

## گرفتن خروجی هر دو برنامه

پیش‌نیاز کامپیوتر توسعه: Node.js 24 و .NET SDK 10.

```powershell
cd C:\src\chapgiti
npm.cmd --prefix frontend ci
npm.cmd run build
npm.cmd run package:iis -- --release-only
```

خروجی‌ها:

- frontend/publish: فرانت مستقل شامل server.js، وابستگی‌های اجرا، public و .next/static.
- backend/publish: بک‌اند Release برای Windows x64 همراه Runtime دات‌نت.
- outputs/chap-giti-dotnet-release-*.zip: بستهٔ آمادهٔ سرور.
- بسته‌ساز جاری در هر دو شکل فراخوانی فقط Release ZIP تولید می‌کند.
- outputs/latest-dotnet-package.json: مسیر آخرین بسته‌ها.

اگر وابستگی‌ها تغییر نکرده‌اند، برای دفعات بعد اجرای npm ci لازم نیست. رمزها، فایل‌های .env شخصی و داده‌های شما داخل ZIP قرار نمی‌گیرند.

## پابلیش جداگانهٔ فرانت

```powershell
cd C:\src\chapgiti\frontend
npm.cmd ci
npm.cmd run publish
```

خروجی کامل frontend/publish را منتقل کنید. اسکریپت publish فایل‌های public و .next/static را نیز کپی و فایل‌های .env را از خروجی حذف می‌کند.

برای اجرای مستقل همین خروجی، در ترمینال فرانت:

```powershell
cd C:\src\chapgiti\frontend\publish
$env:NODE_ENV='production'
$env:HOSTNAME='127.0.0.1'
$env:PORT='3000'
$env:SITE_URL='http://127.0.0.1:3000'
$env:DOTNET_API_URL='http://127.0.0.1:5100'
node .\server.js
```

## پابلیش جداگانهٔ بک‌اند

```powershell
cd C:\src\chapgiti
dotnet publish .\backend\ChapGiti.Api\ChapGiti.Api.csproj -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=10.0.13 -o .\backend\publish
```

پابلیش پروژهٔ API، اسمبلی‌های Domain و Application و Infrastructure را هم همراه آن قرار می‌دهد. همهٔ محتویات backend/publish را منتقل کنید.

برای اجرای مستقل در ترمینال دیگری:

```powershell
cd C:\src\chapgiti\backend\publish
$env:ASPNETCORE_ENVIRONMENT='Production'
$env:ASPNETCORE_URLS='http://127.0.0.1:5100'
$env:Site__Url='http://127.0.0.1:3000'
$env:Storage__Root='C:\src\chapgiti\data'
$env:ConnectionStrings__SqlServer='Server=.;Database=ChapGiti;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;'
.\ChapGiti.Api.exe
```

نمونه برای اتصال ویندوزی به SQL Server محلی است. روی سرور از تنظیمات همان سرور استفاده کنید. فایل‌های SQL و defaults.json در پوشهٔ Data خروجی API قرار می‌گیرند. ساخت خودکار دیتابیس غیرفعال است.

از ریشه نیز npm.cmd run publish:frontend و npm.cmd run publish:backend هر برنامه را جداگانه پابلیش می‌کنند. پس از آماده شدن هر دو خروجی، npm.cmd run package:iis -- --release-only بستهٔ یکپارچهٔ Release را می‌سازد.

## پیش‌نیازهای سرور

- Windows Server x64، Node.js 24 و دسترسی به دیتابیس موجود ChapGiti.
- IIS، IIS Management Scripts and Tools، URL Rewrite و Application Request Routing.
- گزینهٔ Enable Proxy در ARR فعال باشد.
- WinSW برای نصب اولیهٔ سرویس. سرویس NaghshNode قبلی در به‌روزرسانی حفظ می‌شود.

در این روش API با فایل اجرایی self-contained اجرا می‌شود؛ نصب SDK یا Runtime دات‌نت روی VPS لازم نیست. فرانت همچنان Node.js نیاز دارد. npm ci روی ZIP آمادهٔ سرور لازم نیست.

منابع رسمی:

- https://nextjs.org/docs/app/api-reference/config/next-config-js/output
- https://learn.microsoft.com/en-us/dotnet/core/deploying/
- https://learn.microsoft.com/en-us/iis/extensions/url-rewrite-module/reverse-proxy-with-url-rewrite-v2-and-application-request-routing
- https://github.com/winsw/winsw/releases/tag/v2.12.0

## انتقال بسته به سرور

برای به‌روزرسانی، ابتدا از برنامه و تنظیمات فعلی نسخهٔ پشتیبان بگیرید. سپس در PowerShell مدیر، فقط سرویس همین پروژه را متوقف کنید:

```powershell
if (Get-Service -Name NaghshNode -ErrorAction SilentlyContinue) {
    Stop-Service -Name NaghshNode
}
```

ZIP آماده را استخراج و محتویات آن را داخل C:\Naghsh\app کپی کنید. فایل .env.local، فایل‌های deploy/iis/NaghshNode.exe و NaghshNode.xml و پوشهٔ C:\Naghsh\data را حفظ کنید.

ساختار نهایی:

```text
C:\Naghsh\app\
  frontend\server.js
  frontend\node_modules\
  frontend\.next\
  frontend\public\
  backend\publish\ChapGiti.Api.exe
  scripts\
  deploy\iis\
  package.json
  .env.local
```

## تنظیم محیط سرور

در نصب اولیه فایل نمونه را کپی کنید. در نصب موجود همان .env.local را ویرایش کنید:

```powershell
cd C:\Naghsh\app
if (-not (Test-Path -LiteralPath .env.local)) {
    Copy-Item -LiteralPath .env.example -Destination .env.local
}
notepad .env.local
```

تنظیمات اصلی:

```dotenv
SITE_URL=http://203.0.113.10:8081
PORT=3000
DOTNET_API_URL=http://127.0.0.1:5100
DATA_DIR=C:/Naghsh/data
ALLOW_HTTP=true
TRUST_IIS_PROXY=true
SQLSERVER_CONNECTION_STRING=Server=.;Database=ChapGiti;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;
SMS_ENABLED=false
INDEXING_ENABLED=false
```

برای SQL Express مقدار Server را مانند .\SQLEXPRESS تغییر دهید. تنظیمات قبلی پیامک و سایر گزینه‌های محیط خود را حفظ کنید.

اگر SQL Server روی همین VPS است و حساب مدیریتی ویندوز به SQL دسترسی دارد:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\deploy\iis\Configure-SqlServer.ps1 -AppRoot C:\Naghsh\app -ServerName . -Database ChapGiti -DataDirectory C:\Naghsh\data
```

این دستور فقط ساختار همین دیتابیس موجود، دسترسی LocalService و مجوز فایل‌ها را تنظیم می‌کند. برای SQL Authentication یا SQL Server روی دستگاه دیگر، اتصال و مجوز همان حساب سرویس را مطابق محیط خود تنظیم کنید.

حساب مدیر و تعرفه‌ها از دیتابیس موجود خوانده می‌شوند. فقط اگر حساب مدیر هنوز ثبت نشده یا قصد تغییر رمز دارید:

```powershell
$env:NODE_ENV='production'
node .\scripts\admin-setup.mjs admin@example.invalid --username admin
```

رمز به‌صورت تعاملی و مخفی پرسیده می‌شود.

## سرویس دائمی ویندوز

اگر سرویس از قبل وجود دارد:

```powershell
node .\scripts\check-deploy.mjs C:\Naghsh\data
Start-Service -Name NaghshNode
```

در نصب اولیه، WinSW را در مسیر موجود deploy/iis ذخیره و نصب سرویس را اجرا کنید:

```powershell
Invoke-WebRequest -Uri 'https://github.com/winsw/winsw/releases/download/v2.12.0/WinSW-x64.exe' -OutFile .\deploy\iis\NaghshNode.exe
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\deploy\iis\install-service.ps1 -AppRoot C:\Naghsh\app -DataDirectory C:\Naghsh\data
```

نصب‌کننده سرویس را شروع می‌کند. سرویس NaghshNode فرانت و API را با هم اجرا می‌کند.

## تنظیم IIS روی HTTP و پورت 8081

در IIS Manager روی نام سرور، Application Request Routing Cache، سپس Server Proxy Settings را باز و از فعال بودن Enable Proxy مطمئن شوید.

اگر سایت Naghsh از قبل روی 8081 درست تنظیم شده است، برای هر انتشار نیازی به اجرای دوبارهٔ تنظیم IIS نیست. برای نصب اولیه یا اصلاح همین سایت:

```powershell
cd C:\Naghsh\app
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\deploy\iis\switch-http.ps1 -PublicIp 203.0.113.10 -HttpPort 8081 -AppRoot C:\Naghsh\app
```

این اسکریپت سایت Naghsh را به C:\inetpub\naghsh-proxy متصل می‌کند و درخواست پورت 8081 را به Next.js روی 127.0.0.1:3000 می‌فرستد. Next.js مسیرهای API را به دات‌نت روی 127.0.0.1:5100 منتقل می‌کند.

فقط Binding همین سایت تنظیم می‌شود. تنظیمات سراسری ARR و برنامهٔ دیگر روی پورت 80 حفظ می‌شوند. قانون فایروال ویندوز برای 8081 اضافه می‌شود؛ این پورت را در فایروال پنل VPS نیز اجازه دهید.

ریشهٔ فیزیکی IIS همان پوشهٔ proxy است. اسکریپت، web.config مخصوص HTTP را تولید می‌کند؛ قالب HTTPS قدیمی را دستی روی این سایت کپی نکنید.

## بررسی پس از انتشار

```powershell
Get-Service NaghshNode
Invoke-WebRequest http://127.0.0.1:5100/health -UseBasicParsing
Invoke-WebRequest http://127.0.0.1:3000 -UseBasicParsing
Invoke-WebRequest http://127.0.0.1:8081 -UseBasicParsing
```

آدرس مرورگر:

```text
http://203.0.113.10:8081
http://203.0.113.10:8081/admin
```

لاگ‌ها در C:\Naghsh\app\logs هستند. برای اجرای آزمایشی بدون سرویس، ابتدا سرویس را متوقف و node .\scripts\start-node.mjs را از C:\Naghsh\app اجرا کنید؛ پس از آزمایش با Ctrl+C متوقف و سرویس را دوباره شروع کنید.

## انتشارهای بعدی

۱. روی کامپیوتر توسعه npm.cmd run build و npm.cmd run package:iis را اجرا کنید.
۲. روی VPS سرویس NaghshNode را متوقف کنید.
۳. فایل‌های نسخهٔ جدید را با حفظ تنظیمات، داده‌ها و فایل‌های WinSW کپی کنید.
۴. تغییرات ساختار جدول‌های موجود را با حساب راه‌اندازی اعمال کنید:

```powershell
cd C:\Naghsh\app
$env:NODE_ENV='production'
node .\scripts\db-setup.mjs
node .\scripts\check-deploy.mjs C:\Naghsh\data
Start-Service NaghshNode
```

تست‌های دیتابیس فقط خواندنی هستند و هیچ دیتابیس یا حساب آزمایشی ساخته نمی‌شود.


## OTP و فراموشی رمز مشتری

نسخهٔ فعلی از OTP چهاررقمی کنسول ملی‌پیامک پشتیبانی می‌کند. پیش از استفاده از نسخهٔ نوسازی، migrationهای 002 و 003 باید جداگانه بازبینی و فقط با مجوز صریح روی همان ChapGiti موجود اعمال شوند و configure-otp.ps1 را برای ورود مخفی کلید کنسول اجرا کنید. پیامک وضعیت سفارش و OTP تنظیمات جدا دارند. راهنمای کامل: ../../docs/CUSTOMER-AUTH-OTP-FA.md.
