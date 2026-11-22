CREATE TABLE customer_registrations (
  id TEXT PRIMARY KEY NOT NULL,
  phone TEXT NOT NULL,
  first_name TEXT NOT NULL DEFAULT '',
  last_name TEXT NOT NULL DEFAULT '',
  phone_verified INTEGER NOT NULL DEFAULT 0,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);
CREATE INDEX customer_registrations_phone_idx ON customer_registrations(phone);
CREATE TABLE customer_sessions (
  token_hash TEXT PRIMARY KEY NOT NULL,
  registration_id TEXT NOT NULL REFERENCES customer_registrations(id) ON DELETE CASCADE,
  expires INTEGER NOT NULL
);
CREATE INDEX customer_sessions_expires_idx ON customer_sessions(expires);
