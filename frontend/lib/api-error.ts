export class ApiError extends Error {
  constructor(public status: number, public code: string, message: string, public requestId?: string) {super(message);}
}
const statusMessages: Record<number,string> = {
  400:"مقدار فیلدها را بررسی کنید.",401:"نشست شما پایان یافته است. دوباره وارد شوید.",403:"دسترسی به این بخش مجاز نیست.",404:"اطلاعات درخواستی پیدا نشد.",
  409:"اطلاعات تغییر کرده است. نسخهٔ تازه را بررسی کنید.",413:"حجم فایل بیش از حد مجاز است.",415:"نوع فایل مجاز نیست.",
  429:"تعداد درخواست‌ها زیاد است. کمی بعد دوباره تلاش کنید.",503:"سرویس موقتاً در دسترس نیست."
};
export async function responseData(response: Response) {
  const data = await response.json().catch(() => null);
  if (response.ok && data !== null) return data;
  const message = typeof data?.error === "string" && /[\u0600-\u06ff]/.test(data.error) ? data.error : statusMessages[response.status];
  throw new ApiError(response.status, data?.code || "", message || "در پردازش درخواست مشکلی پیش آمد. دوباره تلاش کنید.", data?.requestId);
}
