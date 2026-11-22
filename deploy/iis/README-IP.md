# اجرای IIS بدون دامنه، با IPv4

این راهنما برای VPS ویندوزی است که Node.js 24 و وابستگی‌های پروژه روی آن نصب شده‌اند.
مسیر پروژه در مثال‌ها C:\Naghsh\app است. همه دستورها در Windows PowerShell با Run as Administrator اجرا می‌شوند.
IP را فقط در سرور خود وارد کنید؛ نیازی به ارسال آن در چت نیست.

این روش HTTPS را حفظ می‌کند. گواهی self-signed پایین برای تست شخصی است و مرورگر به طور پیش‌فرض آن را معتبر نمی‌شناسد.
برای انتشار عمومی، گواهی معتبر با IP در Subject Alternative Name یا گواهی دامنه لازم است.
[Let's Encrypt اکنون گواهی IP صادر می‌کند](https://letsencrypt.org/2026/01/15/6day-and-ip-general-availability)؛ اعتبار آن ۱۶۰ ساعت است و باید تمدید و اتصال مجدد خودکار به IIS داشته باشد.
این راهنما صدور یا تمدید خودکار گواهی عمومی را پیاده نمی‌کند.

## ۱. فایل به‌روز و پیش‌نیاز IIS

اگر نسخه قبلی پروژه را منتقل کرده‌اید، بسته naghsh-iis-ip-update.zip را داخل C:\Naghsh\app استخراج کنید تا deploy\iis\configure-iis.ps1 به‌روز شود.
این بسته فقط راهنمای IIS و اسکریپت تنظیم IIS را عوض می‌کند؛ فایل .env.local، دیتابیس و برنامه در آن نیستند.

اگر IIS نصب نیست:

```powershell
Install-WindowsFeature Web-Server -IncludeManagementTools
```

[URL Rewrite](https://www.iis.net/downloads/microsoft/url-rewrite) و [Application Request Routing](https://www.iis.net/downloads/microsoft/application-request-routing) نسخه x64 را نیز نصب کنید.
اگر از قبل نصب‌اند، نصب مجدد لازم نیست.

## ۲. تنظیم محیط

```powershell
cd C:\Naghsh\app
$publicIp = Read-Host 'Public IPv4 of your VPS'
if (-not (Test-Path .env.local)) { Copy-Item .env.example .env.local }
notepad .env.local
```

این مقدارها را تنظیم کنید. به جای YOUR_VPS_IP عدد IP واقعی بنویسید؛ SITE_URL بدون اسلش آخر باشد.
تنظیمات دیگر موجود، به‌ویژه اطلاعات ملی‌پیامک، را حفظ کنید.

```dotenv
SITE_URL=https://YOUR_VPS_IP
PORT=3000
DATA_DIR=C:/Naghsh/data
TRUST_IIS_PROXY=true
INDEXING_ENABLED=false
SMS_ENABLED=false
```

## ۳. دیتابیس، مدیر و نسخه اجرا

هر دستور را بعد از موفق شدن دستور قبلی اجرا کنید.

```powershell
npm.cmd run db:setup
npm.cmd run admin:setup -- admin@example.invalid
npm.cmd run build
node scripts/check-deploy.mjs C:\Naghsh\data
npm.cmd start
```

admin:setup رمز ۱۲ تا ۱۲۸ کاراکتری را به صورت مخفی می‌پرسد؛ اجرای دوباره آن رمز مدیر را عوض می‌کند.
در مرورگر داخل VPS صفحه http://127.0.0.1:3000 را باز کنید.
در این مرحله فقط صفحه اصلی را بررسی کنید؛ ورود و ارسال فرم‌ها باید از https://IP تنظیم‌شده انجام شوند.
با Ctrl+C اجرای دستی را ببندید تا پورت 3000 برای سرویس آزاد شود.

## ۴. سرویس دائمی

```powershell
Invoke-WebRequest -Uri 'https://github.com/winsw/winsw/releases/download/v2.12.0/WinSW-x64.exe' -OutFile '.\deploy\iis\NaghshNode.exe'
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\deploy\iis\install-service.ps1 -DataDirectory C:\Naghsh\data
Get-Service NaghshNode
```

Status باید Running باشد. اگر سرویس قبلاً نصب شده، نصب دوباره لازم نیست؛ پس از تغییر تنظیمات Restart-Service NaghshNode بزنید.
برای خطاها C:\Naghsh\app\logs را بررسی کنید.

## ۵. گواهی آزمایشی و اتصال IIS

اگر پنجره PowerShell را بسته‌اید، cd C:\Naghsh\app و دستور Read-Host مرحله ۲ را دوباره اجرا کنید.
این گواهی تنها روی سرور خودتان ساخته می‌شود و کلید خصوصی آن منتقل نمی‌شود:

```powershell
$certParams = @{
    Type = 'Custom'
    Subject = "CN=$publicIp"
    TextExtension = @(
        "203.0.113.11={text}IPAddress=$publicIp"
        '203.0.113.12={text}1.3.6.1.5.5.7.3.1'
    )
    KeyAlgorithm = 'RSA'
    KeyLength = 2048
    HashAlgorithm = 'SHA256'
    KeyUsage = @('DigitalSignature', 'KeyEncipherment')
    CertStoreLocation = 'Cert:\LocalMachine\My'
    FriendlyName = 'Naghsh IP test'
    NotAfter = (Get-Date).AddMonths(3)
}
$cert = New-SelfSignedCertificate @certParams
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\deploy\iis\configure-iis.ps1 -PublicHost $publicIp -CertificateThumbprint $cert.Thumbprint
```

اگر گواهی معتبر IP دارید، ساخت گواهی آزمایشی را انجام ندهید؛ Thumbprint گواهی معتبر را به دستور configure-iis بدهید.

اسکریپت Naghsh را با Physical Path برابر C:\inetpub\naghsh-proxy می‌سازد؛ فقط web.config در این پوشه عمومی قرار می‌گیرد.
HTTPS برای IP با Host name خالی و SNI خاموش تنظیم می‌شود. HTTP با Host header همان IP به HTTPS می‌رود.
این با [مدل binding در IIS](https://learn.microsoft.com/en-us/iis/configuration/system.applicationhost/sites/site/bindings/binding) و [گواهی آزمایشی Microsoft](https://learn.microsoft.com/en-us/powershell/module/pki/new-selfsignedcertificate) هماهنگ است.
ARR در سطح سرور فعال و Preserve Host Header تنظیم می‌شود؛ اثر این تنظیمات روی سایت‌های دیگر IIS را بررسی کنید.

اگر binding قبلی HTTPS بدون SNI روی پورت 443 وجود دارد، اسکریپت پیش از تغییر متوقف می‌شود؛ آن binding را خودکار حذف نمی‌کند.
در سرور چند-IP می‌توانید -ListenAddress را با یک IP محلی مجزا تعیین کنید. در سرور دارای NAT، PublicHost همان IP عمومی است و ListenAddress آدرس کارت شبکه یا * باقی می‌ماند.
اگر سایت Naghsh از قبل ساخته شده، تنظیمات آن را در IIS Manager ویرایش کنید؛ اسکریپت برای ساخت سایت جدید است.

## ۶. دسترسی و بررسی

```powershell
if (-not (Get-NetFirewallRule -Name 'Naghsh-Web' -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -Name 'Naghsh-Web' -DisplayName 'Naghsh Web' -Direction Inbound -Action Allow -Protocol TCP -LocalPort 80,443
}
```

در فایروال پنل ارائه‌دهنده VPS هم TCP 80 و 443 باید باز باشد. پورت 3000 عمومی نمی‌شود.
در مرورگر آدرس https://IP و https://IP/admin را باز کنید.
با گواهی آزمایشی، هشدار اعتبار گواهی انتظار می‌رود؛ آن را به عنوان سایت دارای گواهی معتبر به مشتری معرفی نکنید.

برای اعتماد به همین گواهی روی کامپیوتر شخصی خود:
- در VPS دستور زیر را اجرا کنید تا تنها بخش عمومی گواهی صادر شود.
- فایل .cer را از طریق اتصال RDP مطمئن به کامپیوتر خود منتقل کنید.
- در کامپیوتر خود فایل .cer را باز کنید و Install Certificate > Current User > Place all certificates > Trusted Root Certification Authorities را انتخاب کنید.
- مرورگر را دوباره باز کنید. فقط دستگاهی که این گواهی را وارد کرده به آن اعتماد می‌کند؛ اعتبار عمومی ایجاد نمی‌شود.

```powershell
Export-Certificate -Cert $cert -FilePath C:\Naghsh\naghsh-ip-test.cer
```

ورود، ثبت سفارش، PDF و پیگیری را از همان آدرس HTTPS آزمایش کنید.
تا آماده شدن محتوا و گواهی عمومی، INDEXING_ENABLED و SMS_ENABLED را false نگه دارید.

## خطاهای متداول

| خطا | اقدام |
|---|---|
| 502.3 | Get-Service NaghshNode و لاگ‌ها؛ سپس پاسخ localhost:3000 را بررسی کنید. |
| 500.19 یا 500.50 | نصب URL Rewrite و ARR و اجرای اسکریپت configure-iis را بررسی کنید. |
| 403 هنگام ورود یا فرم | SITE_URL باید دقیقاً برابر https://IP مرورگر باشد؛ Restart-Service NaghshNode بزنید. |
| SITE_URL must use HTTPS | برای IP عمومی https:// تنظیم کنید؛ http:// با راه‌انداز تولید سازگار نیست. |
| صفحه سایت دیگری باز می‌شود | bindingهای IIS، IP واردشده و اشغال پورت‌های 80/443 را بررسی کنید. |

نصب واقعی سرویس و IIS باید روی VPS مقصد بررسی شود؛ بررسی محلی فایل اسکریپت جایگزین این آزمایش نیست.
برای پیامک، پشتیبان‌گیری و آپدیت، README.md همین پوشه را ببینید.
