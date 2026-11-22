# SignalR برای اعلان‌ها و گفت‌وگوی پشتیبانی چاپ گیتی

> قرارداد جاری ۱۹ نوامبر ۲۰۲۶ چهار endpoint فعال را برای اعلان‌ها و گفت‌وگوهای مشتری و مدیر ثبت می‌کند. POST پیام و تغییر وضعیت همچنان روی REST انجام می‌شوند. IIS فقط hint تغییرات و مذاکرهٔ همان اتصال را مستقیماً به ASP.NET می‌رساند.

دامنهٔ مجاز ۲۰۲۶/۱۱/۱۴ فقط سه بخش است: اعلان مشتری، اعلان مدیر و گفت‌وگوی تیکت. این تغییر هیچ فرمان ثبت سفارش، قیمت‌گذاری، پیشنهاد، ورود، OTP، مدیریت محتوا یا آپلود را به Hub منتقل نمی‌کند. ارسال Push موبایل یا SMS جدیدی وجود ندارد.

## Hubها و مسیرها

| Hub | مسیر مشتری | مسیر مدیر | عملیات |
|---|---|---|---|
| `NotificationsHub` | `/api/realtime/customer/notifications` | `/api/realtime/admin/notifications` | دریافت `Changed`؛ بدون فرمان بیزینسی |
| `SupportHub` | `/api/realtime/customer/support` | `/api/realtime/admin/support` | `JoinTicket(ticketId)`، `LeaveTicket()` و دریافت `Changed` |

مسیرهای استاندارد negotiate و transport را خود SignalR مدیریت می‌کند. تمام ارسال پیام، تغییر وضعیت، ایجاد درخواست و تصمیم پیشنهاد از همان REST Controllerهای قبلی انجام می‌شوند. تغییر قابل‌تحویل فقط `{kind,eventId}` است؛ eventId هش شناسهٔ رویداد است. متن پیام، مبلغ، نام، شماره، ایمیل، PDF، کلید، OTP و token نشست داخل hint نیستند.

## احراز هویت و مالکیت

`RealtimeSessionMiddleware` فقط در پیشوند `/api/realtime` فعال است و همان `SessionService` و cookie دسترسی موجود هر نقش را می‌خواند. scheme محدود `ChapRealtime` صرفاً نتیجهٔ این اعتبارسنجی را به principal استاندارد ASP.NET Core تبدیل می‌کند؛ login، cookie، JWT یا جدول هویت دیگری نمی‌سازد. سیاست HTTPS موجود و Origin دقیق تنظیم‌شده کنترل می‌شوند. WebSocket و درخواست‌های تغییردهندهٔ transport به Origin معتبر نیاز دارند. در هر مسیر فقط cookie همان نقش معتبر است؛ داشتن cookie مشتری مجوز Hub مدیر نیست.

ترتیب middleware صریح است: تطبیق نشست، سپس `UseAuthentication`، سپس `UseAuthorization`. این ترتیب مانع cache شدن NoResult توسط middleware خودکار minimal hosting پیش از تطبیق نشست می‌شود. Hubها فقط روی endpointهای دارای policy ثبت می‌شوند و OnConnected نیز هویت را کنترل می‌کند.

principal و گروه نقش با شناسهٔ داخلیِ server-side ساخته می‌شوند؛ کلید تغییرناپذیر مدیر همان کلید نشست فعلی است. مشتری باید حساب و پروفایل کامل داشته باشد. ورود مشتری و مدیر و تمدید/خروج آن‌ها مستقل باقی‌اند. cookie در JS خوانده نمی‌شود و credential نشست در URL قرار نمی‌گیرد؛ پارامتر id مربوط به transport، شناسهٔ موقت خود پروتکل SignalR است.

پیش از `JoinTicket`، GUID canonical و مالکیت با repository فعلی تیکت کنترل می‌شود. هر اتصال پشتیبانی فقط یک گفت‌وگو را دنبال می‌کند. گروه‌های نقش و `ticket:{id}` فقط کمک مسیریابی‌اند. پیش از **هر تحویل** دوباره cookie ذخیره‌شدهٔ اتصال، نشست فعلی، انقضا/ابطال و مالکیت فعلی تیکت بررسی می‌شوند؛ اتصال فاقد مجوز حذف و بسته می‌شود. فرستادن نهایی به connectionهای مجاز است، نه `Clients.All` یا broadcast اعتمادشده به گروه قدیمی.

