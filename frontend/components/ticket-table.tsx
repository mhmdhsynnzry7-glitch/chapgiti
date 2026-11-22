import Link from "next/link";
import ManagementTable from "./management-table";
import { ticketTime, ticketStatuses, type Ticket } from "@/lib/tickets";
export default function TicketTable({
  items,
  admin = false,
  reload,
  busy,
}: {
  items: Ticket[];
  admin?: boolean;
  reload: () => void;
  busy: boolean;
}) {
  return (
    <ManagementTable
      remote
      title="درخواست‌های پشتیبانی"
      refresh={reload}
      busy={busy}
      columns={[
        "شماره درخواست",
        ...(admin ? ["مشتری"] : []),
        "موضوع",
        "نوع",
        "سفارش مرتبط",
        "آخرین پیام",
        "وضعیت",
        "پیام خوانده‌نشده",
        "آخرین فعالیت",
      ]}
      rows={items.map((t) => ({
        id: t.id,
        values: [
          t.id.slice(0, 8),
          ...(admin ? [t.name || t.phone || "ثبت نشده"] : []),
          t.subject,
          t.order_id ? "سفارش" : "عمومی",
          t.order_code || "—",
          t.last_message || "—",
          ticketStatuses[t.status],
          (admin ? t.unread_admin : t.unread_customer) ?? 0,
          ticketTime(t.updated_at),
        ],
        emphasis: ((admin ? t.unread_admin : t.unread_customer) ?? 0) > 0,
        actions: (
          <Link href={(admin ? "/admin/tickets/" : "/tickets/") + t.id}>
            مشاهده
          </Link>
        ),
      }))}
    />
  );
}
