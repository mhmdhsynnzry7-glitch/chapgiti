export const ticketStatuses: Record<string, string> = {
  open: "باز", awaiting_customer: "منتظر پاسخ مشتری", closed: "بسته‌شده",
};
export type Ticket = {
  id: string; subject: string; status: string; created_at: string; updated_at: string; name?: string; phone?: string;
  order_id?: string | null; order_code?: string | null; order_title?: string | null; order_status?: string | null;
  unread_customer?: number; unread_admin?: number;
  last_message?: string | null; last_actor?: string | null;
};
export type AttachmentLimits = { count: number; bytes: number };
export const defaultTicketLimits: AttachmentLimits = { count: 3, bytes: 4 * 1024 * 1024 };
export type TicketPage = { attachmentLimits?: AttachmentLimits; readOnly?:boolean; items: Ticket[]; total: number; page: number; pageSize: number; orderLinksAvailable: boolean };
export const ticketTime = (value: string) => new Date(value).toLocaleString("fa-IR", { timeZone: "Asia/Tehran" });
export type TicketMessage = { id: string; actor: string; message: string; created_at: string; read_at?: string | null; attachments?: {id:string;name:string;size:number;mime:string}[] };
