# اجرای توسعه روی دیتابیس اصلی — ۲۰۲۶-۱۱-۱۸

در ریشهٔ مخزن، `npm.cmd run dev` با حساب‌های موجود در SQL Server محلی `ChapGiti` اجرا می‌شود. گزارش فعال‌سازی و راه‌اندازی

# Historical EF/catalog/magazine source update — 2026-11-17

EF Core 10.0.15 is primary persistence. SQL001–009 remain immutable; explicit maintenance validates the legacy schema and then applies the separate EF baseline and CatalogAndMagazine migration. No automatic startup migration or database provisioning. The working database has NOT been activated by this task. See [manual adoption and residual SQL inventory](../docs/EF-CORE-ADOPTION-FA.md) and verified implementation report. Public/admin magazine is active in source; portfolio, ticket writes and article-footer ordering CTAs stay disabled.

# بک‌اند چاپ گیتی

در نسخهٔ جاری ۲۰۲۶/۱۱/۱۶، فقط `NotificationsHub` در دو مسیر مستقل `/api/realtime/{customer|admin}/notifications` فعال است. تیکت و لینک اعلان تاریخی فقط آرشیو مجاز دارند؛ ایجاد/پاسخ/تغییر وضعیت و اتصال `support` غیرفعال‌اند. از همان نشست موجود، صف پس از commit و hint بدون اطلاعات شخصی استفاده می‌شود. هیچ فرمان بیزینسی از Hub اجرا نمی‌شود. [راهنمای فنی و IIS](../docs/SIGNALR-REALTIME-FA.md) و [گردش جاری اصلاحیه و مشاهدهٔ سفارش](../docs/ORDER-CORRECTIONS-AND-ATTENTION-FA.md).

نسخهٔ Request-to-Quote-to-Production: برآورد ناشناس، درخواست احراز‌شده، پیشنهاد نسخه‌دار مدیر، تأیید صریح مالک به صف چاپ و سپس شروع مستقل تولید. `OrderQuoteService`، Domain `QuoteWorkflow` و `IOrderQuoteRepository` مرزهای فعلی را حفظ می‌کنند. مبالغ تومان و قیمت‌ها/رویدادهای تاریخی بازنویسی نمی‌شوند.

`006_order_quotes.sql` و `007_in_app_notifications.sql` با مجوز صریح مالک در ۲۰۲۶/۱۱/۱۴ روی ChapGiti محلی Development، با اتصال Shared Memory به instance پیش‌فرض `.` اعمال شدند. این وضعیت برای سرور دیگری فرض نمی‌شود؛ در محیط فاقد 006 خواندن تاریخی کار می‌کند و ساخت درخواست/عملیات quote با 503 رد می‌شود. هیچ migration خودکاری در startup نیست. گزارش اجرای محلی و قراردادهای پیاده‌سازی.

مرکز اعلان پایدار در همین Development محلی schema لازم را دارد. `NotificationPublisher` رویداد Domain را داخل همان UoW ذخیره می‌کند؛ `NotificationService` و APIهای مستقل `/api/customer/notifications` و `/api/admin/notifications` فهرست، `unread-count`، `{id}/read` و `read-all` را ارائه می‌کنند. در محیط فاقد 007 خطای صریح `NOTIFICATIONS_UNAVAILABLE` می‌آید. کلید گیرندهٔ مدیر و FK پیشنهاد، شناسهٔ ثابت تاریخی است؛ ایمیل ویرایشی پروفایل آن را عوض نمی‌کند. SMS و Push کانال این پیاده‌سازی نیستند. بازیابی محدود SQL 1205 فقط برای عملیات مستقل و idempotent اعلان است؛ تراکنش بیزینسی یا درج اعلان خودکار replay نمی‌شود.