اعتبارسنجی‌ها scope کوتاه مستقل دارند؛ اتصال ADO.NET پس از بررسی بسته می‌شود و برای عمر SSE/WebSocket اشغال نمی‌ماند. فقط access cookie در حافظهٔ اتصال باقی می‌ماند؛ refresh credential ذخیره یا log نمی‌شود. لاگ‌های اتصال/خروج، رد مجوز و تحویل/خطا فقط scope، نوع و نام امن خطا دارند. خطای خام یا دادهٔ مشتری log نمی‌شود.

## ترتیب ذخیره‌سازی و تحویل

`CommittedUpdates` در Application قرارداد انتقال را نگه می‌دارد و هیچ وابستگی SignalR/HTTP/SQL ندارد. `SqlStore.Transaction` آن را حول تراکنش واقعی قرار می‌دهد. enqueue داخل تراکنش انجام می‌شود؛ flush فقط بعد از CommitAsync موفق و Dispose تراکنش است. rollback صف را پاک می‌کند و تراکنش تو‌در‌تو زودتر از والد flush نمی‌شود. ارسال شبکه داخل قفل/تراکنش نیست. sink اختیاری CLI فاقد اتصال است؛ API sink واقعی SignalR را ثبت می‌کند.

`NotificationPublisher` همچنان رکورد پایدار را در UoW ذخیره می‌کند. repository با `OUTPUT inserted.id` مشخص می‌کند که درج جدید رخ داده است؛ retry/dedupe پایدار hint تازه نمی‌سازد و به مقدار affected rows یا NOCOUNT تکیه ندارد. ثبت/پاسخ تیکت و تغییر واقعی وضعیت نیز hint را در همان تراکنش صف می‌کنند. خواندن اعلان پس از UPDATE موفق، hint مختص همان گیرنده ایجاد می‌کند؛ فراخوانی در تراکنش بیرونی تا commit آن منتظر می‌ماند.

خرابی انتقال، تراکنش موفق را برنمی‌گرداند و API را به موفقیت کاذب در ذخیره‌سازی تبدیل نمی‌کند. ارسال به هر اتصال timeout دارد. SignalR تضمین durable delivery یا transactional outbox نیست؛ اگر فرایند پس از commit و پیش از hint متوقف شود، داده در SQL هست و initial load، reconnect، focus یا safety polling آن را بازیابی می‌کنند. هیچ جدول، migration یا backfill جدیدی لازم نیست و فایل‌های 001–007 تغییر نمی‌کنند.

## فرانت و fallback

- `NotificationProvider` مشتری و مدیر، badge، dropdown و مرکز اعلان را با اتصال همان نقش به‌روز می‌کند. دریافت hint باعث بازخوانی شمارش و فهرست معتبر می‌شود؛ لیست صفحه‌بندی‌شده از SQL/API می‌آید. read/read-all و deep link منتظر تأیید HTTP می‌مانند.
- `TicketThread` در هر دو نقش پس از اتصال به گفت‌وگوی مجاز می‌پیوندد؛ hint، پیام‌ها و وضعیت را دوباره از API می‌گیرد. ارسال REST و draft موجود حفظ شده‌اند. پاسخ GET قدیمی/لغوشده جای پاسخ جدید را نمی‌گیرد و شناسه‌های تکراری حذف می‌شوند؛ ترتیب سرور حفظ می‌شود.
- package رسمی `@microsoft/signalr` سری 10 به‌صورت lazy فقط هنگام استفاده از این بخش‌های احراز‌شده بارگذاری می‌شود. مذاکرهٔ استاندارد WebSocket، SSE و LongPolling حفظ شده و transport اجباری نیست. هیچ CDN client script یا دامنهٔ cross-origin جدید نیاز نیست.
- reconnect خودکار و retry اولیهٔ محدود ۲/۵/۱۰/۳۰ ثانیه وجود دارد. تمدید نشست از همان helper نقش قبلی استفاده می‌کند. پس از reconnect، تیکت دوباره join و دادهٔ معتبر دوباره دریافت می‌شود.
- hintهای تکراری با eventId در یک حافظهٔ محدود و burstها با تأخیر کوتاه coalesce می‌شوند. polling سریالی از هم‌پوشانی و حلقهٔ فشرده جلوگیری می‌کند. در اتصال فعال، safety reconciliation هر ۱۲۰ ثانیه و در قطع اتصال هر ۲۰ ثانیه است. focus/online فوراً تلاش می‌کنند؛ تب مخفی connection و polling را متوقف می‌کند. تغییر هویت/unmount اتصال و دادهٔ خصوصی قبلی را نگه نمی‌دارد.
- وضعیت ساده و فارسی گفت‌وگو و امکان ارسال HTTP در قطع اتصال حفظ شده است. جزئیات فنی در data attribute تشخیصی و لاگ امن سرور قرار دارند؛ toast یا اعلان فنی انبوه ساخته نشده است.

