# ارتقای نصب موجود چاپ گیتی به نسخهٔ تعرفه‌های قطع

این راهنما برای همان بسته‌ای است که آخرین migration آن `20261123062444_ProposedCutTariffs` است. نام سرور مورد انتظار و SHA-256 فایل ZIP را از گزارش تحویل مالک بگیرید. فرمان‌های این سند در پنجرهٔ استقرار تأییدشده و در PowerShell مدیر روی همان Windows Server اجرا می‌شوند.

## پیش از پنجرهٔ نگهداری

۱. SHA فایل دریافتی را با مقدار تحویل مستقل مقایسه کنید. بسته را در پوشهٔ تازه و خالی خارج از app و ریشهٔ IIS استخراج کنید. در همان پوشه، فهرست فایل‌ها، SHA تک‌تک فایل‌ها، نسخهٔ Git و موجودی هر دو زنجیرهٔ migration را بررسی کنید. نسخهٔ Git نصب فعلی را از manifest قبلی ثبت کنید.

```powershell
$ErrorActionPreference = 'Stop'
$zip = (Resolve-Path -LiteralPath (Read-Host 'Release ZIP')).Path
$expectedHash = (Read-Host 'SHA-256 from owner').Trim()
if ($expectedHash -notmatch '^[0-9a-fA-F]{64}$' -or (Get-FileHash -LiteralPath $zip).Hash -ne $expectedHash) { throw 'ZIP checksum mismatch' }
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$stage = "C:\Giti\stage-$stamp"
$app = 'C:\Giti\app'
$data = 'C:\Giti\data'
if (Test-Path -LiteralPath $stage) { throw 'Staging directory exists' }
Expand-Archive -LiteralPath $zip -DestinationPath $stage
Set-Location -LiteralPath $stage
node.exe .\scripts\verify-release.mjs
if ($LASTEXITCODE -ne 0) { throw 'Release integrity failed' }
.\backend\publish\ChapGiti.Api.exe --release-metadata
if ($LASTEXITCODE -ne 0) { throw 'Compiled inventory failed' }
(Get-Content .\RELEASE-MANIFEST.json -Raw | ConvertFrom-Json).source
Get-Item -LiteralPath "$app\.env.local","$app\deploy\iis\GitiNode.exe","$app\deploy\iis\GitiNode.xml",$data
Get-Service GitiNode
Get-WindowsFeature Web-WebSockets
Import-Module WebAdministration
Get-Website -Name Giti | Select-Object Name,State,PhysicalPath
Get-WebBinding -Name Giti | Select-Object protocol,bindingInformation,certificateHash
node.exe --version
```

Node باید x64 نسخهٔ ۲۴٫۱۵ یا جدیدتر از سری ۲۴ باشد. بک‌اند self-contained برای win-x64 است. نسخهٔ WinSW، هویت سرویس و ACL پوشه‌های app و data را با نصب قبلی تطبیق دهید. ریشهٔ مستقل پروکسی، URL Rewrite، ARR، گواهی و binding موجود را ثبت کنید. در Parspack مسیرهای احراز‌شده و `/api/realtime/*` باید از cache خارج باشند.

۲. با دستورهای بخش «پذیرش بدون نوشتن داده» اتصال خصوصی فرایند PowerShell را آماده کنید. نام instance را از برگهٔ تحویل بگیرید. بررسی Before باید سرور مورد انتظار، بانک موجود ChapGiti، SQL 2025 Express، compatibility 170 و تاریخچهٔ مرتب را برگرداند. وضعیت واقعی مقصد تعیین می‌کند کدام migrationها معوق‌اند. برای این ارتقا، هر migration معوق خارج از دو شناسهٔ زیر نیاز به بازبینی جداگانه دارد. [کنترل مقصد و اتصال خصوصی](SQL2025-ACCEPTANCE-FA.md)

```powershell
$expectedServer = (Read-Host 'Approved SQL SERVERPROPERTY ServerName').Trim()
.\deploy\sql\check-target.ps1 -ExpectedServer $expectedServer -Phase Before
```

