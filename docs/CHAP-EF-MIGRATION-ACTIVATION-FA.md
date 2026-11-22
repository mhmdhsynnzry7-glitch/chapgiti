# راهنمای دستی پذیرش و فعال‌سازی migrationهای EF چاپ گیتی

راهنمای جاری Release خودکفا و کنترل‌های مقصد ۲۰۲۵ در [نصب و ارتقای دیتابیس](DATABASE-MIGRATION-FA.md) آمده است. نمونه‌های SDK و پذیرش آزمایشگاهی در ادامه سابقهٔ توسعه‌اند. migration سوم در Release فعلی `20261118113729_TicketMessageReadState` است.

این راهنما برای سورس پس از پذیرش Onion/EF است. **در وظیفهٔ تهیهٔ این سند، به دیتابیس کاری/سرور وصل نشدیم و migration عملیاتی اجرا نشد.** دستورهای تغییر schema زیر برای اجرای آیندهٔ مالک، پس از بررسی مقصد و مجوز همان محیط‌اند. هیچ ZIP یا Publish تازه‌ای تهیه نشده است.

گزارش پذیرش این مرحله و شرح پیاده‌سازی حدود شواهد را مشخص می‌کنند. مسیر روی SQL Server 2019 LocalDB مستقل آزموده شد. سرور اعلام‌شدهٔ مالک، SQL Server 2025 Express با compatibility 170 و collation `SQL_Latin1_General_CP1_CI_AS` است؛ **پذیرش آن موتور هنوز NOT TESTED/BLOCKED است**. این مشخصات مجوز اتصال ما به آن سرور نیستند.

## ۱. مالکیت schema و ابزار لازم

| مرحله | مالک و وظیفه |
| --- | --- |
| SQL001–009 | `Infrastructure.Persistence.Migrations`؛ فایل‌های ثابت `Data/*.sql`، قفل و تراکنش، ثبت SHA-256 در `dbo.Schema_Migrations` |
| پذیرش legacy | `LegacyEfMigrations`؛ اعتبارسنجی schema/checksum و قفل `ChapGiti.EF.Schema` |
| baseline EF | `20261117090230_Legacy009Baseline`؛ اعتبارسنجی مجدد، بدون ساخت دوبارهٔ جدول قدیمی |
| قابلیت جدید | `20261117092337_CatalogAndMagazine`؛ کاتالوگ چاپ، metadata Articles و alias |
| تغییرهای بعدی | migrationهای بررسی‌شدهٔ EF همان assembly؛ history در `dbo.__EFMigrationsHistory` |

Context: `ChapGiti.Infrastructure.Persistence.Ef.ChapGitiDbContext`؛ migrations assembly همان `ChapGiti.Infrastructure` است. runner جامع `IDatabaseMaintenance.Migrate` ابتدا SQL و سپس EF را اجرا می‌کند. runner SQL از مرز 009 عبور نمی‌کند؛ تغییر بعدی فایل `010.sql` نیست. history قدیمی و EF دو مالک یک تغییر نیستند.

نسخهٔ فعلی EF/Core/Relational/SqlServer/Design و `dotnet-ef` برابر **10.0.15** است. provider برای تولید SQL روی `UseCompatibilityLevel(150)` تنظیم شده؛ این تنظیم **سطح سازگاری دیتابیس را ALTER نمی‌کند**. سطح واقعی 170 مقصد را خودسرانه به 150 تغییر ندهید. پذیرش موتور/سطح مقصد باید مستقل انجام شود.

برای سورس: Windows، SDK .NET 10، Node.js مطابق `frontend/package.json`، وابستگی‌های نصب‌شدهٔ همان lock و SQL موجود لازم‌اند. ابزار EF فقط برای طراحی/کنترل مدل لازم است. برای Release خودکفا: SDK و dotnet-ef لازم نیست؛ exe، dependencyها، resourceهای EF و SQLهای همان build باید در بسته باشند؛ Node برای wrapper موجود لازم است. اگر فقط DLL framework-dependent دارید، runtime ASP.NET Core/.NET 10 متناظر لازم است.

## ۲. پیکربندی چگونه خوانده می‌شود؟

از **ریشهٔ مخزن یا ریشهٔ Release**، `scripts/db-setup.mjs`:

