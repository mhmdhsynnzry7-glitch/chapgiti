# چاپ گیتی

چاپ گیتی سامانهٔ فارسی ثبت و پیگیری سفارش چاپ کتاب است. مشتری با موبایل و رمز وارد می‌شود، هزینهٔ تقریبی چاپ را بر اساس قطع و مشخصات کتاب می‌بیند، PDF خصوصی می‌فرستد و پیشنهاد قیمت و شرایط تحویل را در حساب خود بررسی می‌کند. مدیر چاپخانه کاتالوگ، تعرفه‌ها، نسخه‌های فایل، اصلاحیه‌ها، مراحل سفارش و محتوای سایت را مدیریت می‌کند. تیکت‌ها و اعلان‌های داخل برنامه از SignalR و بازیابی وضعیت با HTTP استفاده می‌کنند.

## معماری و نسخه‌ها

| بخش | پیاده‌سازی موجود در این مخزن |
|---|---|
| فرانت‌اند | Next.js 16.4.6 با App Router، React 19.2.6، TypeScript 5.9.3 و Tailwind CSS 4.2.1. صفحات عمومی رندر سمت سرور و رابط فارسی راست‌به‌چپ دارند. |
| بک‌اند | ASP.NET Core روی .NET 10 با کنترلرهای HTTP و Hubهای مجاز SignalR. نشست‌های مشتری و مدیر مستقل‌اند. |
| منطق و داده | Domain برای قواعد چاپ، قیمت و وضعیت سفارش، Application برای موارد کاربرد و قراردادها و Infrastructure برای EF Core SQL Server 10.0.15، SQL پارامتری، فایل و پیامک. |
| SQL Server | پروژه با SQL Server کار می‌کند. راهنمای پذیرش سرور، SQL Server 2025 Express و سطح سازگاری ۱۷۰ را پوشش می‌دهد. تنظیم provider در سورس نیز باید هنگام تفسیر قابلیت‌های SQL در نظر گرفته شود. |
| محیط توسعه | Windows و PowerShell، SDK دات‌نت ۱۰ و Node.js نسخهٔ ۲۴ از 24.15.0 تا پیش از ۲۵ همراه npm. نسخهٔ کتابخانه‌ها در lockfile و ابزار EF در manifest ثبت شده است. |

## شروع توسعه از یک Clone تازه

در PowerShell از پوشهٔ والد دلخواه اجرا کنید.

```powershell
git clone https://github.com/mhmdhsynnzry7-glitch/chapgiti.git
Set-Location chapgiti
npm.cmd ci
npm.cmd --prefix frontend ci
dotnet tool restore
dotnet restore backend/ChapGiti.slnx
Copy-Item -LiteralPath .env.example -Destination .env.local
```

دستور کپی برای Clone تازه است. در یک checkout موجود، تنظیمات خصوصی همان توسعه‌دهنده را نگه دارید. راه‌انداز ریشه فایل `.env.local` را می‌خواند و تنظیمات موردنیاز را به دو فرایند می‌دهد.

| تنظیم | کاربرد |
|---|---|
| `SITE_URL`، `PORT`، `DOTNET_API_URL` | در توسعهٔ معمول رابط کاربری روی `http://127.0.0.1:3000` و API روی `http://127.0.0.1:5100` است. |
| `SQLSERVER_CONNECTION_STRING` | اتصال خصوصی به بانک موجود. راه‌انداز توسعهٔ اصلی برای instance محلی پیش‌فرض و بانک `ChapGiti` کنترل مقصد دارد. نمونهٔ محلی از هویت ویندوز استفاده می‌کند. |
| `DATA_DIR` | مسیر خصوصی و قابل‌نوشتن برای فایل‌های برنامه. پوشهٔ داده را بیرون از `frontend/public` قرار دهید. |
| `TRUST_IIS_PROXY`، `ALLOW_HTTP`، `INDEXING_ENABLED` | رفتار پراکسی، HTTP و نمایه‌سازی سایت. الگوی توسعه برای loopback تنظیم شده است. |
| `SMS_ENABLED`، `OTP_ENABLED`، `OTP_VERIFICATION_ENABLED`، `OTP_CODE_LENGTH`، `OTP_LIFETIME_SECONDS` | در نمونه، ارسال غیرفعال و اعتبارسنجی واقعی انتخاب شده است. ثبت‌نام و بازیابی با پیامک واقعی به تنظیم خصوصی سرویس نیاز دارند. کد چهاررقمی و اعتبار آن ۱۲۰ ثانیه است. |
| `MELIPAYAMAK_CONSOLE_API_KEY` | کلید خصوصی API کنسول ملی‌پیامک برای OTP تولیدشده توسط ارائه‌دهنده. |
| `MELIPAYAMAK_USERNAME`، `MELIPAYAMAK_PASSWORD`، `MELIPAYAMAK_FROM`، `MELIPAYAMAK_BODY_ID` | تنظیمات خصوصی مسیر پیامک قالبی، در صورت استفاده. |

پس از آماده‌شدن دیتابیس، تاریخچه و حساب مدیر، از ریشه اجرا کنید.

```powershell
npm.cmd run dev
```

سایت مشتری روی `http://127.0.0.1:3000`، ورود مستقل مدیر در `/admin/login` و وضعیت API روی `http://127.0.0.1:5100/health` است. مدیر با نام ثابت `admin` و رمز خصوصی همان نصب وارد می‌شود. برای وضعیت یا توقف فقط فرایندهای همین پروژه، از `npm.cmd run dev:status` یا `npm.cmd run dev:stop` استفاده کنید.

