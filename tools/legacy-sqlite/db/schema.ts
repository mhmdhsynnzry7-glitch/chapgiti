import {
  sqliteTable,
  text,
  integer,
  index,
  uniqueIndex,
} from "drizzle-orm/sqlite-core";
export const settings = sqliteTable("settings", {
  key: text("key").primaryKey(),
  value: text("value").notNull(),
});
export const orders = sqliteTable(
  "orders",
  {
    id: text("id").primaryKey(),
    code: text("code").notNull().unique(),
    customer: text("customer").notNull(),
    phone: text("phone").notNull(),
    email: text("email").notNull().default(""),
    title: text("title").notNull(),
    service: text("service").notNull(),
    spec: text("spec").notNull(),
    estimate: integer("estimate").notNull(),
    finalPrice: integer("final_price"),
    status: text("status").notNull().default("new"),
    notes: text("notes").notNull().default(""),
    adminNotes: text("admin_notes").notNull().default(""),
    version: integer("version").notNull().default(1),
    createdAt: text("created_at").notNull(),
    updatedAt: text("updated_at").notNull(),
  },
  (t) => [
    index("orders_created_idx").on(t.createdAt),
    index("orders_status_idx").on(t.status),
    index("orders_phone_idx").on(t.phone),
  ],
);
export const events = sqliteTable("events", {
  id: text("id").primaryKey(),
  orderId: text("order_id")
    .notNull()
    .references(() => orders.id),
  status: text("status").notNull(),
  note: text("note").notNull(),
  actor: text("actor").notNull(),
  createdAt: text("created_at").notNull(),
});
export const files = sqliteTable("files", {
  id: text("id").primaryKey(),
  orderId: text("order_id")
    .notNull()
    .references(() => orders.id),
  objectKey: text("object_key").notNull().unique(),
  name: text("name").notNull(),
  size: integer("size").notNull(),
  createdAt: text("created_at").notNull(),
});
export const articles = sqliteTable("articles", {
  id: text("id").primaryKey(),
  slug: text("slug").notNull().unique(),
  title: text("title").notNull(),
  excerpt: text("excerpt").notNull(),
  content: text("content").notNull(),
  category: text("category").notNull(),
  published: integer("published", { mode: "boolean" }).notNull().default(false),
  seoTitle: text("seo_title").notNull().default(""),
  seoDescription: text("seo_description").notNull().default(""),
  createdAt: text("created_at").notNull(),
  updatedAt: text("updated_at").notNull(),
});
export const messages = sqliteTable("messages", {
  id: text("id").primaryKey(),
  name: text("name").notNull(),
  phone: text("phone").notNull(),
  subject: text("subject").notNull(),
  message: text("message").notNull(),
  read: integer("read", { mode: "boolean" }).notNull().default(false),
  createdAt: text("created_at").notNull(),
});
export const smsLogs = sqliteTable("sms_logs", {
  id: text("id").primaryKey(),
  eventId: text("event_id").notNull().unique(),
  orderId: text("order_id")
    .notNull()
    .references(() => orders.id),
  phone: text("phone").notNull(),
  status: text("status").notNull(),
  receipt: text("receipt"),
  error: text("error"),
  createdAt: text("created_at").notNull(),
});
export const rateLimits = sqliteTable("rate_limits", {
  key: text("key").primaryKey(),
  count: integer("count").notNull(),
  expires: integer("expires").notNull(),
});
export const auditLogs = sqliteTable("audit_logs", {
  id: text("id").primaryKey(),
  actor: text("actor").notNull(),
  action: text("action").notNull(),
  createdAt: text("created_at").notNull(),
});