آزمون متمرکز فرانت: `node tests/notifications.mjs` و پس از build امن، `node tests/modernization-ui.mjs --notifications`. fixture SQL مستقل اکنون زنجیرهٔ 001 تا 007، rollback اعلان/بیزینس و پایداری را بررسی می‌کند؛ همان flag صریح قبلی و guard هویت الزامی است. اجرای آن مجوز مهاجرت عملیاتی نیست.

آزمون‌های امن این جریان: `npm.cmd run test:auth -- --configuration Verification`، `node tests/request-draft.mjs` و `node tests/modernization-ui.mjs --request-workflow` پس از build با `CHAP_GITI_VERIFY_BUILD=true`. fixture مستقل `tests/sql005` اکنون 006 را هم فقط در LocalDB خودش بررسی می‌کند. این دستورها به publish یا نوشتن در SQL اصلی نیاز ندارند.

بک‌اند مستقل ASP.NET Core 10 و SQL Server است. برای build و اجرای این پروژه نیازی به Node.js یا پوشهٔ frontend نیست.

- ChapGiti.Domain: قوانین چاپ، قیمت‌گذاری و گردش وضعیت سفارش و تیکت.
- ChapGiti.Application: عملیات حساب، سفارش، فایل، تیکت و مدیریت؛ قراردادهای Repository و سرویس‌های خارجی.
- ChapGiti.Infrastructure: پیاده‌سازی SQL Server با EF Core/Fluent mappings، اتصال و تراکنش مشترک، SQL محدود و مستند، فایل خصوصی، هش رمز و ملی‌پیامک.
- ChapGiti.Api: نقطهٔ ترکیب وابستگی‌ها، مسیرهای HTTP، کوکی‌ها و نگاشت خطا و خروجی.

مسیرهای HTTP در Controllers گروه‌بندی شده‌اند و ApiDispatcher حذف شده است. نشست دسترسی/تمدید مشتری و مدیر مستقل است؛ مشتری با موبایل/رمز و مدیر با نام ثابت admin و رمز وارد می‌شود. کوکی، namespace هش، خروج، بازیابی و تمدید هر حوزه مستقل‌اند. جزئیات امنیت و سازگاری: ../docs/CUSTOMER-AUTH-OTP-FA.md.

وابستگی‌ها: Application به Domain؛ Infrastructure به Application و Domain؛ API به Application و Infrastructure. لایه‌های داخلی به HTTP، SQL Server و تنظیمات میزبان ارجاع ندارند.

## اجرای مستقل در PowerShell

```powershell
cd C:\src\chapgiti\backend
dotnet build .\ChapGiti.slnx

if (-not $env:SQLSERVER_CONNECTION_STRING) { throw 'Load the private SQL setting for this installation first.' }
$env:ConnectionStrings__SqlServer = $env:SQLSERVER_CONNECTION_STRING
$env:Storage__Root='C:\src\chapgiti\data'
$env:Site__Url='http://127.0.0.1:3000'
$env:ASPNETCORE_URLS='http://127.0.0.1:5100'
$env:ASPNETCORE_ENVIRONMENT='Development'

dotnet run --project .\ChapGiti.Api
```

این نمونه برای SQL Server محلی با حساب ویندوز است. روی سرور، connection string و مسیر خصوصی ذخیرهٔ فایل را مطابق محیط خود تنظیم کنید. دیتابیس ChapGiti باید از قبل وجود داشته باشد. ساخت خودکار دیتابیس غیرفعال است. حساب migration فقط اجازهٔ تغییر ساختار داخل همان دیتابیس را می‌خواهد؛ حساب سرویس در اجرا فقط مجوز خواندن و نوشتن دیتابیس برنامه را نیاز دارد. فایل داده‌ها از مسیر عمومی سایت جدا می‌ماند.

بررسی سلامت: http://127.0.0.1:5100/health

مقدار Site__Url باید دقیقاً با origin فرانت، شامل پورت، یکسان باشد. مرورگر از طریق Next.js به API متصل می‌شود و به CORS عمومی نیاز نیست. بک‌اند را روی loopback نگه دارید.

