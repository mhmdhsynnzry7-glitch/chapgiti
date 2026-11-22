# پذیرش EF Core و مرز مالکیت schema

این راهنما مربوط به سورس ۲۰۲۶/۱۱/۱۷ است. این وظیفه هیچ مهاجرتی روی دیتابیس کاری اجرا نکرده و هیچ خروجی Publish/ZIP نساخته است. اجرای دستورهای تغییر schema در آینده به بررسی مقصد و مجوز جداگانهٔ مالک نیاز دارد.

راهنمای اجرایی تکمیل‌شدهٔ fresh/upgrade/update و بارگذاری امن تنظیمات در [فعال‌سازی migrationهای EF](CHAP-EF-MIGRATION-ACTIVATION-FA.md) و نتیجهٔ پذیرش مسیر واقعی CLI در گزارش پذیرش آمده است. برای اجرای آینده، wrapper همان سند مسیر استاندارد است؛ اجرای مستقیم DLL زیر فقط وقتی معتبر است که تنظیمات ASP.NET صریحاً به process داده شده باشند. وجود `.env.local` ریشه به‌تنهایی برای اجرای مستقیم DLL کافی نیست.

## معماری واقعی

Domain قوانین مستقل چاپ، قیمت، گردش سفارش و انتشار مقاله را نگه می‌دارد. Application قراردادها و use caseها را هماهنگ می‌کند. Infrastructure شامل EF Core **10.0.15**، SQL Server provider، Fluent mappings، Repositoryها، فایل و سرویس پیامک است. API فقط HTTP و احراز هویت و composition root را نگه می‌دارد. کنترلرها هیچ DbContext یا SqlConnection دریافت نمی‌کنند. فرانت همچنان پروژهٔ مستقل React/Next.js است.

`ChapGitiDbContext` در `backend/ChapGiti.Infrastructure/Persistence/Ef` قرار دارد. مدل پایه از SQL Server **2019 LocalDB 15.0.4382.1** جداگانه و schema حاصل از SQL001–009 استخراج شد؛ دیتابیس کاری برای استخراج مدل خوانده نشد. ۳۲ جدول قدیمی، ۲۱۸ ستون، ۷۷ index/key، ۳۷ FK، ۳۲ CHECK و defaultها بررسی شدند. کاتالوگ و alias مقاله دو جدول جدیدند و Articles موجود توسعه می‌یابد. مبالغ همان `bigint` با واحد تومان باقی مانده‌اند.

`SqlStore` اکنون مالکیت اتصال/تراکنش را از DbContext مشترک می‌گیرد. Repositoryهای EF و SQL محدود روی همان اتصال و تراکنش serializable عمل می‌کنند. INSERT با SaveChanges و خواندن‌ها عمدتاً با AsNoTracking و projection هستند؛ تغییر نسخه با شرط version و ExecuteUpdate انجام می‌شود. پاک‌سازی ChangeTracker در مرز کار انجام می‌شود و اعلان زنده تنها بعد از commit و پایان تراکنش منتشر می‌شود. retry کل تراکنش تجاری فعال نشده است.

## ترتیب و تاریخچه‌ها

ترتیب پذیرش:

1. SQLهای تغییرناپذیر `001_initial.sql` تا `009_order_views.sql` با runner قدیمی و SHA-256 متن UTF-8 با newline نرمال‌شده اجرا/کنترل می‌شوند.
2. `20261117090230_Legacy009Baseline` فقط قرارداد schema را اعتبارسنجی می‌کند؛ جدول‌های قدیمی را دوباره نمی‌سازد و داده‌ای را بازنویسی نمی‌کند.
3. `20261117092337_CatalogAndMagazine` کاتالوگ چاپ، metadata انتشار Articles و Article_Slug_Aliases را اضافه می‌کند.

