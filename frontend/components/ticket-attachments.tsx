"use client";
import { useId, useState } from "react";
import type { AttachmentLimits } from "@/lib/tickets";

type Props = {
  files: File[];
  onChange: (files: File[]) => void;
  limits: AttachmentLimits;
  disabled: boolean;
  label: string;
};

const formats: Record<string, string> = {
  pdf: "PDF", jpg: "JPEG", jpeg: "JPEG", png: "PNG", webp: "WebP",
};
function format(name: string) {
  const dot = name.lastIndexOf(".");
  return dot > 0 ? formats[name.slice(dot + 1).toLowerCase()] : undefined;
}
function size(bytes: number) {
  return Math.ceil(bytes / 1024).toLocaleString("fa-IR") + " کیلوبایت";
}

export default function TicketAttachments({ files, onChange, limits, disabled, label }: Props) {
  const id = useId();
  const [error, setError] = useState("");
  const [over, setOver] = useState(false);
  function add(selection: FileList | null) {
    if (disabled || !selection?.length) return;
    const next = [...files, ...Array.from(selection)];
    if (next.length > limits.count) return setError("تعداد پیوست‌ها بیش از حد مجاز است.");
    if (next.some(file => !format(file.name))) return setError("فایل PDF، JPEG، PNG یا WebP انتخاب کنید.");
    if (next.some(file => file.size === 0 || file.size > limits.bytes)) return setError("حجم پیوست معتبر نیست.");
    setError("");
    onChange(next);
  }
  return <div className="ticket-attachments">
    <label className="field" htmlFor={id}>{label}</label>
    <div className={"ticket-drop" + (over ? " dragging" : "")}
      onDragOver={event => { event.preventDefault(); if (!disabled) setOver(true); }}
      onDragLeave={event => { if (!(event.relatedTarget instanceof Node) || !event.currentTarget.contains(event.relatedTarget)) setOver(false); }}
      onDrop={event => { event.preventDefault(); setOver(false); add(event.dataTransfer.files); }}>
      <span>فایل‌ها را اینجا رها کنید.</span>
      <span className="ticket-picker">
        <span aria-hidden="true">انتخاب فایل‌ها</span>
        <input id={id} type="file" multiple disabled={disabled} accept=".pdf,.jpeg,.jpg,.png,.webp"
          aria-describedby={id+"-limits"} onChange={event => { add(event.target.files); event.target.value = ""; }} />
      </span>
    </div>
    <small id={id+"-limits"}>PDF، JPEG، PNG و WebP · حداکثر {limits.count.toLocaleString("fa-IR")} فایل، هر فایل تا {size(limits.bytes)}</small>
    {error && <p role="alert" className="error-message">{error}</p>}
    {files.length > 0 && <ul className="ticket-file-list">{files.map((file, index) =>
      <li key={index}><span><bdi>{file.name}</bdi> · {format(file.name)} · {size(file.size)}</span>
        <button type="button" disabled={disabled} aria-label={"حذف "+file.name} onClick={() => {
          setError(""); onChange(files.filter((_, i) => i !== index));
        }}>حذف</button>
      </li>)}</ul>}
  </div>;
}
