CREATE TABLE admin_users (
  email TEXT PRIMARY KEY NOT NULL,
  password_hash TEXT NOT NULL,
  created_at TEXT NOT NULL
);
CREATE TABLE admin_sessions (
  token_hash TEXT PRIMARY KEY NOT NULL,
  email TEXT NOT NULL REFERENCES admin_users(email) ON DELETE CASCADE,
  expires INTEGER NOT NULL
);
CREATE INDEX admin_sessions_expires_idx ON admin_sessions(expires);
