"use client";
import { useLayoutEffect, useRef, type InputHTMLAttributes } from "react";
import { normalizedAmountInput } from "@/lib/amount-input";
import { groupedToman } from "@/lib/money";
type Props = Omit<
  InputHTMLAttributes<HTMLInputElement>,
  "value" | "onChange" | "type"
> & { value: string; change: (value: string) => void };
export default function TomanInput({ value, change, ...props }: Props) {
  const input = useRef<HTMLInputElement>(null),
    position = useRef<number | null>(null);
  const digits = normalizedAmountInput(value);
  const display =
    digits && /^\d{1,12}$/.test(digits) ? groupedToman(Number(digits)) : digits;
  useLayoutEffect(() => {
    if (position.current === null || !input.current) return;
    let i = 0,
      count = 0;
    while (i < display.length && count < position.current) {
      if (/\d/.test(display[i])) count++;
      i++;
    }
    input.current.setSelectionRange(i, i);
    position.current = null;
  }, [display]);
  return (
    <input
      {...props}
      ref={input}
      type="text"
      inputMode="numeric"
      dir="ltr"
      value={display}
      onChange={(e) => {
        const next = normalizedAmountInput(e.target.value);
        if (!/^\d{0,12}$/.test(next)) return;
        position.current = normalizedAmountInput(
          e.target.value.slice(0, e.target.selectionStart ?? 0),
        ).length;
        change(next);
      }}
    />
  );
}
