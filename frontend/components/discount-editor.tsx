"use client";
import { useState, type FormEvent } from "react";
import { useApiResource } from "@/hooks/use-api-resource";
import { api } from "@/lib/api-client";
type Policy = { active: boolean; threshold: number; percentage: number };
export default function DiscountEditor() {
  const { data, error, reload } = useApiResource<Policy>(
    "admin/quantity-discount",
  );
  const [busy, setBusy] = useState(false),
    [message, setMessage] = useState("");
  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy) return;
    const f = new FormData(event.currentTarget);
    setBusy(true);
    try {
      await api("admin/quantity-discount", {
        method: "PUT",
        body: JSON.stringify({
          active: f.has("active"),
          threshold: Number(f.get("threshold")),
          percentage: Number(f.get("percentage")),
        }),
      });
      reload();
      setMessage("سیاست تخفیف ذخیره شد.");
    } catch (e) {
      setMessage(e instanceof Error ? e.message : "ذخیره انجام نشد.");
    } finally {
      setBusy(false);
    }
  }
  return (
    <details className="panel">
      <summary>تخفیف تیراژ</summary>
      {error && <p role="alert">{error}</p>}
      {data && (
        <form key={JSON.stringify(data)} onSubmit={save} className="filterbar">
          <label>
            تیراژ بیشتر از
            <input
              name="threshold"
              type="number"
              min="0"
              max="10000"
              required
              defaultValue={data.threshold}
            />
          </label>
          <label>
            درصد تخفیف
            <input
              name="percentage"
              type="number"
              min="0"
              max="100"
              required
              defaultValue={data.percentage}
            />
          </label>
          <label>
            <input name="active" type="checkbox" defaultChecked={data.active} />{" "}
            فعال
          </label>
          <button disabled={busy}>ذخیره تخفیف</button>
        </form>
      )}
      <p role="status">{message}</p>
    </details>
  );
}