## Next.js، IIS و CDN

پروکسی فعلی Next.js، پاسخ stream را مستقیم عبور می‌دهد؛ فقط DELETE مختص SignalR برای پایان SSE/LongPolling اضافه شده است. Route Handler عادی، upgrade WebSocket را انجام نمی‌دهد؛ در اجرای صرفاً Next proxy، SDK می‌تواند به SSE/LongPolling برگردد. این مسیر در مرورگر با WebSocket مسدود آزمایش می‌شود.

برای rollout آینده روی همان Windows Server/IIS و با مجوز جداگانه:

1. WebSocket Protocol و پشتیبانی ARR/URL Rewrite از upgrade فعال باشد.
2. قاعدهٔ **همان origin عمومی** برای `/api/realtime/{customer|admin}/{notifications|support}` و `/negotiate` هرکدام پیش از قاعدهٔ عمومی Next قرار گیرد و با حفظ کامل مسیر به API داخلی `127.0.0.1:5100` برود. بقیهٔ سایت همچنان به Next داخلی 3000 می‌رود. مسیرهای موجود/binding دیگر سایت‌ها تغییر داده نشوند.
3. Cookie، Origin، Accept، Content-Type و هدرهای upgrade عبور کنند؛ HTTPS/WSS عمومی و اعتماد پروکسی طبق تنظیم قبلی حفظ شوند. backend روی loopback باقی بماند.
4. SSE buffer یا cache نشود؛ `no-store, no-transform` حفظ و timeout پراکسی با زمان انتظار transport سازگار باشد. CDN نباید روی این مسیر cache یا پاسخ HTML challenge بسازد. پشتیبانی WebSocket CDN فرض نشده است؛ قطع آن باید با SSE/LongPolling و سپس polling عادی قابل‌تحمل باشد.
5. این پیاده‌سازی یک API process است. چند instance به affinity/طرح scale-out مناسب نیاز دارند؛ backplane، broker یا سرویس جدید در این کار اضافه نشده است.

این‌ها نیازمندی مستند آینده‌اند؛ در این کار هیچ تنظیم IIS، Windows Service، CDN، binding، deployment، publish یا release ZIP اجرا/ایجاد نمی‌شود. متغیر محیطی یا secret جدید لازم نیست؛ `SITE_URL` و مقصد loopback API قبلی استفاده می‌شوند.

## آزمون‌ها

`RealtimeScenarios` و `RealtimeSocket` از Kestrel و پروتکل واقعی WebSocket روی TestApiHost حافظه‌ای استفاده می‌کنند. `tests/realtime.mjs` lifecycle، ID dedup، retry، hidden tab و polling را بدون شبکه می‌سنجد. `tests/modernization-ui.mjs --realtime` هم‌زمانی دو نقش، fallback transport، اعلان/پیام دوطرفه، read، refresh، reconnect، قطع کامل realtime و حفظ HTTP polling را بررسی می‌کند. تست‌های عمومی قبلی نیز اجرا شدند. نتیجهٔ دقیق و محدودیت‌های اجرای حاضر در گزارش QA و خلاصهٔ ماشین‌خوان ثبت شده است؛ تست نوشتنی روی دیتابیس عملیاتی یا SMS واقعی مجاز نیست.

منابع رسمی: [هویت و revalidation در SignalR](https://learn.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz?view=aspnetcore-10.0) و [کلاینت JavaScript و reconnect](https://learn.microsoft.com/en-us/aspnet/core/signalr/javascript-client?view=aspnetcore-10.0).