ترتیب دو تغییر **CutTariffVersions** و سپس **ProposedCutTariffs** است. اولی جدول نسخه‌های تعرفه با کلید مرکب، کلید خارجی کاتالوگ و ایندکس پیام‌های خوانده‌نشده را می‌سازد. دومی ستون `is_proposed bit NOT NULL DEFAULT 0` را اضافه می‌کند. هر قطع ثابت فعال و فاقد تاریخچه یک پیشنهاد یازده‌نرخی می‌گیرد. سفارش و پیشنهاد قیمت قبلی باید با مبلغ ثبت‌شده باز شود. پیش از عملیات، تعداد و جمع‌های کنترلی سفارش‌ها و پیشنهادهای قیمت، تاریخچهٔ تعرفه‌های قبلی و تعداد قطع‌های واجد شرایط را ثبت کنید. روی staging هم‌نسخهٔ SQL 2025 Express یک بازیابی و تمرین ارتقا انجام دهید.

۳ و ۴. DBA یک پوشهٔ تازه و خصوصی برای بکاپ روی دیسک قابل نوشتن حساب سرویس SQL فراهم کند. نام فایل زیر را با مسیر جدید همان پنجره جایگزین کنید و نام دقیق instance را کنترل کنید. در یک نشست SSMS متصل به سرور تأییدشده اسکریپت را به صورت یک batch اجرا کنید. فایل مقصد باید جدید باشد. نتیجهٔ هر دو دستور را ثبت کنید.

```sql
USE [ChapGiti];
DECLARE @expected sysname = N'<approved SQL ServerName>';
DECLARE @bak nvarchar(4000) = N'<new absolute backup path with timestamp>.bak';
IF DB_NAME() <> N'ChapGiti' OR CONVERT(sysname,SERVERPROPERTY('ServerName')) <> @expected
    THROW 51230, 'Wrong backup target.', 1;
BACKUP DATABASE [ChapGiti] TO DISK=@bak WITH COPY_ONLY, CHECKSUM, STOP_ON_ERROR, STATS=10;
RESTORE VERIFYONLY FROM DISK=@bak WITH FILE=1, CHECKSUM, STOP_ON_ERROR;
```

۵ تا ۸. فضای آزاد و دسترسی محدود مقصد بکاپ را پیش از کپی تأیید کنید. فایل خصوصی محیط، WinSW و تنظیمات محلی، برنامه و تمام داده‌ها، به‌ویژه PDFهای خصوصی، باید قابل بازیابی باشند. مسیر واقعی proxy و web.config را از IIS بگیرید. تنظیمات IIS همهٔ سایت‌ها را در پشتیبان سیستم ثبت کنید. برای بازگشت این سایت، فقط تغییر همان سایت را برگردانید. شناسهٔ گواهی و binding را ثبت کنید. گواهی با کلید خصوصی طبق رویهٔ امن سرور نگهداری شود.

```powershell
$backup = (Resolve-Path -LiteralPath (Read-Host 'New private backup directory')).Path
$proxy = [Environment]::ExpandEnvironmentVariables((Get-Website -Name Giti).physicalPath)
function Copy-GitiBackup([string]$source,[string]$dest) {
    robocopy.exe $source $dest /E /COPYALL /DCOPY:DAT /XJ /R:2 /W:2 /NFL /NDL /NJH /NJS
    if ($LASTEXITCODE -ge 8) { throw 'Backup copy failed' }
}
Copy-GitiBackup $app "$backup\app"
Copy-GitiBackup $data "$backup\data"
Copy-GitiBackup $proxy "$backup\proxy"
Backup-WebConfiguration -Name "Giti-before-$stamp"
foreach ($file in '.env.local','deploy\iis\GitiNode.exe','deploy\iis\GitiNode.xml') {
    if ((Get-FileHash -LiteralPath "$app\$file").Hash -ne (Get-FileHash -LiteralPath "$backup\app\$file").Hash) { throw 'Private file backup mismatch' }
}
```

بازیابی زمان‌بندی‌شده باید بتواند **باینری قبلی + SQL سازگار + داده و فایل‌های هماهنگ** را با هم برگرداند. تمرین restore در محیط مجاز جدا، مسیر بکاپ، SHA فایل و تأیید DBA را ثبت کنید. VERIFYONLY جای تمرین بازیابی را نمی‌گیرد.

## پنجرهٔ نگهداری و فعال‌سازی

