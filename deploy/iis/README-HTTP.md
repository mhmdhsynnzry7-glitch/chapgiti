# اجرای چاپخانه با HTTP روی پورت جدا

برای سروری که برنامه دیگری روی http://IP و پورت ۸۰ دارد، از switch-http.ps1 استفاده کنید.
پورت پیش‌فرض چاپخانه 8081 است و آدرس آن http://IP:8081 خواهد بود.
Node همچنان روی 127.0.0.1:3000 و به صورت سرویس NaghshNode اجرا می‌شود.

## اجرا روی VPS

فایل switch-http.ps1 را در C:\Naghsh\app\deploy\iis قرار دهید.
این فایل مستقل است و نسخه لازم راه‌انداز و بررسی استقرار Node را همراه دارد؛ Build مجدد لازم نیست.

در Windows PowerShell با Run as Administrator:

```powershell
$publicIp = Read-Host 'Public IPv4 of your VPS'
powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\Naghsh\app\deploy\iis\switch-http.ps1 -PublicIp $publicIp -HttpPort 8081
```

اگر 8081 اشغال باشد، اسکریپت پیش از تغییر متوقف می‌شود؛ پورت دیگری مانند 8082 را با HttpPort بدهید.
TCP همین پورت را در فایروال پنل VPS هم باز کنید.

## تغییرات

- فقط bindingهای سایت Naghsh با Physical Path برابر C:\inetpub\naghsh-proxy به یک binding از نوع HTTP روی پورت انتخابی تبدیل می‌شوند.
- bindingهای 80 و 443 این سایت حذف می‌شوند. اسکریپت به binding یا وضعیت سایت‌های دیگر دست نمی‌زند.
- web.config چاپخانه فاقد تغییر مسیر HTTPS و هدر HSTS می‌شود.
- SITE_URL=http://IP:8081، ALLOW_HTTP=true، PORT=3000 و TRUST_IIS_PROXY=true تنظیم می‌شوند. سایر متغیرها و اطلاعات ملی‌پیامک حفظ می‌شوند.
- فایل‌های scripts/start-node.mjs و scripts/check-deploy.mjs برای پشتیبانی از ALLOW_HTTP به‌روز می‌شوند.
- سرویس چاپخانه restart می‌شود و پاسخ HTTP 200 از Node و IIS بررسی می‌شود.
- تنظیمات سراسری ARR از جمله preserveHostHeader و reverseRewriteHostInResponseHeaders تغییر نمی‌کنند؛ ARR باید از قبل فعال باشد.
- whitelist هدرهای لازم در محل مربوط به سایت Naghsh ثبت می‌شود.
- مجوز ورودی Windows Firewall تنها برای پورت انتخابی ایجاد می‌شود.

HTTP ترافیک، از جمله ورود پنل، را رمزنگاری نمی‌کند. این حالت با درخواست صریح استفاده از HTTP فعال می‌شود.

## پشتیبان و خطا

قبل از تغییر، فایل تنظیمات، دو اسکریپت Node، تنظیم سرویس و web.config موجود در پوشه زیر پشتیبان گرفته می‌شوند:

C:\Naghsh\app\deploy\iis\backups\http-TIMESTAMP

این پوشه به Administrator و SYSTEM محدود می‌شود. snapshot سایت‌ها و مقادیر فعلی ARR نیز ثبت می‌شود.
یک پشتیبان کامل IIS هم با Backup-WebConfiguration ساخته می‌شود؛ آن را بدون بررسی روی کل سرور restore نکنید، چون ممکن است تغییرات سایت‌های دیگر را هم برگرداند.

اگر اجرای انتقال پس از شروع تغییرات خطا بدهد، فایل‌های برنامه برگردانده و سرویس دوباره شروع می‌شود، اما سایت IIS چاپخانه متوقف می‌ماند تا دوباره روی پورت ۸۰ مزاحم برنامه قبلی نشود.
bindingهای قبلی خودکار برگردانده نمی‌شوند. پیام خطا و مسیر پشتیبان را نگه دارید.

اگر برنامه قبلی روی http://IP هنوز درست باز نشد، یک بار در پنجره InPrivate/Incognito بررسی کنید؛ redirect دائمی قبلی ممکن است در مرورگر مانده باشد.
اسکریپت وضعیت تاریخی تنظیمات سراسری ARR را نمی‌داند و آن‌ها را حدسی به مقدار دیگری برنمی‌گرداند. برای بررسی بیشتر، نام سایت، bindingها و خطای واقعی برنامه قبلی لازم است.

```powershell
Import-Module WebAdministration
Get-Website | Select-Object Name,State,@{Name='Bindings';Expression={$_.bindings.Collection.bindingInformation -join ', '}}
```

## بررسی انجام‌شده

پشتیبانی HTTP، کوکی ورود، رد Origin مربوط به پورت دیگر، CMS و گردش سفارش با داده آزمایشی بررسی شده‌اند.
پیش‌بررسی اسکریپت برای پورت اشغال، سایت با مسیر متفاوت و ARR غیرفعال به صورت شبیه‌سازی‌شده آزمایش شده است.
اتصال واقعی IIS و فایروال تنها با اجرای اسکریپت روی VPS قابل تأیید است.
