# استقرار چاپ گیتی روی IIS و VPS ویندوز

برای نصب جدید chapgiti.ir از [راهنمای Giti و GitiNode](../../docs/WINDOWS-SERVER-INSTALL-FA.md) استفاده کنید. دستورهای بعدی سابقهٔ استقرارهای قدیمی با IP یا HTTP هستند.

**HTTP روی پورت جدا، کنار برنامهٔ دیگر:** [راهنمای انتقال به HTTP](README-HTTP.md).

**بدون دامنه:** از [راهنمای اجرای با IP](README-IP.md) استفاده کنید. مراحل پایین برای دامنه است.

این پروژه با Node.js اجرا می‌شود و IIS درخواست‌های دامنه و HTTPS را به آن می‌رساند.
این روش با راهنمای [Self-hosting در Next.js](https://nextjs.org/docs/app/guides/self-hosting) و [Reverse Proxy در IIS](https://learn.microsoft.com/en-us/iis/extensions/url-rewrite-module/reverse-proxy-with-url-rewrite-v2-and-application-request-routing) هماهنگ است.

## ۱. پیش‌نیاز سرور

- Windows Server پشتیبانی‌شده با دسترسی Administrator.
- Node.js **24 LTS x64، حداقل 24.15**؛ Node 22 برای این بسته پشتیبانی نمی‌شود.
- IIS، افزونه [URL Rewrite](https://www.iis.net/downloads/microsoft/url-rewrite) و [Application Request Routing](https://www.iis.net/downloads/microsoft/application-request-routing).
- دامنه با DNS متصل به IP سرور و گواهی معتبر آن در `Local Computer > Personal`، همراه کلید خصوصی.
- دسترسی خروجی HTTPS برای نصب npm و سرویس ملی‌پیامک.
- پورت‌های ورودی 80 و 443. Node فقط روی `127.0.0.1` گوش می‌دهد؛ پورت 3000 را عمومی نکنید.

ساختار پیشنهادی:

```text
C:\Naghsh\app                  کد سایت، node_modules و .next
C:\Naghsh\data                 دیتابیس و PDFهای خصوصی
C:\inetpub\naghsh-proxy        فقط web.config مربوط به IIS
C:\Naghsh\backups              نسخه پشتیبان
```

Physical Path سایت IIS باید پوشه `naghsh-proxy` باشد. پوشه app یا data را به عنوان مسیر عمومی معرفی نکنید.
این نسخه برای **یک پردازش Node روی یک سرور** طراحی شده است؛ SQLite و فایل‌ها روی دیسک محلی هستند.

## ۲. انتقال و آماده‌سازی برنامه

محتویات ZIP سورس نسخه IIS را در `C:\Naghsh\app` قرار دهید.
پوشه‌های node_modules یا .next کامپیوتر توسعه لازم نیست منتقل شوند.

در PowerShell سرور:

```powershell
cd C:\Naghsh\app
npm.cmd ci
Copy-Item .env.example .env.local
notepad .env.local
```

اگر `.env.local` از قبل وجود دارد، آن را کپی و بازنویسی نکنید.
مقادیر زیر را با دامنه واقعی خود تنظیم کنید؛ SITE_URL بدون اسلش پایانی باشد:

```dotenv
SITE_URL=https://chap.example.com
PORT=3000
DATA_DIR=C:/Naghsh/data
TRUST_IIS_PROXY=true
INDEXING_ENABLED=false
SMS_ENABLED=false
# MELIPAYAMAK_USERNAME (configure privately when enabling SMS)
# MELIPAYAMAK_PASSWORD (configure privately when enabling SMS)
# MELIPAYAMAK_FROM (configure privately when enabling SMS)
# MELIPAYAMAK_BODY_ID (configure privately when enabling SMS)
```

فایل شامل رمزهای احتمالی است؛ آن را در Git ثبت نکنید.
اگر رمز ملی‌پیامک علامت دلار دارد، در فایل dotenv آن را به شکل `\$` بنویسید تا متغیر محیطی تلقی نشود.

```powershell
npm.cmd run db:setup
npm.cmd run admin:setup -- admin@example.invalid --username admin
npm.cmd run build
node scripts/check-deploy.mjs C:\Naghsh\data
npm.cmd start
```

دستور admin:setup رمز ۱۲ تا ۱۲۸ کاراکتری می‌پرسد و تأیید آن را می‌گیرد. رمز در خط فرمان یا خروجی ثبت نمی‌شود.
برای تغییر رمز در آینده همین دستور را تکرار کنید؛ نشست‌های آن مدیر باطل می‌شوند.

در خود سرور `http://127.0.0.1:3000` را باز کنید. در این مرحله صفحه اصلی قابل بررسی است.
ورود و عملیات فرم‌ها را پس از تنظیم IIS از **دامنه HTTPS** تست کنید، چون SITE_URL و کوکی امن برای همان دامنه تنظیم شده‌اند.
با Ctrl+C اجرای دستی را متوقف کنید.

## ۳. اجرای دائمی به صورت Windows Service

از [انتشار رسمی WinSW v2.12.0](https://github.com/winsw/winsw/releases/tag/v2.12.0)، فایل WinSW-x64.exe را دریافت کنید.
این ابزار برنامه Node را به صورت سرویس ویندوز اجرا می‌کند؛ پیکربندی حساب سرویس و راه‌اندازی خودکار در [مستندات رسمی WinSW](https://raw.githubusercontent.com/winsw/winsw/v2.12.0/doc/xmlConfigFile.md) آمده است.

در PowerShell با **Run as Administrator**:

```powershell
cd C:\Naghsh\app
Invoke-WebRequest -Uri 'https://github.com/winsw/winsw/releases/download/v2.12.0/WinSW-x64.exe' -OutFile '.\deploy\iis\NaghshNode.exe'
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\deploy\iis\install-service.ps1 -DataDirectory C:\Naghsh\data
Get-Service NaghshNode
```

اسکریپت از حساب کم‌دسترسی **LocalService** استفاده می‌کند، دسترسی فایل تنظیمات را محدود می‌کند و به پوشه داده، لاگ و cache اجازه نوشتن می‌دهد.
DATA_DIR فایل تنظیمات باید با DataDirectory دستور بالا یکسان باشد.
سرویس پس از بوت ویندوز خودکار اجرا و پس از خطا مجدداً راه‌اندازی می‌شود.
لاگ‌ها در `C:\Naghsh\app\logs` با چرخش و سقف تعداد فایل نگهداری می‌شوند.

این اسکریپت سرویس موجود را بازنویسی نمی‌کند. برای تغییرات بعدی:

```powershell
Restart-Service NaghshNode
Get-Content C:\Naghsh\app\logs\NaghshNode.err.log -Tail 50
```

## ۴. اتصال دامنه و SSL در IIS

ابتدا گواهی دامنه را در IIS / Certificate Manager نصب کنید و Thumbprint آن را پیدا کنید:

```powershell
Get-ChildItem Cert:\LocalMachine\My | Select-Object Subject, Thumbprint, NotAfter
```

سپس نام دامنه و Thumbprint واقعی را در این دستور وارد کنید:

```powershell
cd C:\Naghsh\app
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\deploy\iis\configure-iis.ps1 -Domain chap.example.com -CertificateThumbprint YOUR_40_CHARACTER_THUMBPRINT
```

این اسکریپت:
- سایت جدید Naghsh و App Pool بدون Managed Runtime می‌سازد.
- HTTPS با SNI و گواهی انتخابی را متصل می‌کند و HTTP را به HTTPS می‌فرستد.
- تمام درخواست‌ها، API و فایل‌های static را به `127.0.0.1:3000` هدایت می‌کند.
- سقف درخواست IIS را ۱۶ MiB می‌گذارد؛ برنامه برای هر PDF سقف ۱۵ MiB دارد.
- IP مشتری را در هدر مخصوص بازنویسی می‌کند تا محدودیت درخواست به IP واقعی متصل شود.
- کد خطای API مانند 401 یا 429 را بدون جایگزینی با صفحه خطای IIS عبور می‌دهد.

برای بازنویسی هدرها، متغیرهای لازم طبق [مستندات Microsoft](https://learn.microsoft.com/en-us/iis/extensions/url-rewrite-module/setting-http-request-headers-and-iis-server-variables) در فهرست مجاز IIS ثبت می‌شوند.
تنظیمات ARR شامل Enable Proxy و Preserve Host Header در سطح سرور هستند؛ اگر سایت‌های دیگری روی این IIS دارید، اثر این دو تنظیم را روی آن‌ها بررسی کنید.
اسکریپت روی سایت همنام یا پوشه عمومی غیرخالی اجرا نمی‌شود.

اگر پورت دیگری انتخاب می‌کنید، PORT در .env.local و پارامتر `-Port` اسکریپت IIS باید یکسان باشند.
دامنه اضافی یا www را با binding و گواهی جداگانه اضافه و به دامنه اصلی هدایت کنید.

## ۵. بررسی نهایی و انتشار

از یک دستگاه خارج از سرور:

1. صفحه اصلی و `/admin` را از HTTPS باز کنید.
2. با ایمیل مدیر و رمزی که تنظیم کرده‌اید وارد شوید.
3. سفارش آزمایشی و PDF ثبت و پیگیری کنید؛ از پنل فایل را دانلود و مرحله سفارش را تغییر دهید.
4. نام برند، تلفن، آدرس، تعرفه‌ها و اطلاعات سئو را در پنل اصلاح کنید.
5. متغیرهای ملی‌پیامک را وارد کنید. برای الگوی خدماتی BODY_ID، ترتیب متغیرها «کد سفارش؛ وضعیت فارسی» است.
6. پس از بررسی دامنه و محتوا، `INDEXING_ENABLED=true` و در صورت آماده بودن پیامک `SMS_ENABLED=true` قرار دهید و سرویس را restart کنید.
7. `/robots.txt` و `/sitemap.xml` را بررسی کنید. تمدید گواهی HTTPS را طبق روش صادرکننده گواهی تنظیم کنید.

ورود بدون رمز پیش‌فرض است. اطلاعات ChatGPT و هدرهای oai هیچ دسترسی مدیریتی ایجاد نمی‌کنند.
آدرس و پروتکل مرورگر باید دقیقاً با SITE_URL یکسان باشد؛ تفاوت آن‌ها هنگام ارسال فرم خطای 403 می‌دهد. SITE_URL می‌تواند IP دارای HTTPS یا دامنه باشد.

## پشتیبان‌گیری و به‌روزرسانی

داده‌ها خارج از پوشه برنامه هستند و با آپدیت سورس جایگزین نمی‌شوند.
پیش از آپدیت، سرویس را متوقف و از **تمام data** پشتیبان بگیرید؛ تنها کپی site.sqlite در زمان اجرای برنامه کافی نیست، زیرا SQLite از WAL استفاده می‌کند.

```powershell
New-Item -ItemType Directory -Path C:\Naghsh\backups -Force | Out-Null
$backupFile = 'C:\Naghsh\backups\data-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.zip'
Stop-Service NaghshNode
try {
  Compress-Archive -LiteralPath C:\Naghsh\data -DestinationPath $backupFile
} finally {
  Start-Service NaghshNode
}
```

نسخه پشتیبان را خارج از پوشه عمومی و با دسترسی محدود نگه دارید. از `.env.local` نیز جداگانه و امن پشتیبان بگیرید.
برای بازیابی، سرویس را متوقف کنید، پوشه data را از نسخه سالم برگردانید و دوباره اجرا کنید.
بازیابی را ابتدا روی یک پوشه و محیط آزمایشی بررسی کنید.

برای آپدیت، سرویس را متوقف کنید، سورس جدید را بدون بازنویسی .env.local و داده‌ها جایگزین، سپس این دستورها را اجرا کنید:

```powershell
cd C:\Naghsh\app
npm.cmd ci
npm.cmd run db:setup
npm.cmd run build
Start-Service NaghshNode
```

Migrationها در drizzle ثبت می‌شوند؛ فایل SQL اعمال‌شده را تغییر ندهید و migration جدید اضافه کنید.
راه‌انداز سرویس نیز migrationهای جدید را قبل از اجرا اعمال می‌کند.

## رفع اشکال

| خطا | بررسی |
|---|---|
| Failed to create service / account name is invalid | در XML سرویس، domain باید NT AUTHORITY و user باید LocalService باشد؛ نام کامل را داخل user ننویسید. از اسکریپت اصلاح‌شده استفاده کنید. |
| 502.3 | سرویس Node، لاگ، PORT و پاسخ 127.0.0.1:3000 را بررسی کنید. |
| 500.19 یا 500.50 | نصب URL Rewrite / ARR و مجاز بودن server variables را بررسی کنید. |
| 403 روی فرم | SITE_URL دقیقاً دامنه و پروتکل مرورگر باشد؛ سرویس را restart کنید. |
| ورود ناموفق | admin:setup را با همان DATA_DIR اجرا و رمز را بازنشانی کنید. |
| خطای دسترسی SQLite/PDF | LocalService باید روی data مجوز Modify داشته باشد. |
| فایل بزرگ رد می‌شود | سقف PDF برنامه ۱۵ MiB و IIS برابر ۱۶ MiB است. |
| 429 هنگام ورود | محدودیت ۱۰ تلاش در ۱۵ دقیقه برای هر IP است؛ پس از پایان بازه تلاش کنید. |

تست‌های برنامه روی Windows / Node production اجرا شده‌اند. نصب سرویس، binding گواهی و عبور درخواست از ARR باید روی VPS مقصد بررسی شوند.