۹. ترافیک سایت Giti را به صفحهٔ نگهداری ببرید و فقط برای UAT دسترسی محدود نگه دارید. سرویس موجود را متوقف کنید. همهٔ workerها، jobها، نمونه‌های CLI و اتصال‌های نویسندهٔ همین برنامه را تخلیه کنید. بک‌اند قدیمی پرچم `is_proposed` را نمی‌شناسد و ممکن است با تعرفهٔ تأییدنشده سفارش بپذیرد. تا پذیرش نسخهٔ جدید مسیر نوشتن عمومی بسته بماند. برای یک نقطهٔ بازیابی هماهنگ، پس از توقف، یک full backup نهایی با مسیر تازه و VERIFYONLY بگیرید و نسخهٔ بکاپ app و data را همگام کنید. شمارش‌ و جمع‌های کنترلی همین لحظه را مبنای مقایسه قرار دهید.

```powershell
Stop-Service GitiNode -ErrorAction Stop
(Get-Service GitiNode).WaitForStatus('Stopped','00:01:00')
if (Get-NetTCPConnection -State Listen -LocalPort 3000,5100 -ErrorAction SilentlyContinue) { throw 'A listener is still active' }
```

۱۰ تا ۱۲. در staging تأییدشده و در همان نشست دارای اتصال خصوصی، Before را تکرار کنید. فهرست معوق، checksumها و بکاپ تأییدشده باید با مجوز این ارتقا منطبق باشند. در صورت خالی بودن فهرست معوق، مستقیم After را اجرا کنید. هر خطای runner نیازمند توقف و بررسی همان history توسط DBA است.

```powershell
Set-Location -LiteralPath $stage
.\deploy\sql\check-target.ps1 -ExpectedServer $expectedServer -Phase Before
# Only when its pending list matches the authorized change window
$env:NODE_ENV = 'production'
node.exe .\scripts\db-setup.mjs
if ($LASTEXITCODE -ne 0) { throw 'Migration failed' }
.\deploy\sql\check-target.ps1 -ExpectedServer $expectedServer -Phase After
```

After باید صفر pending، کلید مرکب و FK کاتالوگ، check constraintهای فعال، ستون و پیش‌فرض پیشنهاد، ایندکس unread و یازده کلید هر پیشنهاد را تأیید کند. شمارش و مبلغ سفارش و quote تاریخی را دوباره مقایسه کنید. تعداد پیشنهادها براساس کاتالوگ واقعی آن سرور است.

۱۳ و ۱۴. در ریشهٔ staging خصوصی و روی NTFS، دو فرمان زیر را اجرا کنید. فرمان اول اطلاعات داخلی Next.js را برای همین نصب می‌سازد. تکرار آن کلید معتبر موجود را نگه می‌دارد. بررسی هش ZIP و فایل‌های pristine پیش از این مرحله انجام می‌شود. برای شروع هر انتشار از staging تازه استفاده کنید. روی نصب موجود فایل آماده‌شدهٔ انتشار دیگری را جایگزین نکنید.

```powershell
Set-Location -LiteralPath $stage
node.exe .\scripts\initialize-frontend.mjs
if ($LASTEXITCODE -ne 0) { throw 'Frontend initialization failed' }
node.exe .\scripts\initialize-frontend.mjs --check
if ($LASTEXITCODE -ne 0) { throw 'Frontend installation check failed' }
```

پس از آماده‌شدن فرانت، فایل‌های خصوصی موجود را با ACL قبلی در همان موقعیت نسبی داخل staging بازگردانید. پوشهٔ data خارج از app می‌ماند. مسیرهای خصوصی و log سفارشی و دسترسی write کش/لاگ سرویس را طبق manifest نصب قبلی کنترل کنید. پیش از تعویض، ACL پوشهٔ app، دسترسی حساب سرویس به کل release و ACL اختصاصی فایل‌های حساس را بررسی کنید. پوشهٔ app قبلی را کنار بگذارید. تعویض زیر مخصوص دو پوشهٔ هم‌والد روی یک دیسک است. دستورات از پوشهٔ والد اجرا می‌شوند تا shell مانع Rename نشود.

