"use client";
import type { ComponentProps } from "react";
import { normalizeDigits } from "@/lib/pricing";

export default function CountInput({ value, onValueChange, min, max, ...props }: Omit<ComponentProps<"input">, "value" | "onChange" | "min" | "max"> & {
  value: number; onValueChange: (value: number) => void; min: number; max: number;
}) {
  return <input {...props} ref={element => { element?.setCustomValidity(Number.isSafeInteger(value) && value >= min && value <= max ? "" : "تعداد را در محدودهٔ مجاز وارد کنید."); }} type="text" inputMode="numeric" pattern="[0-9]+" value={Number.isFinite(value) ? value : ""} onChange={event => {
    const text = normalizeDigits(event.target.value);
    const number = /^[0-9]+$/.test(text) ? Number(text) : Number.NaN;
    event.target.setCustomValidity(Number.isSafeInteger(number) && number >= min && number <= max
      ? "" : "یک عدد صحیح بین " + min.toLocaleString("fa-IR") + " و " + max.toLocaleString("fa-IR") + " وارد کنید.");
    onValueChange(number);
  }} />;
}
