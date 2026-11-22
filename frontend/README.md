# فرانت چاپ گیتی

پروژهٔ مستقل React 19 / Next.js 16 با رابط فارسی و رندر سمت سرور.

```powershell
cd C:\src\chapgiti\frontend
npm.cmd ci
Copy-Item .env.example .env.local
npm.cmd run dev
```

اگر فایل .env.local از قبل وجود دارد، همان را ویرایش کنید. مقدار DOTNET_API_URL باید آدرس داخلی بک‌اند، مانند http://127.0.0.1:5100 باشد. SITE_URL آدرس مرورگر با پورت آن است.

بک‌اند را مطابق backend/README.md در ترمینال دیگری اجرا کنید. فرانت به SDK دات‌نت یا دسترسی مستقیم به دیتابیس نیاز ندارد. رمز دیتابیس و اطلاعات پیامک را در فایل تنظیمات فرانت ننویسید.

```powershell
npm.cmd run build
npm.cmd run start
```

دستورهای lint و typecheck نیز داخل همین پوشه اجرا می‌شوند. مسیر app/api صرفاً پروکسی سمت سرور به API دات‌نت است. احراز هویت، مجوزها، محاسبهٔ نهایی مبلغ و ذخیره‌سازی در بک‌اند انجام می‌شوند.

## پابلیش مستقل

از همین پوشه npm.cmd run publish را اجرا کنید. خروجی کامل در frontend/publish ساخته می‌شود و شامل server.js، وابستگی‌های اجرا، public و .next/static است. فایل‌های .env وارد خروجی نمی‌شوند. راهنمای نصب: ../deploy/iis/README-DOTNET.md.