1. `environment()` در `scripts/dotnet-runtime.mjs`، envهای Next را از همان working directory می‌خواند؛ متغیرهای process از فایل‌ها اولویت بالاتری دارند.
2. `SQLSERVER_CONNECTION_STRING` را به `ConnectionStrings__SqlServer` و سایر نام‌های پروژه را به تنظیمات ASP.NET تبدیل می‌کند.
3. در Development ابتدا backend فعلی Debug را build و همان را با `--migrate` اجرا می‌کند.
4. در Production/Release فقط backend منتشرشدهٔ همان بسته را اجرا می‌کند؛ publish/build خودکار انجام نمی‌دهد.

خود اجرای مستقیم `dotnet ChapGiti.Api.dll --migrate`، فایل `.env.local` ریشه را نمی‌خواند. API از تنظیمات ASP.NET، env فرایند و content root کنار اسمبلی استفاده می‌کند. بنابراین با اتکا به وجود فایل `.env.local`، DLL را مستقیم اجرا نکنید؛ wrapper موجود مسیر استاندارد است. Connection string/رمز/API key را در command line، چت، screenshot یا خروجی `Get-Content .env.local` نمایش ندهید.

پیش از اجرا، فایل خصوصی را در ویرایشگر امن با مقصد مورد تأیید تطبیق دهید؛ متغیر process قدیمی مربوط به SQL را نیز بررسی کنید. برای مهاجرت، حساب SQL باید DDL/history و application lock لازم را مجاز داشته باشد. هویت migration ممکن است با هویت سرویس برنامه متفاوت باشد؛ مجوز اجرا را به‌صورت ضمنی از ادمین Windows استنباط نکنید.

`Configure-SqlServer.ps1` فقط preflight نیست: env/ACL و دسترسی حساب SQL را تغییر می‌دهد و migration اجرا می‌کند. در این راهنما آن را برای بررسی خواندنی پیشنهاد نمی‌کنیم. `--import-stdin` و `--admin-setup` نیز migration-only نیستند؛ برای ارتقا یا رفع مشکل history اجرا نشوند.

## ۳. بررسی مقصد در SSMS — فقط SQL خواندنی

در SSMS خودِ محیط مجاز، database selector را روی دیتابیس مورد تأیید بگذارید. این queryها PowerShell نیستند:

```sql
SELECT @@SERVERNAME AS server_name,
       SERVERPROPERTY('MachineName') AS machine_name,
       SERVERPROPERTY('InstanceName') AS instance_name,
       SERVERPROPERTY('ProductVersion') AS product_version,
       SERVERPROPERTY('Edition') AS edition,
       DB_NAME() AS current_database,
       ORIGINAL_LOGIN() AS login_name;
SELECT name, state_desc, compatibility_level, collation_name
FROM sys.databases WHERE name = DB_NAME();
SELECT COUNT(*) AS user_tables FROM sys.tables;
```

نام server، instance، database، نسخه، collation و login را با مقصد داخل تنظیمات خصوصی تطبیق دهید. database پیش‌فرض روی master یا نام درست به‌تنهایی کافی نیست. اگر هویت با برنامهٔ مصوب فرق دارد، قبل از هر migration متوقف شوید. این query در این وظیفه روی سرور مالک اجرا نشده است.

### فهرست historyها در SSMS

این قطعه روی دیتابیس خالی نیز history نمی‌سازد:

```sql
IF OBJECT_ID(N'dbo.Schema_Migrations', N'U') IS NULL
    SELECT N'Legacy history is absent' AS migration_status;
ELSE
    EXEC(N'SELECT name, checksum, applied_at FROM dbo.Schema_Migrations ORDER BY name');

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
    SELECT N'EF history is absent' AS migration_status;
ELSE
    EXEC(N'SELECT MigrationId, ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId');
```

برای legacy، رکورد غایب نسبت به فهرست ۹تایی زیر یعنی pending و checksum متفاوت یعنی **توقف برای بررسی**. برای EF، رکورد غایب نسبت به migrationهای موجود در assembly همان نسخه یعنی pending. هیچ رکورد history را دستی INSERT/UPDATE نکنید؛ نبود history در دیتابیس دارای جدول‌های برنامه، مجوز نام‌گذاری آن به‌عنوان نصب تازه نیست.