```powershell
Set-Location C:\Giti
foreach ($file in '.env.local','deploy\iis\GitiNode.exe','deploy\iis\GitiNode.xml') {
    $from = Join-Path $app $file
    $to = Join-Path $stage $file
    if (Test-Path -LiteralPath $to) { throw 'Unexpected private file in release' }
    Copy-Item -LiteralPath $from -Destination $to
    Set-Acl -LiteralPath $to -AclObject (Get-Acl -LiteralPath $from)
    if ((Get-FileHash -LiteralPath $from).Hash -ne (Get-FileHash -LiteralPath $to).Hash) { throw 'Private file mismatch' }
}
$parent = (Resolve-Path C:\Giti).Path
if ((Split-Path (Resolve-Path $stage).Path) -ne $parent -or (Resolve-Path $app).Path -ne (Join-Path $parent 'app')) { throw 'Unexpected directory layout' }
$prior = "app-before-$stamp"
if (Test-Path -LiteralPath (Join-Path $parent $prior)) { throw 'Previous-version path exists' }
Rename-Item -LiteralPath $app -NewName $prior
Rename-Item -LiteralPath $stage -NewName 'app'
Start-Service GitiNode
(Get-Service GitiNode).WaitForStatus('Running','00:01:00')
```

روی نصب موجود، WinSW را دوباره نصب نکنید. قالب IIS داخل بسته را با web.config فعلی **ریشهٔ proxy** مقایسه کنید. اگر تعریف محلی `system.webServer/webSocket` وجود دارد، پس از بکاپ فقط همان عنصر را از فایل فعال بردارید. تنظیم سرور به ارث می‌رسد. قواعد HTTPS، چهار مسیر SignalR، fallback و catch-all، گواهی، binding و تنظیم سایر سایت‌ها را حفظ کنید. [رفتار WebSocket در IIS](https://learn.microsoft.com/en-us/iis/configuration/system.webserver/websocket/)

## پذیرش و بازگشت

۱۵ و ۱۶. آدرس سلامت داخلی باید SQL Server را سالم گزارش کند. فرانت و HTTPS را در دسترسی محدود UAT بررسی کنید. شناسهٔ Build و assetهای جدید، اعتبار گواهی، redirect و هدرهای cache را در دسترسی مستقیم و از CDN ثبت کنید.

```powershell
Invoke-RestMethod http://127.0.0.1:5100/health
(Invoke-WebRequest -UseBasicParsing http://127.0.0.1:3000).StatusCode
(Invoke-WebRequest -UseBasicParsing https://chapgiti.ir).StatusCode
```

۱۷. با حساب‌های آزمایشی از پیش تأییدشده، ورود رمزی مشتری و `admin` را در دو پروفایل مرورگر جدا بررسی کنید. ۱۸. در پنل مدیر یک **قطع آزمایشی مجاز** را باز کنید. یازده نرخ را ببینید. محاسبهٔ مشتری پیش از تأیید باید پیام انتظار تأیید بدهد. پس از تأیید همان قطع، مبلغ و نسخه را بررسی کنید. مبالغ ۱۵ خدمت تکمیلی و تعرفهٔ قطع‌های دیگر را مقایسه کنید. [سیاست مبالغ و تأیید](CUT-TARIFF-POLICY-FA.md)

۱۹. فقط در دامنهٔ UAT مجاز، یک سفارش آزمایشی و PDF مصنوعی ثبت کنید. از دریافت فایل توسط صاحب سفارش، ممنوعیت دسترسی حساب دیگر و حفظ مبلغ سفارش تاریخی اطمینان بگیرید. ۲۰. اعلان مدیر و مشتری، لینک سفارش، ارسال و دریافت پیام پشتیبانی، unread/read و بازیابی پیام پس از reconnect را در دو نشست کنترل کنید. در DevTools وضعیت WebSocket 101 را ثبت کنید. با WebSocket مسدود، SSE/LongPolling و سپس همگام‌سازی HTTP را هم امتحان کنید. پیامک یا Push واقعی جزو smoke test نیست. پس از تأیید این موارد، دسترسی عمومی را باز کنید.

در شکست پیش از migration، پس از بررسی تاریخچه و اثبات سازگاری می‌توان سرویس قبلی را برگرداند. پس از آغاز migration، نویسنده‌ها بسته بمانند. تاریخچه، علت و نقطهٔ بازیابی را ثبت کنید. با مجوز DBA از بکاپ تأییدشدهٔ همان پنجره و فایل‌ها و باینری متناظر بازیابی کنید. دستور Down یا حذف دستی history برای بازگشت این نسخه مجاز نیست. اگر بعد از بازگشایی سفارش جدید ثبت شده است، اثر بازیابی بر آن سفارش‌ها را پیش از هر restore با مالک مشخص کنید.