تاریخچهٔ قدیمی در `Schema_Migrations` باقی می‌ماند؛ EF تاریخچهٔ استاندارد خودش را در `__EFMigrationsHistory` نگه می‌دارد. EF history استاندارد، checksum فایل SQL قدیمی نیست. هیچ `010_*.sql` جدیدی توسط runner قدیمی پذیرفته نمی‌شود. `DatabaseMaintenance.Migrate` تنها مسیر صریحِ هماهنگ اجرای دو بخش است. برنامه در startup مهاجرت اجرا نمی‌کند؛ `EnsureCreated` وجود ندارد و ساخت دیتابیس عملیاتی خودکار ممنوع است.

پیش از ثبت baseline، `Legacy009Baseline.sql` نام/تعداد و checksum هر ۹ migration، نوع/طول/precision/nullability/identity/computed ستون‌ها، کلیدها، ترتیب و فیلتر indexها، FKهای فعال و trusted و رفتار حذف، CHECKها، defaultها و عبارت computed ایمیل مدیر را کنترل می‌کند. خرابی قرارداد باعث توقف می‌شود؛ داشتن history به‌تنهایی کافی نیست. مدیر migration پیش از EF همین کنترل را اجرا می‌کند و خود baseline نیز دوباره آن را اجرا می‌کند.

نام‌های خودکار PK/UQ/FK در SQL Server بین نصب‌ها متفاوت‌اند؛ baseline شکل واقعی آن‌ها را مقایسه می‌کند. نام‌های logical استخراج‌شده در مدل، مجوز تغییر خودکار نام فیزیکی قدیمی نیستند. در migration آینده‌ای که بخواهد PK/UQ/index قدیمی را حذف/تغییر دهد، باید نام واقعی با `sys.*` و mapping مربوطه صریحاً بازبینی شود. migration جدید این وظیفه هیچ کلید یا index قدیمی را حذف یا rename نمی‌کند.

## نصب تازه در آینده

مقصد باید از قبل توسط مسئول SQL ساخته و هویت آن تأیید شده باشد. مسیر maintenance، زنجیرهٔ قدیمی را داخل همان مقصد اجرا و سپس baseline و migration EF را اعمال می‌کند. خالی‌بودن برنامه به معنی ایجاد مقاله، نویسنده یا تعرفهٔ جعلی نیست. تنها ۸۰ گزینهٔ فنی موجود با IDهای قطعی به جدول کاتالوگ منتقل می‌شوند؛ تعرفه‌ها در Settings باقی می‌مانند.

اگر Import قدیمی صریحاً لازم باشد، همچنان مقصدِ دارای اطلاعات برنامه رد می‌شود. تنها نشانگر سیستمی `order_seen_baseline_sequence` متعلق به SQL009 از تشخیص «دادهٔ موجود» مستثنا و مقدار خود مقصد حفظ می‌شود. مقالهٔ قدیمی از published واقعی خود به وضعیت مناسب نگاشت می‌شود؛ نویسنده/تاریخ انتشار نامعلوم خالی می‌ماند. سفارش واردشدهٔ قدیمی بدون نسخهٔ workflow صریح، نسخهٔ ۱ می‌گیرد. اجرای Import جزء این وظیفه روی دیتابیس کاری نبوده است.

## ارتقای دیتابیس موجود در آینده

ابتدا server/catalog، مجوز، backup مطابق سیاست محیط، checksumهای SQL001–009 و schema واقعی کنترل شوند. سپس maintenance صریح، migrationهای معوق legacy و EF را به ترتیب اعمال می‌کند. startup سورس فعلی کل history و baseline را کنترل می‌کند و با EF معوق پیش از سرویس‌دهی متوقف می‌شود. اجرای توسعه و تشخیص migrationهای معوق

baseline برگشت خودکار ندارد. rollback داده‌دار باید با برنامهٔ بازگشت و backup بررسی‌شده انجام شود؛ `database update 0` مسیر مجاز بازگشت به schema قدیمی نیست.

