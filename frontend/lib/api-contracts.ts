import type { PrintSpec, Rates } from "./pricing";
import type { initialSettings } from "./content";
import type { Ticket, TicketMessage } from "./tickets";

export type OrderReceipt = { id: string; code: string; estimate: number };
export type ActionState = { code: string; actor: "customer" | "admin" | "none"; label: string; required: boolean; unread: boolean };
export type OrderSummary = OrderReceipt & {
  attention?: ActionState; updated_at?: string; service?: string;
  customer: string; phone: string; title: string; status: string;
  final_price: number | null; created_at: string;
};
export type PrintOrder = OrderSummary & {
  workflow_version?: number; information_request?: string | null; information_requires_file?: boolean;
  email: string; service: string; spec: Omit<PrintSpec, "quantity" | "pages"> & { quantity?: number; pages?: number; primaryFileId?: string; customerNotes?: string; priceBreakdown?: Partial<Record<"print" | "binding" | "cover" | "extras" | "setup" | "discountAmount" | "total", number>> }; version: number;
  admin_notes: string; updated_at: string; approved_file_id?: string | null;
};
export type OrderEvent = { id: string; status: string; note: string; actor?: string | null; created_at: string };
export type OrderFile = { id: string; name: string; size: number; createdAt?: string; created_at?: string; revisionId?: string | null; fileVersion?: number | null };
export type OrderCorrection = { id: string; revision: number; state: "requested" | "submitted" | "approved" | "rejected" | "superseded";
  requestText: string; requiresNewPdf: boolean; requestedAt: string | null; legacy: boolean; baseFileId?: string | null;
  submissionText?: string | null; submittedAt?: string | null; fileId?: string | null; reviewReason?: string | null; reviewedAt?: string | null; approvedFileId?: string | null; version: number };
export type OrderAttention = { finalConditions?: import("@/components/final-conditions").Conditions | null; orderViewsAvailable?: boolean; viewSequence?: number; correctionsAvailable?: boolean; corrections?: OrderCorrection[]; approvedFileId?: string | null };
export type ProductionReadiness = { canStart: boolean; problems: { code: string; message: string }[]; approvedFileId: string | null };
export type SmsLog = {
  id: string; phone: string; status: string; receipt: string | null;
  error: string | null; created_at: string;
};
export type AuditLog = { id: string; actor: string; action: string; created_at: string };
export type LogEntry = Pick<SmsLog, "id" | "created_at"> & Partial<SmsLog & AuditLog>;
export type OrderDetail = { order: PrintOrder; files: OrderFile[]; events: OrderEvent[]; sms: SmsLog[] };
export type CustomerOrderSummary = Pick<OrderSummary, "attention" | "id" | "code" | "title" | "status" | "estimate" | "final_price" | "created_at"> & { quote_amount?: number | null; quote_revision?: number; information_request?: string | null; service?: string; updated_at?: string };
export type CustomerOrderPage = { items: CustomerOrderSummary[]; total: number; page: number; pageSize: number };
export type OrderQuote = { id: string; order_id: string; revision: number; amount_toman: number; scope_text: string; delivery_terms: string; exclusions_text: string; state: string; sent_at: string; accepted_at?: string | null; decided_at?: string | null; decision_reason?: string; version: number };
export type TrackingResult = OrderAttention & { openTicketId?: string | null; requiresBinding?: boolean; order: CustomerOrderSummary & { version?: number; workflow_version?: number; information_request?: string | null; information_requires_file?: boolean }; events: (Pick<OrderEvent, "status" | "note" | "created_at"> & {actor_role?:"customer"|"admin"|"system"})[]; specification?: Partial<PrintSpec> & { customerNotes?: string; requiresPdf?: boolean }; files?: OrderFile[]; quotesAvailable?: boolean; quotes?: OrderQuote[] };
export type OverviewResult = {
  summary: { total: number; value: number; customers: number };
  counts: { status: string; count: number }[];
  trend: { day: string; count: number }[];
  recent: OrderSummary[];
};
export type OrderListResult = { orders: OrderSummary[]; total: number; page: number };
export type CustomerSummary = {
  phone: string; name: string; email: string; orders: number; value: number; last_order: string;
};
export type ContactMessage = {
  id: string; name: string; phone: string; email: string; subject: string;
  message: string; read: boolean; created_at: string;
};
export type ArticleDraft = {
  id?: string; slug: string; title: string; excerpt: string; content: string;
  category: string; published: boolean; seoTitle: string; seoDescription: string;
};
export type ArticleRecord = ArticleDraft & { id: string; createdAt: string; updatedAt: string };
export type SettingsResult = {
  site: typeof initialSettings; rates: Partial<Rates>;
  orderRequirements?: Record<string, boolean>;
  sms: { enabled: boolean; configured: boolean; template: boolean };
};
export type TicketThreadResult = { attachmentLimits?: {count:number;bytes:number}; ticket: Ticket; messages: TicketMessage[]; orderFiles?: Pick<OrderFile,"id" | "name" | "size">[]; readOnly?: boolean };