### checksumهای فعلی legacy

الگوریتم runner: متن فایل با UTF-8 خوانده، `CRLF` به `LF` تبدیل و سپس SHA-256 محاسبه می‌شود. در نسخهٔ فعلی raw hashها نیز با این مقادیر یکسان‌اند. `Get-FileHash` فقط hash بایت خام می‌دهد؛ برای فایل دارای newline متفاوت جایگزین محاسبهٔ runner نیست.

| فایل | SHA-256 نرمال‌شده |
| --- | --- |
| `001_initial.sql` | `17fe9362f712c76d2d91a63bd49245befd81a83556c0487fd6ce89ee51c8529b` |
| `002_customer_otp.sql` | `624a23a1bdb3a3548e3ad23cb5e081adc907878627838e4602d9b2eca62f2252` |
| `003_unified_authentication.sql` | `55daaf193b1bd091c8e4cfc076e9fdddc41a541fce4489853ef5fb46b8dd3c88` |
| `004_administrator_login_email.sql` | `d02ef35686d2ca494b69409cfed8a8ee183a53ec14ad38b25420a14d72f02c21` |
| `005_order_support.sql` | `c506c962b18edfafe2b5acf777c12d4b6cba7699f2b6dad0371b6ae5646cba0f` |
| `006_order_quotes.sql` | `fb1f9364ce3f32a30d506e38d79b514c1ad44cc481c7fe54039bfda239b26819` |
| `007_in_app_notifications.sql` | `f795d6192ebae861ad63238dc383d4a377d68c27178cb279cb23787c91c4e8d2` |
| `008_order_corrections.sql` | `f6d987417eb1d59d53bf1488a8c0e69d2e8030b9fc885dcfd824d8b86c61381b` |
| `009_order_views.sql` | `1a936768618561a10423d6f77104ac810b842ed252786b69948937bf8efa69e1` |

### فهرست سورس EF — PowerShell، بدون اتصال SQL

از `C:\src\chapgiti`:

```powershell
dotnet tool restore --ignore-failed-sources
dotnet ef --version
dotnet build .\backend\ChapGiti.Infrastructure --configuration Verification
dotnet ef migrations list --no-connect --project .\backend\ChapGiti.Infrastructure --startup-project .\backend\ChapGiti.Infrastructure --context ChapGitiDbContext --configuration Verification --no-build
dotnet ef migrations has-pending-model-changes --project .\backend\ChapGiti.Infrastructure --startup-project .\backend\ChapGiti.Infrastructure --context ChapGitiDbContext --configuration Verification --no-build
```

Design factory فقط اتصال طراحی غیرقابل‌استفادهٔ loopback دارد و `.env.local` را نمی‌خواند. `--no-connect` باید حفظ شود. پیام «Pending status not shown» در این حالت طبیعی است: این فرمان فهرست **سورس** است و applied/pending دیتابیس را اثبات نمی‌کند. برای وضعیت واقعی، history خواندنی بالا را در همان مقصد بررسی کنید. هیچ `dotnet ef database update` در این فرایند توصیه نمی‌شود؛ اجرای مستقیم آن می‌تواند بررسی هماهنگ legacy/EF را دور بزند.

## ۴. مسیر A — نصب تازه

پیش‌شرط‌ها: مقصد دقیق تأییدشده، دیتابیس از قبل توسط مسئول SQL ایجادشده و واقعاً خالی، پیکربندی خصوصی صحیح، مجوز تغییر schema و recovery plan متناسب محیط. runner برنامه دیتابیس نمی‌سازد. دیتابیس دارای داده را drop/recreate یا reseed نکنید. روی موتور 2025 ابتدا همین مسیر در زیرساخت disposable مستقل آن موتور پذیرفته شود.

### از سورس — PowerShell

