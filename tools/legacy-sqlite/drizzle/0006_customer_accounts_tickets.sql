CREATE TABLE customer_accounts (
  phone TEXT PRIMARY KEY NOT NULL,
  registration_id TEXT NOT NULL UNIQUE REFERENCES customer_registrations(id),
  password_hash TEXT NOT NULL,
  created_at TEXT NOT NULL
);
ALTER TABLE orders ADD COLUMN customer_id TEXT REFERENCES customer_registrations(id);
CREATE INDEX orders_customer_idx ON orders(customer_id);
CREATE TABLE tickets (
  id TEXT PRIMARY KEY NOT NULL,
  customer_id TEXT NOT NULL REFERENCES customer_registrations(id),
  subject TEXT NOT NULL,
  status TEXT NOT NULL DEFAULT 'open' CHECK(status IN ('open','awaiting_customer','closed')),
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);
CREATE INDEX tickets_customer_idx ON tickets(customer_id,updated_at);
CREATE TABLE ticket_messages (
  id TEXT PRIMARY KEY NOT NULL,
  ticket_id TEXT NOT NULL REFERENCES tickets(id),
  actor TEXT NOT NULL CHECK(actor IN ('customer','admin')),
  message TEXT NOT NULL,
  created_at TEXT NOT NULL
);
CREATE INDEX ticket_messages_ticket_idx ON ticket_messages(ticket_id,created_at);
