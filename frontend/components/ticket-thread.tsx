"use client";
import { responseData } from "@/lib/api-error";
import { sessionFetch } from "@/lib/session-client";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  useEffect,
  useCallback,
  useRef,
  useState,
  type FormEvent,
} from "react";
import { Send, MessageSquare } from "lucide-react";
import { createRequestId } from "@/lib/request-id";
import { customerApi } from "@/lib/customer-client";
import type { TicketThreadResult } from "@/lib/api-contracts";
import { ticketStatuses, type TicketMessage } from "@/lib/tickets";
import { connectRealtime } from "@/lib/realtime-client";
import { uniqueById } from "@/lib/realtime-connection";
import TicketAttachments from "./ticket-attachments";
import { defaultTicketLimits, ticketTime } from "@/lib/tickets";

export default function TicketThread({
  initial,
  admin = false,
}: {
  initial: TicketThreadResult;
  admin?: boolean;
}) {
  const [data, setData] = useState(initial),
    [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false),
    [error, setError] = useState("");
  const [requestKey, setRequestKey] = useState("");
  const [files, setFiles] = useState<File[]>([]);
  const limits = data.attachmentLimits ?? defaultTicketLimits;
  const sending = useRef(false);
  const router = useRouter();
  const path = (admin ? "admin/tickets/" : "tickets/") + data.ticket.id;
  const request = useCallback(
    async (suffix: string, options: RequestInit = {}) => {
      if (!admin) return customerApi(path + suffix, options);
      const response = await sessionFetch("/api/" + path + suffix, {
        ...options,
        headers: options.body instanceof FormData ? {} : {"Content-Type":"application/json"},
      });
      if (response.status === 401)
        router.replace("/admin/login?next=/admin/tickets");
      return responseData(response);
    },
    [admin, path, router],
  );
  const sequence = useRef(0);
  const refresh = useCallback(async () => {
    const version = ++sequence.current;
    const next = await request("");
    if (version !== sequence.current) return;
    setData(next);
    const incoming = (next.messages as TicketMessage[]).filter(
      (m) => m.actor === (admin ? "customer" : "admin") && !m.read_at,
    );
    const latest = incoming.at(-1);
    if (!next.readOnly && latest && document.visibilityState === "visible") {
      await request("/read", {
        method: "POST",
        body: JSON.stringify({ throughId: latest.id }),
      });
      window.dispatchEvent(new Event("chap:notifications-refresh"));
    }
  }, [request, admin]);
  useEffect(() => {
    let live = false,
      disposed = false;
    const order = sequence;
    const sync = () => {
      if (!disposed && document.visibilityState === "visible")
        void refresh().catch(() => {
          if (!disposed) setError("بازیابی گفت‌وگو انجام نشد.");
        });
    };
    const stop = connectRealtime(
      admin ? "admin" : "customer",
      "ticket",
      sync,
      (state) => {
        live = state === "connected";
      },
      initial.ticket.id,
    );
    const timer = window.setInterval(() => {
      if (!live) sync();
    }, 12000);
    sync();
    window.addEventListener("focus", sync);
    document.addEventListener("visibilitychange", sync);
    return () => {
      disposed = true;
      order.current++;
      stop();
      clearInterval(timer);
      window.removeEventListener("focus", sync);
      document.removeEventListener("visibilitychange", sync);
    };
  }, [admin, initial.ticket.id, refresh]);
  async function reply(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy || sending.current) return;
    sending.current = true;
    setBusy(true);
    setError("");
    try {
      const idempotencyKey = requestKey || createRequestId();
      setRequestKey(idempotencyKey);
      if (files.length) {
        const form = new FormData();
        form.set("message",message); form.set("idempotencyKey",idempotencyKey);
        for (const file of files) form.append("files",file);
        await request("/messages/attachments",{method:"POST",body:form});
      } else {
        await request("/messages",{method:"POST",body:JSON.stringify({message,idempotencyKey})});
      }
      setFiles([]);
      setMessage("");
      setRequestKey("");
      await refresh().catch(()=>setError("پیام ثبت شد. برای دریافت نسخهٔ تازهٔ گفت‌وگو، صفحه را تازه کنید."));
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "ارتباط برقرار نشد.");
    } finally {
      sending.current = false;
      setBusy(false);
    }
  }
  async function status(value: string) {
    setBusy(true);
    setError("");
    try {
      await request("", {
        method: "PATCH",
        body: JSON.stringify({ status: value }),
      });
      await refresh();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "ذخیره انجام نشد.");
    } finally {
      setBusy(false);
    }
  }
  return (
    <section className="panel ticket-thread">
      <div className="ticket-thread-heading">
        <div>
          <span className="eyebrow">گفت‌وگو با پشتیبانی</span>
          <h2>{data.ticket.subject}</h2>
          {admin && (
            <p>
              {data.ticket.name} ·{" "}
              <Link
                prefetch={false}
                dir="ltr"
                href={"tel:" + data.ticket.phone}
              >
                {data.ticket.phone}
              </Link>
            </p>
          )}
        </div>
        <span className={"ticket-status " + data.ticket.status}>
          {ticketStatuses[data.ticket.status]}
        </span>
      </div>
      {data.ticket.order_id && (
        <p className="notice">
          سفارش مرتبط:{" "}
          <Link
            prefetch={false}
            href={
              (admin ? "/admin/orders/" : "/account/orders/") +
              data.ticket.order_id
            }
          >
            {data.ticket.order_title} · <bdi>{data.ticket.order_code}</bdi>
          </Link>{" · "}<Link prefetch={false} href={(admin ? "/admin/orders/" : "/account/orders/") + data.ticket.order_id + "#order-files"}>فایل‌های سفارش</Link>
        </p>
      )}
      {!!data.orderFiles?.length && <details className="support-order-files"><summary>مشاهدهٔ فایل‌های سفارش مرتبط ({data.orderFiles.length.toLocaleString("fa-IR")})</summary>
        <ul>{data.orderFiles.map(file=><li key={file.id}><a href={(admin ? "/api/admin/files/" : "/api/customer/files/")+file.id}>{file.name}</a> · {(Math.ceil(file.size/1024)).toLocaleString("fa-IR")} کیلوبایت</li>)}</ul>
      </details>}
      <div className="ticket-messages">
        {uniqueById(data.messages).map((item) => (
          <article
            key={item.id}
            data-message-id={item.id}
            className={"ticket-bubble " + item.actor}
          >
            <div>
              <strong>
                <MessageSquare size={14} />
                {item.actor === "admin"
                  ? "پشتیبانی چاپ گیتی"
                  : admin
                    ? data.ticket.name?.trim() || data.ticket.phone || "مشتری"
                    : "شما"}
              </strong>
              {!item.read_at && item.actor === (admin ? "customer" : "admin") && <span className="unread-label">جدید</span>}
              <time dateTime={item.created_at}>
                {ticketTime(item.created_at)}
              </time>
            </div>
            {item.message && <p>{item.message}</p>}
            {!!item.attachments?.length && <ul>{item.attachments.map(file=><li key={file.id}>
              <a href={"/api/"+path+"/attachments/"+file.id} download>{file.name}</a> · {Math.ceil(file.size/1024).toLocaleString("fa-IR")} کیلوبایت
            </li>)}</ul>}
          </article>
        ))}
      </div>
      {!data.messages.length && (
        <p className="ticket-empty">هنوز پیامی برای نمایش وجود ندارد.</p>
      )}
      {error && (
        <p className="error-message" role="alert">
          {error}
        </p>
      )}
      {data.readOnly ? <p role="status">ارسال پیام پس از فعال‌سازی نسخهٔ جدید در دسترس است.</p> : data.ticket.status !== "closed" ? (
        <form onSubmit={reply} className="ticket-reply">
          <label className="field">
            پاسخ شما
            <textarea
              name="reply"
              disabled={busy}
              required={!files.length}
              minLength={files.length ? 0 : 2}
              maxLength={5000}
              rows={4}
              value={message}
              onChange={(event) => {
                setMessage(event.target.value);
                setRequestKey("");
              }}
            />
          </label>
          <TicketAttachments label="افزودن پیوست" files={files} limits={limits} disabled={busy}
            onChange={next => { setFiles(next); setRequestKey(""); setError(""); }} />
          <button className="button" disabled={busy} aria-busy={busy}>
            <Send size={17} />
            {busy ? "در حال ارسال…" : requestKey && error ? "تلاش دوباره برای ارسال" : "ارسال پاسخ"}
          </button>
        </form>
      ) : (
        <p className="notice">
          این تیکت بسته شده است.
          {!admin && (
            <>
              {" "}
              برای درخواست تازه،{" "}
              <Link prefetch={false} href="/tickets#new-ticket">
                تیکت جدید ثبت کنید.
              </Link>
            </>
          )}
        </p>
      )}
      {admin && !data.readOnly && (
        <div className="ticket-status-actions">
          <button
            type="button"
            className="button button-outline button-sm"
            disabled={busy}
            onClick={() =>
              status(data.ticket.status === "closed" ? "open" : "closed")
            }
          >
            {data.ticket.status === "closed"
              ? "باز کردن دوباره تیکت"
              : "بستن تیکت"}
          </button>
        </div>
      )}
    </section>
  );
}
