import { database } from "./database.mjs";
import { hashPassword } from "./password.mjs";

export async function setAdminCredentials({ email, username, password }) {
  email = String(email).trim().toLowerCase();
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) throw new Error("Invalid email.");
  if (username !== undefined) {
    username = String(username).trim().toLowerCase();
    if (!/^[a-z][a-z0-9_.-]{2,31}$/.test(username))
      throw new Error("Username must be 3-32 characters: letters, numbers, _, . or -.");
  }
  const hash = await hashPassword(password);
  const db = database();
  const statements = [
    db.prepare("INSERT INTO admin_users (email,password_hash,created_at) VALUES (?,?,?) ON CONFLICT(email) DO UPDATE SET password_hash=excluded.password_hash")
      .bind(email, hash, new Date().toISOString()),
  ];
  if (username !== undefined) {
    statements.push(db.prepare("INSERT INTO admin_usernames (username,email) VALUES (?,?) ON CONFLICT(email) DO UPDATE SET username=excluded.username").bind(username, email));
  }
  statements.push(db.prepare("DELETE FROM admin_sessions WHERE email=?").bind(email));
  await db.batch(statements);
  return { email, username };
}