## انتشار مستقل

```powershell
dotnet publish .\ChapGiti.Api -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=10.0.13 -o .\publish
```

فایل‌های SQL و پیش‌فرض‌ها هنگام build و publish به Data در خروجی API کپی می‌شوند. این فایل‌های نسخه‌بندی‌شده مستقل از بیلد فرانت هستند. تعرفه‌های ذخیره‌شدهٔ SQL Server بر پیش‌فرض‌ها اولویت دارند. migration اعمال‌شده را ویرایش نکنید؛ پس از مرز SQL009، تغییر ساختار را در migration و snapshot جدید EF قرار دهید؛ راهنمای پذیرش EF و قفل مرز legacy را رعایت کنید.

آزمون وابستگی‌ها و قوانین: از ریشهٔ مخزن npm.cmd run test:architecture، یا داخل backend دستور dotnet run --project .\tests\ChapGiti.ArchitectureTests.

مستندات جزئی‌تر: ../docs/ARCHITECTURE.md و ../deploy/iis/README-DOTNET.md.


## سیاست یک دیتابیس

استثنای صریح فقط برای پذیرش SQL با مجوز جداگانه: ابزار `tests/sql005` یک نمونهٔ LocalDB مستقل و دیتابیس موقتِ ساختگی می‌سازد، هویت مقصد را کنترل و فقط fixture خودش را حذف می‌کند. این ابزار ورودی connection string عملیاتی نمی‌پذیرد و در اسکریپت‌های معمول اجرا نمی‌شود. برنامه همچنان تنها از دیتابیس عملیاتی موجود استفاده می‌کند. راهنمای آزمون: `../tests/sql005/README.md`؛ نتایج: `../docs/SQL005-ACCEPTANCE-2026-10-30.md`.

تنها دیتابیس موجود ChapGiti استفاده می‌شود. اجرای برنامه، migration، نصب IIS و آزمون‌ها دیتابیس جدیدی نمی‌سازند. فرمان --migrate فقط جدول‌ها و ساختار نسخه‌بندی‌شده را در دیتابیس موجود آماده می‌کند؛ اگر دیتابیس وجود نداشته باشد، با خطا متوقف می‌شود.

تست‌های دیتابیس با npm.cmd run test:db فقط اتصال، سلامت و مسیرهای GET را بررسی می‌کنند. حساب، سفارش، تیکت و تعرفهٔ آزمایشی درج نمی‌شود. آزمون‌های تغییردهندهٔ داده غیرفعال‌اند و تست معماری همچنان بدون اتصال به SQL Server اجرا می‌شود.

## پیش‌نیاز نسخهٔ نوسازی

migration شمارهٔ 003 در محیط محلی، روی دیتابیس موجود ChapGiti، در تاریخ 2026-10-27 با اجازهٔ صریح مالک و پس از بکاپ COPY_ONLY و تأیید صحت آن اعمال شد. چهار جدول جدید و ستون هدف OTP بررسی شدند و تعداد رکوردهای جداول قبلی حفظ شد. این وضعیت به معنی اعمال migration در سرورهای دیگر نیست؛ آن محیط‌ها همچنان به بازبینی، بکاپ و مجوز جداگانه نیاز دارند. اجرای معمول برنامه یا npm run dev فقط سلامت را بررسی می‌کند و migration خودکار ندارد.

ورود محیط واقعی به HTTPS نیاز دارد. HTTP تنها روی loopback در Development برای بررسی محلی پذیرفته می‌شود. هیچ تنظیم سرور در این تغییر انجام نشده است.

بررسی محلیِ بدون SQL/پیامک واقعی: npm.cmd run test:auth و npm.cmd run test:ui از ریشه. میزبان حافظه‌ای آزمون، ترکیب واقعی Controllerها، نشست و اعتبارسنجی برنامه را استفاده می‌کند. نوشتن داده روی SQL عملیاتی در تست‌ها ممنوع است.

