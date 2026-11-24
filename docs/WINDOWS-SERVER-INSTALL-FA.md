# نصب دستی چاپ گیتی روی Windows Server جدید

این راهنما برای نصب دستی Release خودکفای ویندوز روی `https://chapgiti.ir` است. پیش از نصب، هش بسته، قرارداد سرور و نتیجهٔ پذیرش مقصد را تطبیق دهید. [پذیرش SQL Server 2025](SQL2025-ACCEPTANCE-FA.md)

برای ارتقای نصب موجود، مراحل پشتیبان‌گیری، کنترل شناسهٔ `main`، پیوست تیکت و تعویض نسخه را به‌ترتیب اجرا کنید. [راهنمای ارتقا و بازگشت نسخه](UPGRADE-CUT-TARIFFS-FA.md)

## ۱. مسیرها و معماری اجرا

| مورد | مقدار نصب جدید |
|---|---|
| محل استخراج اولیهٔ release | `C:\Deploy\Giti-Release` |
| ریشهٔ برنامه | `C:\Giti\app` |
| دادهٔ خصوصی پایدار | `C:\Giti\data` |
| Physical Path سایت IIS | `C:\inetpub\giti-proxy` |
| نام سایت و Application Pool | `Giti` |
| Windows Service | `GitiNode` |
| Next.js داخلی | `127.0.0.1:3000` |
| ASP.NET Core داخلی | `127.0.0.1:5100` |
| نام دیتابیس موجود/تأییدشده | `ChapGiti` |

WinSW یک supervisor را اجرا می‌کند که API خودکفا و Next standalone را روی loopback راه می‌اندازد. چهار مسیر `/api/realtime/{customer|admin}/{notifications|support}` و negotiate آن‌ها از ARR مستقیماً به ۵۱۰۰ می‌رسند. صفحه‌ها و REST از ۳۰۰۰ و پراکسی فعلی Next عبور می‌کنند.

مسیرها و نام سرویس پارامتر دارند؛ برای نصب قدیمی باید مقادیر همان نصب را صریح بدهید. نام نصب جدید هیچ تنظیم سرور قدیمی را اثبات نمی‌کند. header داخلی `X-Naghsh-Client-IP` برای سازگاری قرارداد موجود حفظ شده؛ نام سرویس و مسیر نصب `Giti` است.

## ۲. آماده‌سازی سرور

روی Windows Server x64 پشتیبانی‌شده توسط Node.js 24، .NET 10 و SQL Server 2025، PowerShell را با دسترسی Administrator باز کنید. پیش از نصب سازگاری نسخهٔ واقعی Windows و آخرین به‌روزرسانی‌های امنیتی آن را با مستندات سازندگان بررسی کنید.

برای این بسته Node.js با بازهٔ `>=24.15.0 <25` لازم است؛ ساخت محلی با `v24.20.0` انجام شد. بک‌اند با .NET 10 و Runtime مستقل `win-x64` منتشر شده است؛ برای اجرای آن SDK یا Hosting Bundle لازم نیست. WinSW 2.12.0 و پیش‌نیاز .NET Framework همان باینری را طبق مستندات آن آماده کنید. SDK دات‌نت و ابزار تست داخل release نیستند و نصب روی سرور به `npm ci` یا build نیاز ندارد.

دریافت رسمی:

- [Node.js](https://nodejs.org/en/download)
- [IIS URL Rewrite 2.1](https://www.iis.net/downloads/microsoft/url-rewrite)
- [Application Request Routing 3](https://www.iis.net/downloads/microsoft/application-request-routing)
- [WinSW 2.12.0](https://github.com/winsw/winsw/releases/tag/v2.12.0)
- [الزامات WebSocket در ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/websockets?view=aspnetcore-10.0)

قابلیت‌های IIS را روی **سرور مقصد** فعال کنید؛ این دستورات در مرحلهٔ تولید بسته اجرا نشده‌اند:

```powershell
Install-WindowsFeature Web-Server, Web-Mgmt-Console, Web-WebSockets, Web-Filtering, Web-Static-Content, Web-Http-Logging
node.exe --version
Get-WindowsFeature Web-WebSockets
```

سپس URL Rewrite و ARR x64 را از صفحات رسمی نصب کنید. در IIS Manager بخش Application Request Routing Cache → Server Proxy Settings، پراکسی باید فعال باشد. اسکریپت تنظیم IIS نیز این تنظیم ARR را اعمال می‌کند؛ چون تنظیم server-wide است، روی سرور دارای برنامه‌های دیگر ابتدا اثر آن را بررسی کنید. پورت‌های 3000 و 5100 فقط loopback باشند؛ برای آن‌ها firewall عمومی باز نکنید. فقط پورت‌های عمومی لازم 80/443 با سیاست سرور باز شوند؛ SQL نیز عمومی نشود.

## ۳. بررسی و استخراج بسته

فایل `.sha256` کنار ZIP را همراه ZIP منتقل و با SHA اعلام‌شده در گزارش تحویل مقایسه کنید. archive قدیمی را حذف نکنید. مسیر staging باید برای همین release اختصاصی و خالی باشد؛ اگر از قبل فایل دارد، مسیر جداگانه انتخاب کنید.

```powershell
$releaseZip = Read-Host 'مسیر کامل Release ZIP'
Get-FileHash -Algorithm SHA256 -LiteralPath $releaseZip
New-Item -ItemType Directory -Path C:\Deploy\Giti-Release -ErrorAction Stop
Expand-Archive -LiteralPath $releaseZip -DestinationPath C:\Deploy\Giti-Release
Set-Location C:\Deploy\Giti-Release
node.exe .\scripts\verify-release.mjs
```

ZIP در ریشهٔ خود `frontend`، `backend`، `scripts`، `deploy`، `docs` و manifest دارد؛ زیرپوشهٔ اضافی برای app فرض نشده است. verifier، SHA تمام فایل‌های manifest و checksum کامل migrationها را کنترل می‌کند و به دیتابیس متصل نمی‌شود. بسته‌ساز محتوای هر ورودی فشرده را دوباره می‌خواند و SHA آن را می‌سنجد. verifier پوشهٔ استخراج‌شده را پیش از افزودن .env.local و WinSW اجرا کنید.

برای نصب تازه، مسیر برنامه باید خالی باشد. دادهٔ خصوصی هرگز داخل ریشهٔ فیزیکی IIS کپی نشود:

```powershell
New-Item -ItemType Directory -Path C:\Giti\app -ErrorAction Stop
New-Item -ItemType Directory -Path C:\Giti\data -Force
Get-ChildItem -LiteralPath C:\Deploy\Giti-Release -Force | Copy-Item -Destination C:\Giti\app -Recurse
Set-Location C:\Giti\app
node.exe .\scripts\verify-release.mjs
```

این روش copy مخصوص نصب تازه است؛ روش به‌روزرسانی پایین همین سند را برای نصب موجود بخوانید.

پیش از راه‌اندازی نصب تازه، در ریشهٔ بستهٔ تأییدشده روی NTFS و با دسترسی مدیر این دستورها را اجرا کنید. هر نصب انتشار کلیدهای داخلی Next.js خودش را دریافت می‌کند. این فایل‌ها در ریشهٔ خصوصی برنامه و خارج از WebRoot قرار می‌گیرند. بررسی SHAهای manifest برای نسخهٔ تازه‌استخراج‌شده است. پس از آماده‌سازی، صحت سه فایل اختصاصی مقصد با فرمان دوم بررسی می‌شود.

```powershell
node.exe .\scripts\initialize-frontend.mjs
if ($LASTEXITCODE -ne 0) { throw 'Frontend initialization failed' }
node.exe .\scripts\initialize-frontend.mjs --check
if ($LASTEXITCODE -ne 0) { throw 'Frontend installation check failed' }
```

## ۴. تنظیم امن محیط مقصد

اگر `.env.local` وجود دارد آن را بازنویسی نکنید. در نصب تازه نمونه را فقط یک بار کپی کنید:

```powershell
if (-not (Test-Path -LiteralPath .env.local)) { Copy-Item -LiteralPath .env.example -Destination .env.local }
```

فایل خصوصی را با مقادیر واقعی سرور ویرایش کنید. آدرس عمومی، رمز SQL، کلید پیامک یا certificate از این راهنما حدس زده نمی‌شود:

| کلید | مقدار/کاربرد |
|---|---|
| `SITE_URL` | origin واقعی HTTPS بدون مسیر و اسلش انتهایی |
| `PORT` | `3000` |
| `DOTNET_API_URL` | `http://127.0.0.1:5100` |
| `DATA_DIR` | `C:/Giti/data` |
| `TRUST_IIS_PROXY` | `true` پشت IIS تنظیم‌شدهٔ این بسته |
| `ALLOW_HTTP` | `false` برای انتشار عمومی |
| `SQLSERVER_CONNECTION_STRING` | اتصال خصوصی به instance واقعی و دیتابیس تأییدشدهٔ `ChapGiti` |
| `INDEXING_ENABLED` | تا تصمیم انتشار عمومی `false`؛ سپس طبق تصمیم مالک |
| `SMS_ENABLED` و `OTP_ENABLED` | برای UAT بدون پیامک `false` |
| `OTP_VERIFICATION_ENABLED` | همواره `true` در Production |
| `OTP_CODE_LENGTH` / `OTP_LIFETIME_SECONDS` | `4` / `120` |
| `MELIPAYAMAK_*` | فقط credential واقعی محیط مقصد، در فایل خصوصی؛ خالی تا فعال‌سازی مجاز |

راه‌انداز، `AllowedHosts` بک‌اند را به loopback و hostname واقعی `SITE_URL` محدود می‌کند تا Host عمومی مسیر مستقیم SignalR پذیرفته شود. اگر `AllowedHosts` را صریح تنظیم کرده‌اید، همان مقدار حفظ می‌شود؛ hostname عمومی صحیح و loopbackها را در آن منظور کنید و wildcard نگذارید. [قرارداد رسمی Host filtering](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/host-filtering?view=aspnetcore-10.0).

فایل را با SIDهای مستقل از زبان Windows محافظت کنید:

```powershell
icacls.exe .env.local /inheritance:r /grant:r '*S-1-5-18:F' '*S-1-5-32-544:F' '*S-1-5-19:R'
```

`.env.local` هیچ‌وقت داخل public، Git، فایل ZIP یا پیام‌رسان قرار نگیرد. OTP واقعی هنگام فعال‌سازی از Console ملی‌پیامک و کلید همان محیط استفاده می‌کند؛ تغییر نام سایت، قالب SMS یا سیاست OTP را عوض نمی‌کند. درخواست OTP در smoke test نصب، پیامک واقعی می‌فرستد؛ بدون تصمیم جداگانه آن را آزمایش نکنید. ورودهای موجود با رمز مستقل‌اند. Push پیاده‌سازی/ارسال نشده است.

## ۵. دیتابیس و حساب مدیر

ابتدا [راهنمای کامل migration](DATABASE-MIGRATION-FA.md) را اجرا کنید: شناسایی instance، وجود دیتابیس، بررسی checksumها و فقط پس از مجوز، runner واقعی. دستور مستند و آزمایش‌شده از ریشهٔ release:

```powershell
node.exe .\scripts\db-setup.mjs
```

این دستور schema را تغییر می‌دهد؛ startup معمول و نصب WinSW آن را خودکار اجرا نمی‌کنند. SQL Server 2025 Express هنوز پذیرش واقعی این نسخه را نگذرانده است؛ قبل از rollout داده‌دار، اجرای همان زنجیره در دیتابیس disposable روی SQL Server 2025 و UAT مقصد لازم است.

در دیتابیس ارتقایافته، حساب `admin` و رمز فعلی را حفظ کنید و ابزار setup مدیر اجرا نکنید. در **دیتابیس تازه و خالی**، مدیر باید یک بار با ایمیل واقعی مالک و رمز جدید مطابق policy ساخته شود. setup می‌تواند credential حساب موجود را تغییر دهد؛ فقط برای provisioning صریح استفاده کنید:

```powershell
$adminEmail = Read-Host 'ایمیل واقعی مدیر نصب جدید'
node.exe .\scripts\admin-setup.mjs --username admin $adminEmail
```

رمز به‌صورت تعاملی و پوشیده درخواست می‌شود؛ در command line، فایل نمونه یا چت قرار نگیرد. username ثابت `admin` است. این بسته هیچ رمز پیش‌فرض یا account از سرور قدیمی تحمیل نمی‌کند.

## ۶. نصب سرویس GitiNode

باینری رسمی WinSW-x64.exe v2.12.0 را دریافت و با نام دقیق `C:\Giti\app\deploy\iis\GitiNode.exe` ذخیره کنید. URL قابل کپی:

```text
https://github.com/winsw/winsw/releases/download/v2.12.0/WinSW-x64.exe
```

فایل `GitiNode.xml.template` فقط نمونهٔ قابل‌بازبینی است. installer با مسیر واقعی `node.exe`، AppRoot، DataDirectory و ServiceName، فایل هم‌نام `GitiNode.xml` را تولید می‌کند؛ صرفاً تغییر نام exe کافی نیست. serviceaccount برابر `NT AUTHORITY\LocalService` و نام Windows آن `NT AUTHORITY\LOCAL SERVICE` است. installer روی سرویس موجود overwrite نمی‌کند.

ابتدا دسترسی SQL همین هویت اجرا را طبق بخش دیتابیس آماده کنید. بررسی زیر اتصال و وجود مدیر را **فقط در سرور مقصد** می‌خواند:

```powershell
Set-Location C:\Giti\app
node.exe .\scripts\check-deploy.mjs C:\Giti\data
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\deploy\iis\install-service.ps1 -AppRoot C:\Giti\app -DataDirectory C:\Giti\data -ServiceName GitiNode
Get-Service -Name GitiNode
```

installer دسترسی Read/Execute برنامه و Read فایل خصوصی تنظیمات، و Modify فقط data/log/cache را به LocalService می‌دهد. هویت اجرای SQL باید جداگانه تأیید شود؛ موفقیت تست Administrator جای تست سرویس را نمی‌گیرد.

```powershell
Start-Service -Name GitiNode
Stop-Service -Name GitiNode
Restart-Service -Name GitiNode
Get-Content C:\Giti\app\logs\* -Tail 50
Invoke-WebRequest -UseBasicParsing http://127.0.0.1:5100/health
Invoke-WebRequest -UseBasicParsing http://127.0.0.1:3000
Get-NetTCPConnection -LocalPort 3000,5100 -State Listen
```

Listenerها باید روی loopback باشند. سرویس Automatic/Delayed و recovery با restart دارد؛ توقف/راه‌اندازی و reboot سرور را در پنجرهٔ UAT خودتان تأیید کنید.

## ۷. HTTPS و IIS

پیش از عمومی‌کردن ورود، DNS/آدرس واقعی، گواهی معتبر دارای private key و trust زنجیره و نام گواهی آماده باشد. thumbprint از certificate واقعی LocalMachine گرفته شود؛ اسکریپت هیچ گواهی یا دامنه‌ای اختراع نمی‌کند. bindingهای موجود را بررسی کنید و اجازه ندهید سایت قبلی تحت تأثیر قرار گیرد.

```powershell
$publicHost = 'chapgiti.ir'
$certificateThumbprint = Read-Host 'Thumbprint گواهی معتبر LocalMachine/My'
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\deploy\iis\configure-iis.ps1 -PublicHost $publicHost -CertificateThumbprint $certificateThumbprint -SiteName Giti -WebRoot C:\inetpub\giti-proxy -Port 3000 -BackendPort 5100
```

این دستور بعداً روی مقصد IIS را تغییر می‌دهد: app pool بدون managed runtime، سایت جدید، HTTP→HTTPS، binding گواهی و web.config اختصاصی proxy را می‌سازد. روی site/pool موجود یا WebRoot غیرخالی متوقف می‌شود؛ public data یا برنامه داخل WebRoot قرار ندهید. هدرهای forwarded در لبه بازنویسی می‌شوند و پورت‌های backend داخلی باقی می‌مانند.

WebSocket Protocol و عبور HTTP/1.1 Upgrade در ARR/CDN لازم است. مسیرهای اعلان و پشتیبانی مشتری و مدیر پیش از catch-all فرانت به API داخلی می‌رسند. تنظیمات timeout پراکسی باید اجازهٔ اتصال بلندمدت بدهند. CDN با WebSocket نامعلوم را فعال فرض نکنید؛ پس از تنظیم، negotiation و پاسخ 101 را در DevTools بررسی کنید. در نبود WebSocket، SignalR از SSE/LongPolling و برنامه از polling کنترل‌شده استفاده می‌کند. تغییر تنظیم عمومی ARR را با سایر سایت‌ها هماهنگ کنید.

## ۸. smoke test و UAT مقصد

پس از تأیید گواهی و hostname واقعی:

```powershell
$site = 'https://chapgiti.ir'
Invoke-WebRequest -UseBasicParsing "$site/"
Invoke-WebRequest -UseBasicParsing "$site/customer"
Invoke-WebRequest -UseBasicParsing "$site/admin/login"
Invoke-WebRequest -UseBasicParsing "$site/api/public/bootstrap"
```

با حساب‌های آزمایشی مجاز، ورود مستقل هر نقش، رد دسترسی متقاطع، الزام رضایت و PDF اصلی و دسترسی مالک به فایل را بررسی کنید. مشتری آخرین پیشنهاد قیمت را می‌پذیرد. قیمت قفل می‌شود. اصلاح فایل یک PDF جدید می‌خواهد. مدیر شرایط تحویل را جداگانه می‌فرستد و مشتری نسخهٔ جاری را صریحاً تأیید می‌کند. شروع تولید فقط با PDF تأییدشده، قیمت پذیرفته، شرایط پذیرفته و بدون اصلاحیهٔ باز مجاز است. پس از refresh، نسخهٔ فایل و قیمت و وضعیت خوانده‌شدن را تطبیق دهید.

در پشتیبانی عمومی و مرتبط با سفارش، پیام مشتری و پاسخ مدیر، عزل دسترسی کاربر دیگر، قطع و وصل، بازیابی پیام جاافتاده و read/unread را با داده‌های UAT دارای مجوز بررسی کنید. هرکدام از چهار Hub ابتدا negotiate و سپس در صورت پشتیبانی واقعی یک پاسخ 101 روی WSS دارد. با مسدود کردن WebSocket در مرورگر آزمایشی، مسیر fallback و امکان ثبت پیام روی REST را نیز بسنجید. سلامت و ماندگاری پس از راه‌اندازی مجدد در پنجرهٔ مجاز همان سرور بررسی می‌شود.

## ۹. به‌روزرسانی و rollback آینده

در هر ارتقا، قبل از تعویض باینری‌ها، در staging تأییدشده همان انتشار `node.exe .\scripts\initialize-frontend.mjs` و سپس `node.exe .\scripts\initialize-frontend.mjs --check` را اجرا کنید. نسخهٔ قبلی برنامه را با فایل‌های مخصوص همان نصب برای بازیابی نگه دارید.

1. از `.env.local`، XML/exe سرویس، تنظیمات IIS، دیتابیس SQL و `C:\Giti\data` بکاپ قابل‌بازیابی تهیه کنید؛ جزئیات بازیابی را قبل از تغییر مرور کنید.
2. فقط `GitiNode` را در پنجرهٔ نگهداری متوقف کنید؛ سرویس‌ها و سایت‌های دیگر را تغییر ندهید.
3. release جدید را در staging مستقل استخراج و SHA، manifest و migrations را بررسی کنید. `.env.local` یا data را از بسته بازنویسی نکنید.
4. سازگاری برنامه/SQL و pending migrationها را با DBA بررسی و فقط نسخه‌های مجاز را با runner اعمال کنید؛ نسخه‌های applied را تغییر ندهید.
5. باینری‌ها و assetها را از staging تأییدشده جایگزین کنید؛ دادهٔ خصوصی، تنظیمات مقصد، WinSW exe/XML و logs حفظ شوند. rollback باینری را در پوشهٔ جدا نگه دارید. فایل generated publish قدیمیِ نامرتبط را وارد runtime جدید نکنید.
6. سرویس را راه‌اندازی و health، login، فایل، اصلاحیه و اعلان را بررسی کنید.
7. اگر لازم شد، سرویس را متوقف و باینری/تنظیمات سازگار قبلی را برگردانید. migration یا جدول/تاریخچه را کورکورانه معکوس یا حذف نکنید؛ rollback دیتابیس فقط با برنامهٔ بازیابی تأییدشدهٔ DBA انجام شود. برای خالی‌کردن نسخهٔ app، هرگز `C:\Giti\data` را حذف نکنید.

### پذیرش تیکت و پیوست پس از نصب

پیش از ارتقا، سلامت بکاپ کامل SQL را با checksum و RESTORE VERIFYONLY بررسی کنید. همراه نسخهٔ برنامه و تنظیمات خصوصی، کل DATA_DIR شامل ticket-files، فایل‌های سفارش و مجوز پوشه‌ها باید قابل بازیابی باشد. این نسخه از همان جدول dbo.Ticket_Attachments و مهاجرت 20261124131104_TicketMessageAttachments استفاده می‌کند. قبل از فعال‌کردن برنامه، ابزار db:setup بسته باید همهٔ شناسه‌های مورد انتظار را روی مقصد تأیید کند؛ برنامه در startup مهاجرت اجرا نمی‌کند.

با حساب آزمایشی مشتری، در فرم تیکت عمومی و سپس در جزئیات یک سفارش آزمایشی، یک PDF و یک تصویر را پیش از ارسال انتخاب کنید. یک فایل را حذف و دوباره اضافه کنید. بعد از ثبت، با حساب مدیر در مرورگر جداگانه نام و محتوای دانلودهای همان اولین پیام را بررسی کنید. پاسخ فایل‌دار مدیر باید در نشست مشتری دیده شود. دانلود پیوست به مسیر احرازشدهٔ تیکت وابسته است. هنگام به‌روزرسانی یا rollback، فایل‌های خصوصی و فرادادهٔ SQL را با هم حفظ کنید و به باینری قبلی سازگار برگردید. حذف جدول پیوست یا حذف پوشهٔ ticket-files دادهٔ کاربران را از بین می‌برد.

### نمونهٔ بکاپ کامل SQL پیش از تعویض نسخه

این مثال را DBA روی نمونهٔ مقصدی اجرا می‌کند که نام ماشین و نام دیتابیس آن را از قبل تطبیق داده است. پوشهٔ بکاپ باید از قبل وجود داشته باشد و حساب سرویس SQL Server به آن دسترسی نوشتن داشته باشد.

```sql
SELECT SERVERPROPERTY('MachineName') AS machine, SERVERPROPERTY('ServerName') AS instance, DB_NAME() AS active_database;
-- پس از تطبیق مقصد و بررسی فضای آزاد، مسیر محافظت‌شدهٔ مخصوص همین نصب را وارد کنید.
DECLARE @backup nvarchar(4000) = CONCAT(N'D:\SqlBackups\ChapGiti-before-upgrade-', CONVERT(char(8), GETDATE(),112), N'-', REPLACE(CONVERT(char(12),GETDATE(),114), ':', ''), N'.bak');
BACKUP DATABASE [ChapGiti] TO DISK = @backup WITH COPY_ONLY, CHECKSUM, STATS = 10;
RESTORE VERIFYONLY FROM DISK = @backup WITH CHECKSUM;
SELECT @backup AS verified_backup_path;
```

نام بکاپ، نتیجهٔ بررسی، نسخهٔ فعلی برنامه و زمان توقف نوشتن را کنار هم ثبت کنید. برای بازگشت پس از ثبت دادهٔ جدید، ابتدا با DBA دربارهٔ حفظ تغییرات پس از انتشار تصمیم بگیرید.