```powershell
Set-Location -LiteralPath 'C:\src\chapgiti'
if (Test-Path -LiteralPath '.\RELEASE-MANIFEST.json') { throw 'This command requires the source checkout, not a Release root.' }
$previousNodeEnvironment = [Environment]::GetEnvironmentVariable('NODE_ENV', 'Process')
try {
    $env:NODE_ENV = 'development'
    node.exe .\scripts\db-setup.mjs
    if ($LASTEXITCODE -ne 0) { throw 'Migration failed; inspect the error before continuing.' }
} finally {
    [Environment]::SetEnvironmentVariable('NODE_ENV', $previousNodeEnvironment, 'Process')
}
```

این فرمان ابتدا build محلی Debug می‌گیرد و سپس maintenance همان سورس را اجرا می‌کند. root `npm run build` یا اسکریپت publish را برای مهاجرت اجرا نکنید. ابتدا SQL001–009، سپس baseline و در نهایت CatalogAndMagazine اعمال می‌شوند. بدون خطا، انتظار فعلی ۹ رکورد legacy و ۲ رکورد EF است. فقط ۸۰ گزینهٔ فنی موجود در ۸ گروه initialize می‌شوند؛ تعرفه/نویسنده/مقاله/حساب مدیر جعلی ساخته نمی‌شود.

پس از نصب schema در دیتابیس خالی، `--check` می‌تواند با «No administrator is configured» متوقف شود. این پیام به‌معنای شکست migration نیست؛ setup اولیهٔ حساب مدیر یک عملیات جداگانهٔ مصوب است. از آن برای reset رمز حساب موجود استفاده نکنید.

## ۵. مسیر B — ارتقای دیتابیس legacy موجود

از دیتابیس موجود استفاده کنید؛ ساخت دیتابیس جایگزین، حذف حساب/سفارش، Import مجدد یا دست‌کاری history لازم نیست. پیش از اجرا، هویت مقصد، history/checksumها، سازگاری schema، پنجرهٔ نگهداری و backup/recovery متناسب همان محیط را بررسی کنید. در SQL2025 پذیرش isolated مقصد هنوز پیش‌نیاز حل‌نشده است.

در محیط آینده، نسخهٔ قدیمی برنامه هنگام تغییر schema نباید هم‌زمان mutation انجام دهد؛ هماهنگی توقف/شروع با مالک زیرساخت جدا از این سند است. این وظیفه هیچ سرویسی را متوقف/تغییر نداده است.

فرمان **همان wrapper مسیر A** از ریشهٔ سورس است. اگر legacy001–009 همگی applied باشند، checksumها بررسی و از DDL قدیمی صرف‌نظر می‌شود؛ اگر بعضی pending باشند، به ترتیب فایل‌ها اجرا می‌شوند و بعد EF شروع می‌شود. فایل جدید SQL010 درست نکنید. در دیتابیس legacy پذیرفته‌شده، baseline جدول قدیمی را بازسازی نمی‌کند و نسخهٔ دوم فقط قابلیت‌های جدید را اضافه می‌کند.

از ساختگی‌کردن `Schema_Migrations` یا `__EFMigrationsHistory` برای ردشدن preflight اجتناب کنید. baseline schema واقعی، column/type/nullability، key/index، FK trusted، CHECK/default و computed email را کنترل می‌کند. label/ابعاد/قیمت quote و فایل‌های تاریخی نباید با کاتالوگ امروز بازنویسی شوند.

در schema قدیمی Articles وضعیت archived مستقل وجود ندارد؛ `published=0` به draft و `published=1` به published منتقل می‌شود. نویسنده/تاریخ نامعلوم خالی می‌ماند. مقالهٔ archived پس از پذیرش EF در rerun، archived باقی می‌ماند. قبل از baseline نمی‌توان بدون جعل schema، catalog ویرایش‌شدهٔ SQL جدید یا وضعیت archive جدید ساخت؛ حفظ آن‌ها در rerun بعدی آزمایش شده است.

## ۶. مسیر C — تغییرهای عادی بعدی EF

تغییر مدل، مجوز تغییر دیتابیس نیست. توسعه‌دهنده از ریشهٔ مخزن، بدون اتصال به دیتابیس کاری، پس از تغییر موجهٔ مدل/Fluent mapping:

```powershell
dotnet tool restore --ignore-failed-sources
dotnet ef migrations add MeaningfulChange --project .\backend\ChapGiti.Infrastructure --startup-project .\backend\ChapGiti.Infrastructure --context ChapGitiDbContext --configuration Verification --output-dir Persistence/Ef/Migrations
dotnet ef migrations has-pending-model-changes --project .\backend\ChapGiti.Infrastructure --startup-project .\backend\ChapGiti.Infrastructure --context ChapGitiDbContext --configuration Verification
```