## OTP برای Development محلی

بازیابی فقط برای حساب کامل با شمارهٔ تأییدشده شروع می‌شود؛ شمارهٔ ناموجود، ثبت‌نام موقت، حساب ناقص و شمارهٔ قدیمی تأییدنشده پیش از SMS و ایجاد چالش رد می‌شوند. کدهای ساختاریافتهٔ نبود حساب، کد اشتباه، انقضا، چالش ناموجود، مصرف قبلی، سقف تلاش و خرابی ارسال در CustomerOtpErrors تعریف شده‌اند. برای آزمون هنگام اجرای API محلی، `npm.cmd run test:auth -- --configuration Verification` خروجی جداگانه می‌سازد؛ تست مرورگر نیز همین configuration را استفاده می‌کند و برنامهٔ Debug متوقف نمی‌شود.

تنظیم فعلیِ محلی OTP_VERIFICATION_ENABLED=true، OTP_ENABLED=true و SMS_ENABLED=true است؛ کد چهاررقمی و اعتبار ۱۲۰ ثانیه حفظ شده‌اند. appsettings.Development.json نیز تأیید واقعی و ارسال را فعال می‌کند. کلید خصوصی کنسول ملی‌پیامک باید در MELIPAYAMAK_CONSOLE_API_KEY یا Otp__ApiKey موجود باشد؛ نبود/خرابی ارائه‌دهنده خطا می‌دهد و کد دلخواه قبول نمی‌شود. پیامک سفارش تنظیمات مستقلِ نام کاربری/رمز و خط یا قالب را می‌خواهد. هیچ credential در سورس قرار ندارد. راهنمای کامل: ../docs/CUSTOMER-AUTH-OTP-FA.md.

false فقط برای آزمون عمدی در Development با Site:Url از نوع loopback مجاز است؛ Production/Staging یا آدرس عمومی با این مقدار شروع نمی‌شوند. با false پس از دریافت چالش هر کد دقیقاً چهاررقمی پذیرفته و همهٔ SMSها متوقف می‌شوند؛ این حالت پیش‌فرض نیست. هیچ رکورد یا migration در زمان تغییر تنظیم بازنویسی نمی‌شود.

بررسی حالت محلی بدون SQL واقعی: npm.cmd run test:ui:otp-bypass. test:auth هر دو حالت را پوشش می‌دهد. رابط فرانت تغییر نکرده است.

## ثبت‌نام با تأیید شماره

ثبت‌نام مشتری اکنون از `POST /api/customer/otp/request` با phone و purpose=register شروع می‌شود؛ پیش از OTP نام یا رمز دریافت نمی‌شود. `POST /api/customer/otp/verify` مجوز پنج‌دقیقه‌ای registrationToken و شمارهٔ یکسان‌سازی‌شده را می‌دهد. `POST /api/customer/registration/complete` فقط registrationToken، firstName، lastName، password و confirmation می‌گیرد. مجوز و ساخت حساب اتمیک‌اند؛ شمارهٔ مقصد از مجوز خوانده می‌شود. پاسخ OTP حساب یا نشست نمی‌سازد. ذخیره‌سازی با ردیف‌های grant در جدول چالش موجود انجام می‌شود و migration تازه‌ای لازم نیست؛ داده‌های قدیمی دست‌نخورده می‌مانند.

grant وضعیت منطقی است و نباید در delivery_status نوشته شود؛ CK_OtpDelivery آن را رد می‌کند. ثبت فیزیکی مجوز از register/pending/attempts=5/code_hash=NULL استفاده می‌کند و فقط Repository آن را به grant برمی‌گرداند. هش توکن کامل حفظ می‌شود. تست‌ها INSERT واقعی Repository را با قیود schema در حافظه ارزیابی می‌کنند و خطای قدیمی SQL 547/HTTP 500 را بدون SMS یا نوشتن SQL واقعی بازتولید می‌کنند.

## Services CMS V2

