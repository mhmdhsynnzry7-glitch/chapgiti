import { database } from "./database.mjs";
import { hashPassword, verifyPassword } from "./password.mjs";

export async function customerAccount(phone) {
  return database().prepare("SELECT registration_id,password_hash FROM customer_accounts WHERE phone=?").bind(phone).first();
}
export async function authenticateCustomer(phone, password) {
  const row = await customerAccount(phone);
  const fallback = "scrypt$" + "0".repeat(32) + "$" + "0".repeat(128);
  const valid = await verifyPassword(password, row?.password_hash || fallback);
  return row && valid ? row.registration_id : null;
}
export async function completeCustomerProfile(customer, firstName, lastName, password) {
  const db = database(), account = await customerAccount(customer.phone);
  if (account && account.registration_id !== customer.id) {
    const error = new Error("Phone already registered");
    error.code = "PHONE_IN_USE";
    throw error;
  }
  const updatedAt = new Date().toISOString();
  const statements = [db.prepare("UPDATE customer_registrations SET first_name=?,last_name=?,updated_at=? WHERE id=? AND phone=?")
    .bind(firstName, lastName, updatedAt, customer.id, customer.phone)];
  if (!account) {
    const hash = await hashPassword(password);
    statements.push(db.prepare("INSERT INTO customer_accounts (phone,registration_id,password_hash,created_at) VALUES (?,?,?,?)")
      .bind(customer.phone, customer.id, hash, updatedAt));
  }
  try { await db.batch(statements); }
  catch (cause) {
    const winner = await customerAccount(customer.phone);
    if (winner && winner.registration_id !== customer.id) {
      const error = new Error("Phone already registered"); error.code = "PHONE_IN_USE"; throw error;
    }
    throw cause;
  }
}