## دیتابیس و قیمت‌گذاری

بانک SQL Server را DBA از پیش فراهم می‌کند. اجرای عادی برنامه تاریخچه را بررسی می‌کند. `MIGRATION_REQUIRED` به معنی نیاز به آماده‌سازی جداگانهٔ همان مقصد است. برای بانک موجود، پیش از هر ارتقا نام ماشین، instance، نام بانک، backup قابل بازیابی، ترتیب و checksumها را کنترل کنید. اسکریپت‌های نسخه‌خوردهٔ قبلی بخشی از قرارداد دیتابیس هستند.

زنجیرهٔ قدیمی SQL از `001` تا `009` است. ترتیب EF موجود در این snapshot عبارت است از `20261117090230_Legacy009Baseline`، `20261117092337_CatalogAndMagazine`، `20261118113729_TicketMessageReadState` و `20261122130922_CutTariffVersions`. اجرای migration و ایجاد حساب مدیر عملیات مستقل نگهداری‌اند. در یک نصب جدید، مدیر باید برای هر قطع ۱۱ نرخ چاپ را صریحاً تنظیم کند. ۱۵ خدمت تکمیلی نرخ مستقل دارند و سفارش ثبت‌شده نسخهٔ نرخ مؤثر را نگه می‌دارد. [راهنمای دیتابیس](docs/DATABASE-MIGRATION-FA.md) · [سند محصول](docs/PRODUCT-ARCHITECTURE-FA.md)

## ساخت و آزمون محلی

دستورهای زیر برای بررسی سورس هستند. ساخت فرانتِ تأییدی با `CHAP_GITI_VERIFY_BUILD` خروجی محلی Next را برای تست آماده می‌کند. دستور کلی `npm run build` در این پروژه وارد مسیر Publish می‌شود.

```powershell
dotnet build backend/ChapGiti.slnx
dotnet run --project backend/tests/ChapGiti.AuthenticationTests --configuration Verification
npm.cmd run test:architecture
npm.cmd run lint
$env:CHAP_GITI_VERIFY_BUILD = 'true'
$env:SITE_URL = 'http://127.0.0.1:3000'
$env:DOTNET_API_URL = 'http://127.0.0.1:1'
npm.cmd run build:frontend
npm.cmd run typecheck
node tests/order-workflow-schema.mjs
node tests/finishing-pricing.mjs
node tests/modernization-ui.mjs --workflow-modernization
```

آزمون مرورگر از Chrome یا Chromium محلی استفاده می‌کند. در صورت نیاز `CHROME_PATH` را روی مسیر فایل اجرایی تنظیم کنید. میزبان API آزمون از دادهٔ حافظه‌ای و ارسال‌کنندهٔ شبیه‌سازی‌شده استفاده می‌کند. آزمون‌های نوشتنی SQL فقط در زیرساخت ایزوله و با مجوز همان آزمون اجرا می‌شوند. بعد از بررسی، متغیرهای موقت Build را پاک کنید یا برای `npm run dev` یک پنجرهٔ تازهٔ PowerShell باز کنید. [پذیرش دستی](docs/MANUAL-UAT-FA.md)

## ساختار سورس

```text
frontend/                        صفحات، اجزای React، قراردادهای TypeScript و دارایی‌های عمومی
backend/ChapGiti.Api/            کنترلرها، هویت HTTP و SignalR
backend/ChapGiti.Application/    موارد کاربرد و قراردادهای مخزن و پیام‌رسانی
backend/ChapGiti.Domain/         قواعد قیمت‌گذاری و گردش سفارش
backend/ChapGiti.Infrastructure/ EF Core، migrationها، SQL، فایل و سرویس پیامک
backend/tests/                   آزمون‌های .NET، API و معماری
tests/                           آزمون‌های مرورگر، فرانت‌اند و پذیرش ایزوله
scripts/                         راه‌اندازی توسعه، ساخت، آزمون و نگهداری
deploy/iis/ و deploy/sql/        الگوها و ابزارهای Windows Server و کنترل مقصد
docs/                            معماری، راهنماهای فنی و پذیرش محصول
tools/legacy-sqlite/             ابزار سازگاری با قالب دادهٔ تاریخی
.agents/skills/                  دستورالعمل‌های تخصصی پروژه
```

## معماری استقرار و داده‌های خصوصی

در استقرار ویندوز، IIS با URL Rewrite و ARR درخواست‌ها را به فرایندهای محلی هدایت می‌کند. سرویس `GitiNode` فرانت Next و API را اجرا می‌کند. SignalR به پشتیبانی WebSocket در مسیر پراکسی نیاز دارد و مسیر fallback نیز در برنامه وجود دارد. تنظیم واقعی سایت، گواهی‌ها، دسترسی SQL و حساب سرویس متعلق به هر سرور است. تکرار دستور Clone یا اجرای آزمون محلی، استقرار انجام نمی‌دهد. [راهنمای Windows Server](docs/WINDOWS-SERVER-INSTALL-FA.md) · [SignalR](docs/SIGNALR-REALTIME-FA.md)

رمز مدیر، کلید پیامک، فایل محیطی واقعی، کوکی‌ها، PDFهای مشتری، فایل خصوصی و بکاپ دیتابیس در پیکربندی خصوصی همان نصب نگهداری می‌شوند. مخزن عمومی شامل الگوها و داده‌های مصنوعی آزمون است. [قواعد توسعه و حریم خصوصی](AGENTS.md)
