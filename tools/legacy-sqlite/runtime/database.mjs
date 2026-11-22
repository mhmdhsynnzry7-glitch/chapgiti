import { DatabaseSync } from "node:sqlite";
import { mkdirSync, readFileSync, readdirSync } from "node:fs";
import path from "node:path";
import { createHash } from "node:crypto";

export function dataDirectory() {
  return path.resolve(process.env.DATA_DIR || path.join(process.cwd(), "data"));
}
export function openConnection() {
  const dir = dataDirectory();
  mkdirSync(dir, { recursive: true });
  const db = new DatabaseSync(path.join(dir, "site.sqlite"));
  db.exec("PRAGMA foreign_keys=ON; PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;");
  return db;
}
export function migrate() {
  const db = openConnection();
  try {
    db.exec("CREATE TABLE IF NOT EXISTS app_migrations (name TEXT PRIMARY KEY, checksum TEXT NOT NULL, applied_at TEXT NOT NULL)");
    const directory = path.join(process.cwd(), "drizzle");
    for (const name of readdirSync(directory).filter(n => n.endsWith(".sql")).sort()) {
      const sql = readFileSync(path.join(directory, name), "utf8");
      const checksum = createHash("sha256").update(sql.replace(/\r\n/g, "\n")).digest("hex");
      db.exec("BEGIN IMMEDIATE");
      try {
        const existing = db.prepare("SELECT checksum FROM app_migrations WHERE name=?").get(name);
        if (existing && existing.checksum !== checksum) throw new Error("Applied migration was changed: " + name);
        if (!existing) {
          db.exec(sql);
          db.prepare("INSERT INTO app_migrations VALUES (?,?,?)").run(name, checksum, new Date().toISOString());
        }
        db.exec("COMMIT");
      } catch (e) { db.exec("ROLLBACK"); throw e; }
    }
  } finally { db.close(); }
}
class Statement {
  constructor(db, sql, values = []) { this.db = db; this.sql = sql; this.values = values; }
  bind(...values) { return new Statement(this.db, this.sql, values); }
  async first() { return this.db.prepare(this.sql).get(...this.values) ?? null; }
  async all() { return { results: this.db.prepare(this.sql).all(...this.values) }; }
  execute() {
    const result = this.db.prepare(this.sql).run(...this.values);
    return { meta: { changes: Number(result.changes) } };
  }
  async run() { return this.execute(); }
}
export function database() {
  // Share one connection across hot reloads. Each batch executes synchronously in one transaction.
  const key = Symbol.for("naghsh.sqlite:" + dataDirectory());
  globalThis[key] ??= openConnection();
  const db = globalThis[key];
  return {
    prepare(sql) { return new Statement(db, sql); },
    async batch(statements) {
      db.exec("BEGIN IMMEDIATE");
      try {
        const results = statements.map(s => s.execute());
        db.exec("COMMIT");
        return results;
      } catch (e) { db.exec("ROLLBACK"); throw e; }
    }
  };
}
