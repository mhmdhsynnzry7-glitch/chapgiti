import { ActionSummary } from "./order-attention";
import OrderProgress, {nextOrderAction} from "./order-progress";
import Link from "next/link";
import { formatToman } from "@/lib/money";
import { date, money, statuses, labels, type PrintSpec } from "@/lib/pricing";
import BookSpecificationSummary from "./book-specification-summary";
import type { CustomerOrderSummary, TrackingResult } from "@/lib/api-contracts";
import { Status } from "./ui";
import CopyOrderCode from "./copy-order-code";
import QuoteHistory from "./quote-history";
import CustomerQuoteActions from "./customer-quote-actions";
import { CustomerCorrections } from "./order-corrections";
import { OrderSeen } from "./notification-center";

export function OrderPriceTotals({ order }: { order: CustomerOrderSummary }) {
  return (
    <dl className="order-price-totals">
      <div>
        <dt>قیمت برآوردی</dt>
        <dd>{formatToman(order.estimate)}</dd>
      </div>
      {order.final_price != null && (
        <div className="confirmed-order-price">
          <dt>قیمت نهایی سفارش</dt>
          <dd>{formatToman(order.final_price)}</dd>
        </div>
      )}
    </dl>
  );
}

export default function OrderTrackingDetails({
  result,
}: {
  result: TrackingResult;
}) {
  const { order, events } = result;
  return (
    <div className="customer-order-details">
      {result.specification && (
        <OrderSeen
          orderId={order.id}
          sequence={result.viewSequence}
          available={result.orderViewsAvailable}
        />
      )}
      <div className="panel-title">
        <h2>{order.title}</h2>
        <Status
          value={order.status}
          label={statuses[order.status] || "وضعیت نامشخص"}
        />
      </div>
      {order.attention ? <ActionSummary state={order.attention}/> : <p className="notice">{nextOrderAction(order.status, result.corrections?.at(-1)?.state, result.finalConditions, result.quotes?.at(-1)?.state)}</p>}
      <dl className="order-information">
        <div>
          <dt>کد پیگیری</dt>
          <dd>
            <CopyOrderCode code={order.code} />
          </dd>
        </div>
        <div>
          <dt>تاریخ ثبت</dt>
          <dd>
            <time dateTime={order.created_at}>{date(order.created_at)}</time>
          </dd>
        </div>
        {order.service && (
          <div>
            <dt>خدمت</dt>
            <dd>
              {order.service === "book-printing"
                ? "چاپ کتاب"
                : "خدمات چاپ و نشر"}
            </dd>
          </div>
        )}
      </dl>
      {!result.specification && <Link className="button button-sm" href={"/account/orders/"+order.id}>{order.attention?.required ? order.attention.label : "مشاهده جزئیات سفارش"}</Link>}
      <OrderPriceTotals order={order} />
      <p className="order-estimate-note">
        برآورد اولیه تقریبی است؛ پیشنهاد چاپخانه را در حساب خود بررسی و صریحاً
        تأیید می‌کنید. تأیید پیشنهاد، شروع خودکار چاپ نیست.
      </p>
      {result.quotes && <QuoteHistory quotes={result.quotes} />}
      {result.specification && <CustomerQuoteActions result={result} />}
      {result.specification && <CustomerCorrections result={result} />}
      {result.specification && (
        <section aria-label="مشخصات ثبت‌شده سفارش">
          <h3>مشخصات ثبت‌شده</h3>
      <dl className="order-information">
            {result.specification.pages != null && (
              <div>
                <dt>تعداد صفحات</dt>
                <dd>{money(result.specification.pages)}</dd>
              </div>
            )}
            {result.specification.quantity != null && (
              <div>
                <dt>تیراژ</dt>
                <dd>{money(result.specification.quantity)} جلد</dd>
              </div>
            )}
            {result.specification.size && (
              <div>
                <dt>قطع</dt>
                <dd>
                  {result.specification.catalogSnapshot?.options.find(
                    (option) =>
                      option.kind === "bookSizes" &&
                      option.code === result.specification?.size,
                  )?.label ??
                    labels.size[result.specification.size] ??
                    result.specification.size}
                </dd>
              </div>
            )}
          </dl>
          {result.specification.details && (
            <BookSpecificationSummary
              spec={result.specification as PrintSpec}
            />
          )}
          {result.specification.customerNotes && (
            <p>{result.specification.customerNotes}</p>
          )}
        </section>
      )}
      {result.files && (
        <section id="order-files" aria-label="فایل‌های سفارش">
          <h3>فایل‌های ارسال‌شده</h3>
          {result.files.length ? (
            <ul>
              {result.files.map((file) => (
                <li key={file.id}>
                  <a href={"/api/customer/files/" + file.id}>{file.name}</a>
                  {" · "}
                  <a
                    href={"/api/customer/files/" + file.id + "?preview=true"}
                    target="_blank"
                    rel="noopener noreferrer"
                    aria-label={"نمایش " + file.name + " در برگهٔ جدید"}
                  >
                    نمایش
                  </a>
                  {file.fileVersion && (
                    <span>
                      {" "}
                      · نسخهٔ {file.fileVersion.toLocaleString("fa-IR")}
                    </span>
                  )}
                  {result.approvedFileId === file.id && (
                    <strong> · فایل تأییدشدهٔ چاپ</strong>
                  )}{" "}
                  · <bdi>{money(Math.ceil(file.size / 1024))} KB</bdi>
                </li>
              ))}
            </ul>
          ) : (
            <p>فایلی ارسال نشده است.</p>
          )}
        </section>
      )}
      {order.status === "cancelled" ? (
        <p className="notice order-cancelled" role="status">
          این سفارش رد یا لغو شده است.
        </p>
      ) : (
        <OrderProgress terms={result.finalConditions} binding={result.requiresBinding} status={order.status} accepted={result.quotes?.some(q=>q.state==="accepted")} />
      )}
      {result.specification && (
        <Link prefetch={false} className="text-link" href="/contact">
          ارتباط با چاپخانه
        </Link>
      )}
      <h3 className="order-history-heading">تاریخچه سفارش</h3>
      {events.length ? (
        <ol className="timeline">
          {events.map((event, index) => (
            <li key={`${event.created_at}-${index}`}>
              <strong>{statuses[event.status] || "به‌روزرسانی سفارش"}</strong>
              <p>{event.note}</p>
              <small>{event.actor_role==="customer"?"مشتری":event.actor_role==="admin"?"مدیر چاپخانه":"سامانه"} · <time dateTime={event.created_at}>{new Date(event.created_at).toLocaleString("fa-IR",{timeZone:"Asia/Tehran"})}</time></small>
            </li>
          ))}
        </ol>
      ) : (
        <p className="ticket-empty">هنوز رویدادی برای نمایش وجود ندارد.</p>
      )}
    </div>
  );
}
