export type Row = Record<string, any>;
export interface Statement {
  bind(...values: any[]): Statement;
  first<T = Row>(): Promise<T | null>;
  all<T = Row>(): Promise<{ results: T[] }>;
  run(): Promise<{ meta: { changes: number } }>;
}
export function dataDirectory(): string;
export function openConnection(): import("node:sqlite").DatabaseSync;
export function migrate(): void;
export function database(): {
  prepare(sql: string): Statement;
  batch(statements: Statement[]): Promise<{ meta: { changes: number } }[]>;
};