`MeaningfulChange` مثال نام تغییر آینده است؛ برای اجرای برنامهٔ فعلی migration تازه نسازید. Up/Down، Designer و snapshot باید بررسی و همراه کد در Git ثبت شوند؛ nullable/default، واحد مبلغ، index/FK و حفظ داده در تست fresh و upgrade واقعی بررسی شوند. این وظیفه commit/push انجام نداده است.

مالکیت جدید EF است؛ SQL001–009 و checksum/history قبلی نباید ویرایش شوند. برای تغییر key/index قدیمی با نام تولیدی SQL Server، نام فیزیکی واقعی مقصد و قرارداد validation باید صریحاً بازبینی شوند. اعتبارسنجی legacy در هر اجرای جامع انجام می‌شود؛ migration آینده‌ای که عمداً شکل legacy را تغییر می‌دهد نیازمند طراحی نسخه‌سازگارِ همان validation است، نه حذف بررسی یا درج history دستی.

پس از مجوز اجرا در مقصد، wrapper مسیر A/B در سورس یا wrapper Release پایین، همهٔ migrationهای pending همان assembly را به ترتیب اجرا می‌کند. این CLI گزینه‌ای برای فرض‌کردن migration «010» یا انتخاب دستی یک نسخهٔ ساختگی ندارد.

## ۷. اجرای آینده از Release، بدون سورس/SDK

**ZIPهای قبلی پروژه متعلق به build پیش از EF هستند و برای فعال‌سازی این سورس مناسب نیستند.** این بخش برای Release جدیدی است که بعداً با مجوز جداگانه ساخته و پذیرفته می‌شود؛ در این task چنین بسته‌ای ساخته/آزموده نشد.

بستهٔ آینده باید `backend/publish/ChapGiti.Api.exe`، dependencyهای همان build از جمله Infrastructure/EF/SqlServer، `Data` حاوی هر ۹ SQL و resourceهای کامپایل‌شدهٔ baseline/catalog، wrapperهای `scripts/db-setup.mjs`/`dotnet-runtime.mjs` و dependency env مورد استفاده در frontend را حمل کند. کپی‌کردن صرف فایل `.cs` migration در کنار exe آن را اجرایی نمی‌کند؛ migration باید در assembly همان نسخه کامپایل شده باشد.

پس از استخراج و تطبیق manifest/checksum بسته و مقصد امن، پیکربندی خصوصی را جداگانه در ریشهٔ نصب نگه دارید. example مسیر پیش‌فرض مستند است، دامنه یا instance واقعی فرض نشده است:

```powershell
Set-Location -LiteralPath 'C:\Giti\app'
if (-not (Test-Path -LiteralPath '.\backend\publish\ChapGiti.Api.exe')) { throw 'Matching published backend is missing.' }
$previousNodeEnvironment = [Environment]::GetEnvironmentVariable('NODE_ENV', 'Process')
try {
    $env:NODE_ENV = 'production'
    node.exe .\scripts\db-setup.mjs
    if ($LASTEXITCODE -ne 0) { throw 'Migration failed; do not start mixed application versions.' }
} finally {
    [Environment]::SetEnvironmentVariable('NODE_ENV', $previousNodeEnvironment, 'Process')
}
```

در Release واقعی `RELEASE-MANIFEST.json` و environment Production مانع انتخاب مسیر build توسعه می‌شوند. این wrapper فقط maintenance را اجرا می‌کند و IIS یا Windows Service نصب/تنظیم نمی‌کند. تست فعلی همان CLI را از خروجی build Verification اجرا کرد؛ پذیرش exe خودکفای Release آینده و SQL2025 همچنان باید در مرحلهٔ مجاز بعدی انجام شود.

## ۸. postflight — فقط SQL خواندنی در SSMS

دوباره هویت بخش ۳ و دو history را بررسی کنید. در نسخهٔ فعلی باید ۹ checksum مطابق جدول، baseline و feature EF مطابق سورس و جدول‌های زیر موجود باشند:

```sql
SELECT name FROM sys.tables
WHERE schema_id = SCHEMA_ID(N'dbo')
ORDER BY name;
SELECT kind, COUNT(*) AS options,
       SUM(CASE WHEN active=1 AND is_default=1 THEN 1 ELSE 0 END) AS active_defaults
FROM dbo.Print_Catalog_Options GROUP BY kind ORDER BY kind;
SELECT kind, code, active, is_default, display_order, version
FROM dbo.Print_Catalog_Options ORDER BY kind, display_order, code;
SELECT publication_status, COUNT(*) AS articles
FROM dbo.Articles GROUP BY publication_status;
SELECT name, is_disabled, is_not_trusted FROM sys.foreign_keys
WHERE is_disabled=1 OR is_not_trusted=1;
SELECT name FROM sys.indexes
WHERE name IN (N'UX_PrintCatalog_Code',N'UX_PrintCatalog_Default',N'IX_PrintCatalog_List',
               N'IX_Articles_PublicList',N'IX_Articles_CategoryStatus',N'IX_ArticleAliases_Article')
ORDER BY name;
```

در نصب تازه دقیقاً ۸۰ گزینه/۸ گروه با یک default فعال برای هر گروه انتظار می‌رود. پس از ویرایش مدیر، تعداد یا ترتیب را به defaults برنگردانید. در خروجی فعلی ۳۵ جدول dbo داریم؛ این عدد برای migrationهای آینده ثابت دائمی نیست.

داده‌های قبلی سفارش/quote/correction/file/notification/view و tarif را با snapshot امن قبل از ارتقا مقایسه کنید؛ اطلاعات شخصی را در log عمومی صادر نکنید. مثال بررسی رابطهٔ بدون orphan:

```sql
SELECT COUNT(*) AS orphaned_quotes
FROM dbo.Order_Quotes q LEFT JOIN dbo.Orders o ON o.id=q.order_id WHERE o.id IS NULL;
SELECT COUNT(*) AS orphaned_files
FROM dbo.Files f LEFT JOIN dbo.Orders o ON o.id=f.order_id WHERE o.id IS NULL;
SELECT COUNT(*) AS orphaned_aliases
FROM dbo.Article_Slug_Aliases a LEFT JOIN dbo.Articles r ON r.id=a.article_id WHERE r.id IS NULL;
SELECT COUNT(*) AS orphaned_corrections
FROM dbo.Order_Corrections c LEFT JOIN dbo.Orders o ON o.id=c.order_id WHERE o.id IS NULL;
```

`--check` در سورس فعلی کل history و checksumهای legacy، شناسه‌های EF همان اسمبلی، قرارداد فیزیکی baseline و وجود مدیر را کنترل می‌کند. برای پذیرش کاربردی، queryهای بالا و APIهای کاتالوگ و مجله را نیز بررسی کنید. از ریشهٔ سورس پس از build Debug متناظر، فرمان زیر با همان env mapper اجرا می‌شود. راهنمای انتخاب دیتابیس توسعه و آزمون SQL2025

```powershell
node.exe --input-type=module -e "import {environment,cli} from './scripts/dotnet-runtime.mjs'; await cli(['--check'],environment(true),{development:true});"
```

در Release متناظر، همان فرمان با `environment(false)` و `{development:false}` استفاده می‌شود. هیچ رمز یا connection string در این command نیست. این check را در این task روی تنظیمات کاری اجرا نکرده‌ایم.

## ۹. خطا و بازیابی

هر SQL قدیمی transaction جداگانه دارد؛ کل زنجیره یک transaction واحد نیست. EF نیز تغییر هر migration را با history هماهنگ انجام می‌دهد. بنابراین موفقیت بعضی مراحل قبل از خطای مرحلهٔ بعد ممکن است؛ history را بخوانید، علت را رفع و **همان runner** را دوباره اجرا کنید. رکوردهای موفق را حذف نکنید.