Services are stored in the existing `Settings.services` JSON value. No migration is required. Reads use the preserved six-service fallback until an administrator explicitly saves content. Application startup does not initialize or overwrite the operational key.

- Public: `GET /api/public/services`, `GET /api/public/services/{slug}`, and published services in `GET /api/public/bootstrap`.
- Independent administrator session required: `GET/POST /api/admin/services`, `PUT /api/admin/services/{id}`, `POST /api/admin/services/reorder`.
- Create/update payload: slug, title, subtitle, description, fullDescription, image, icon, color, features, sections, seoTitle, seoDescription, displayOrder, active; updates also require the last returned version. IDs and aliases are server-managed. Creation starts inactive in the UI.
- Reorder payload: `{ "items": [{ "id": "...", "version": 1 }] }`, including the complete current catalog. Conflicts return 409. Transactions lock the Settings key, preserve other settings and record audit entries.
- Image references must identify existing uploaded banner assets. Empty image uses the existing illustration. Public endpoints filter inactive content; aliases resolve canonical records, while Next performs canonical redirects.
- Existing banner writes accept both CTA label/link empty or both valid. Site settings support optional platform-validated HTTPS `instagram`, `telegram`, `bale` URLs; omitted fields preserve existing values.
- Magazine uses the existing Articles/assets with versioned publication and canonical aliases. Published-only /api/public/articles, public SSR /blog and /admin/articles are active in source. Portfolio alone stays retired/redirected; legacy bootstrap arrays remain empty, with no automatic fake-article seeding.

Verification: `npm.cmd run test:auth -- --configuration Verification`; browser CMS checks: `node tests/modernization-ui.mjs --cms-phase=7` after a safe frontend build. Real SQL persistence is tested only by the explicitly opted-in guarded disposable `tests/sql005` fixture. See `../docs/CHAP-CMS-MODERNIZATION-REPORT.md` for evidence and restrictions. No publish command is part of these checks.

## ورود مستقل و پروفایل مدیر

مسیر مدیر `/admin/login` و API آن `/api/auth/login` با ورودی username=admin و password است؛ ایمیل فقط مشخصات پروفایل است و قابل استفاده برای ورود نیست؛ `/api/customer/refresh` فقط مشتری و `/api/auth/refresh` فقط مدیر را تمدید می‌کند. سیاست رمز جدید/تغییریافته ۶ تا ۱۲۸ نویسه با A–Z و نشانهٔ ASCII است؛ رمزهای تاریخی هنگام ورود همچنان قابل استفاده‌اند.

migrationهای افزایشی 004 و 005 در تاریخ 2026-10-30 با مجوز صریح مالک روی دیتابیس تأییدشدهٔ Development محلی اعمال شدند؛ مالک برای همین عملیات شرط بکاپ/restore را کنار گذاشت. تغییر ایمیل خود مدیر، تیکت مرتبط با سفارش و تاریخچهٔ یادداشت خصوصی در این دیتابیس در دسترس‌اند. وضعیت محیط دیگری تأیید نشده و شروع معمول برنامه همچنان migration اجرا نمی‌کند. فایل‌های اعمال‌شده را تغییر ندهید. گزارش اجرای محلی: ../docs/LOCAL-DEVELOPMENT-MIGRATIONS-004-005-2026-10-30.md. گزارش تاریخی طراحی 004: ../docs/ADMIN-AUTH-PASSWORD-REPORT-FA.md.
# Current correction workflow and support archive

The 2026-11-16 source update adds `OrderCorrectionService`, `OrderProductionReadiness`, private PDF revisions and independent `OrderViewService` tracking. Ticket intake/replies/status changes now return authenticated 410; historical reads and notification deep links remain authorized. Pending migration source files are 008 then 009 and must never run automatically. Detailed contracts and safe tests: [order corrections and attention](../docs/ORDER-CORRECTIONS-AND-ATTENTION-FA.md). This source task did not access SQL Server or apply either migration.