## دستورهای توسعه و کنترل مدل، بدون اتصال SQL

از ریشهٔ مخزن:

```powershell
dotnet tool restore
dotnet ef migrations list --no-connect --project backend/ChapGiti.Infrastructure --context ChapGitiDbContext
dotnet ef migrations has-pending-model-changes --project backend/ChapGiti.Infrastructure --context ChapGitiDbContext
dotnet ef migrations add MeaningfulChange --project backend/ChapGiti.Infrastructure --context ChapGitiDbContext --output-dir Persistence/Ef/Migrations
dotnet build backend/ChapGiti.Api --configuration Verification
```

فرمان `migrations add` فقط برای تغییر schema بعدی است؛ برای اجرای برنامه migration تازه نسازید. Design factory فقط یک اتصال غیرقابل‌استفادهٔ طراحی دارد، `.env.local` را نمی‌خواند و به دیتابیس کاری وصل نمی‌شود. `migrations list` را برای فهرست سورس حتماً با `--no-connect` اجرا کنید. فایل migration، Designer و ModelSnapshot باید همراه تغییرات در Git ثبت شوند. خروجی `has-pending-model-changes` در پایان این کار بدون تغییر مدل است.

مسیر اجرای آینده پس از مجوز و تنظیم امن connection string، فایل و origin صحیح، از backend ساخته‌شدهٔ همان سورس است:

```powershell
# تغییر schema؛ فقط پس از تأیید مقصد و مجوز اجرای مستقل در محیط هدف
dotnet backend/ChapGiti.Api/bin/Verification/net10.0/ChapGiti.Api.dll --migrate
dotnet backend/ChapGiti.Api/bin/Verification/net10.0/ChapGiti.Api.dll --check
```

این دو فرمان در این وظیفه روی تنظیمات کاری اجرا نشده‌اند. کلید API، رمز و connection string واقعی را در خط فرمان/سند نگذارید. در Release آینده باید اسمبلی Infrastructure شامل migrationهای کامپایل‌شده و resourceهای baseline/کاتالوگ و Data/SQL001–009 همان build حمل شوند؛ bundle یا بستهٔ تازه‌ای در این وظیفه تولید نشده است.

## فهرست مسیرهای SQL باقی‌مانده

EF مسیر اصلی CRUD و query عادی است، نه پوشش نمایشی روی Repositoryهای قبلی. این استثناها عمداً روی اتصال و UoW مشترک مانده‌اند:

| محل | عملیات باقی‌مانده و علت |
| --- | --- |
| AccountRepository | قفل بازهٔ حساب/username برای ثبت اتمیک؛ MERGE/HOLDLOCK ابزار صریح setup مدیر. خواندن، ایجاد، پروفایل، رمز و revoke عادی EF هستند. |
| AccessSessionRepository | فقط sp_getapplock امنیت؛ session/refresh resolve، rotation و revoke با EF. |
| SessionRepository | MERGE/OUTPUT اتمیک شمارندهٔ rate limit؛ session و audit عادی EF. |
| OrderRepository | LockExistingOrder و LockOwnedOrder برای idempotency/مالکیت فایل؛ درج و فهرست و جزئیات و رویداد و فایل EF. |
| AdminOrderRepository | قفل سفارش و receipt یادداشت؛ dashboard، فیلتر/صفحه‌بندی، export، نسخه و یادداشت EF. |
| OrderQuoteRepository | فقط قفل receipt فرمان؛ تاریخچه/درج/تصمیم پیشنهاد و metadata سفارش EF. |
| OrderCorrectionRepository | SQL دستی ندارد؛ تاریخچه، فایل، انتقال و شرط نسخه EF هستند. |
| OrderViewRepository | aggregate دقیق distinct سفارش/حد پایه با CTE/APPLY و قفل monotonic acknowledgement؛ خواندن sequence و نوشتن view با EF. |
| NotificationRepository | INSERT/OUTPUT با UPDLOCK/HOLDLOCK برای dedupe اتمیک و sequence؛ گیرنده، صفحه‌بندی، count و read/read-all با EF. |
| ContentRepository / AdminContentRepository | قفل بازهٔ Settings برای upsert/ویرایش serializable؛ دادهٔ Settings و asset با EF. writer قدیمی مقاله حذف شده است. |
| UserManagementRepository | SQL دستی ندارد؛ projection، جست‌وجو، شمارش و نقش با EF، بدون hash در read model. |
| OtpRepository | مسیر امنیتی CHECK-aware با قرارداد IOtpSqlExecutor، قفل موبایل/challenge، encoding grant، انقضا/تلاش/مصرف اتمیک و cleanup محدود باقی مانده است؛ فرایند OTP بازنویسی نشده و همان اتصال EF را استفاده می‌کند. |
| CustomerActivityRepository | UNION/CTE و تبدیل امن epoch برای تاریخچهٔ واقعی چندمنبعی، صفحه‌بندی و count در SQL؛ هیچ write ندارد. |
| TicketRepository | projection آرشیو با OUTER APPLY آخرین پیام، مالکیت و صفحه‌بندی؛ متدهای تاریخی mutation در مسیر فعال API نیستند و endpointها همچنان authenticated 410 می‌دهند. |
| PrintCatalogRepository / MagazineRepository | probe قابلیت schema و قفل گروه/نشانی؛ CRUD، فیلتر و projection اصلی EF است. |
| SmsService | dedupe و وضعیت رسیدِ مسیر موجود پیامک سفارش؛ provider بازطراحی نشده و این مسیر در آزمون‌ها با transport خاموش/ساختگی جایگزین است. |
| SqlStore / DatabaseMaintenance | adapter محدود پارامتری، probeهای schema و readiness؛ مالک اتصال/تراکنش DbContext است. |
| Migrations / LegacyEfMigrations | تاریخچه/checksum/DDL قدیمی، قرارداد baseline و applock اجرای صریح. |
| Importer | ورود صریح snapshot تاریخی با allowlist جدول/ستون و guard خالی‌بودن؛ ابزار runtime CRUD یا مسیر خودکار نیست. |

SQLها پارامتری‌اند؛ نام ستون گیرنده و نام جدول Import فقط از allowlist ثابت می‌آیند. این استثناها به معنی تکمیل صددرصد ORM نیستند و در گزارش نهایی پنهان نشده‌اند.

## آزمون ایمن

```powershell
dotnet run --project backend/tests/ChapGiti.AuthenticationTests --configuration Verification
node scripts/test-architecture.mjs
node tests/print-catalog.mjs
dotnet run --project tests/sql005 --configuration Verification -- --authorize-disposable-ef-modernization
dotnet run --project tests/sql005 --configuration Verification -- --authorize-disposable-localdb-008-009
```

دو فرمان آخر فقط fixture مستقلِ opt-in هستند: نام DB و LocalDB تولیدی، بدون ورودی connection string کاربر، guard قبل از write/API و حذف تأییدشده. فرمان‌های قدیمی `tests/customer-accounts.mjs`، `tests/reference-features.mjs`، `test:db` و runtime عمومی را در batch این کار اجرا نکنید. آزمون‌های write/خرابی/هم‌زمانی روی دیتابیس کاری مجاز نیستند.

نسخهٔ واقعی ثبت‌شده در هر دو سناریوی fresh و upgrade برابر **15.0.4382.1** و `compatibility_level` واقعی هر دو دیتابیس آزمایشی **150** بود. SQL Server provider نیز با `UseCompatibilityLevel(150)` تنظیم شده است. SQL Server 2025 Express در این محیط نصب/آزموده نشده است. پذیرش 2019 اثبات پذیرش 2025 نیست؛ پیش از انتشار آینده باید همین preflight و تست مستقل در نسخهٔ مقصد انجام شود.