| پیام/کد | اقدام |
| --- | --- |
| database باید از قبل موجود باشد، SQL4060/911 | instance/database/مجوز را بررسی کنید؛ runner دیتابیس نمی‌سازد |
| `Applied SQL Server migration was modified` | فایل و history مصوب را تطبیق دهید؛ checksum ذخیره‌شده را عوض نکنید |
| SQL51100/51101 | prerequisites/history legacy ناقص یا نامطابق؛ baseline را دستی ثبت نکنید |
| SQL51102–51106/51109 | قرارداد ستون، index/key، FK، CHECK/default یا computed email نامطابق؛ با schema واقعی بررسی شود |
| SQL51107 یا قفل EF در دسترس نیست | اجرای هم‌زمان را بررسی کنید؛ lock را خودسرانه حذف نکنید |
| SQL2714 در feature migration | object متداخل؛ در دیتابیس کاربر جدول را برای عبور از خطا حذف نکنید |
| `PRINT_CATALOG_UNAVAILABLE` / `MAGAZINE_UNAVAILABLE` | assembly/history/schema همان نسخه را کنترل کنید؛ fallback ثابت یا seed/reset راه‌حل نیست |
| `No administrator is configured` | schema ممکن است کامل باشد؛ setup مجاز مدیر را جداگانه بررسی کنید |

تست rollback با object متداخل فقط در disposable انجام شده است؛ مجوز تکرار خرابی روی دادهٔ واقعی نیست. baseline، Down خودکار ندارد. Down قابلیت جدید جدول/metadata را حذف می‌کند و مسیر rollback داده‌دار توصیه‌شده نیست. بازیابی باید بر مبنای backup معتبر، نسخهٔ برنامهٔ سازگار و برنامهٔ بررسی‌شده انجام شود؛ هیچ دستور حذف/بازسازی تاریخچه در این راهنما وجود ندارد.

## ۱۰. شروع UAT محلی پس از فعال‌سازی مجاز

این بخش پس از مجوز جداگانه و تأیید postflight انجام می‌شود؛ task فعلی برنامه‌ای با اتصال کاری اجرا نکرده است. از سورس همسان و ریشهٔ مخزن، `npm.cmd run dev` همان `scripts/dev-dotnet.mjs` را اجرا می‌کند: build Debug، check خواندنی و شروع frontend/backend؛ migration خودکار ندارد. originهای واقعی از تنظیمات امن پروژه می‌آیند؛ پیش‌فرض‌ها frontend `http://127.0.0.1:3000` و API `http://127.0.0.1:5100` هستند.

در UAT بدون پیامک، ارسال واقعی خاموش و verification واقعی حفظ شود؛ کد دلخواه یا bypass برای راحتی تست فعال نکنید. ثبت‌نام OTP را با fixture/transport آزمایشی مجاز موجود بررسی کنید. حساب واقعی را برای تست reset نکنید.

1. سلامت `/health` و ورود مستقل `/customer` و `/admin/login` با حساب مجاز موجود.
2. `/admin/print-settings`: ویرایش label/ابعاد/default/active/order؛ مشاهدهٔ نتیجه و refetch در `/calculator` و `/order` و خطای انتخاب غیرفعال.
3. کنترل سفارش/quote پذیرفته‌شدهٔ تاریخی، correction/file انتخاب‌شده و جلوگیری از production با correction حل‌نشده یا quote جدید تأییدنشده.
4. `/admin/articles`: draft، publish، تغییر slug و unpublish/archive؛ کنترل `/blog`، detail، alias و sitemap؛ draft و archived نباید ظاهر شوند.
5. unread اعلان و unread سفارش مستقل، reload/reconnect و نشست جدید؛ archive تیکت خواندنی و mutationها 410 بمانند.

Next ممکن است برای notFound پس از آغاز streaming، HTTP200 همراه صفحهٔ not-found و robots noindex بدهد؛ API مقالهٔ غیرعمومی همچنان باید 404 بدهد و متن/عنوان مقاله نباید در صفحه بماند. این رفتار مطابق [مستند رسمی Next.js](https://nextjs.org/docs/app/api-reference/file-conventions/not-found) است و نباید صرفاً با تغییر status assertion، نشت محتوا پنهان شود.

پس از UAT، نتیجهٔ واقعی محیط هدف و محدودیت‌های SQL2025/Release آینده جداگانه ثبت شوند. هیچ موفقیت آزمون مستقل، تأیید فعال‌شدن دیتابیس کاری نیست.
