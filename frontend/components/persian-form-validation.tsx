"use client";
import { useEffect } from "react";

type Field = HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement;
function field(target: EventTarget | null): target is Field {
  return target instanceof HTMLInputElement || target instanceof HTMLTextAreaElement || target instanceof HTMLSelectElement;
}
function hint(input: Field) {
  const v = input.validity;
  if (v.valueMissing) return "این فیلد الزامی است.";
  if (input instanceof HTMLInputElement && input.type === "email" && v.typeMismatch) return "یک نشانی ایمیل معتبر وارد کنید.";
  if (v.tooShort && "minLength" in input) return "حداقل " + input.minLength.toLocaleString("fa-IR") + " نویسه وارد کنید.";
  if (v.tooLong && "maxLength" in input) return "حداکثر " + input.maxLength.toLocaleString("fa-IR") + " نویسه مجاز است.";
  if (input instanceof HTMLInputElement) {
    if (v.rangeUnderflow) return "مقدار باید حداقل " + input.min + " باشد.";
    if (v.rangeOverflow) return "مقدار باید حداکثر " + input.max + " باشد.";
    if (v.badInput || v.stepMismatch) return "یک عدد معتبر در محدودهٔ مشخص‌شده وارد کنید.";
    if (input.type === "tel") return "شماره موبایل معتبر وارد کنید.";
    if (input.type === "password") return "رمز عبور باید با شرایط نوشته‌شده زیر فیلد هم‌خوانی داشته باشد.";
  }
  return "مقدار واردشده را بررسی کنید.";
}
export default function PersianFormValidation() {
  useEffect(() => {
    const managed = new WeakSet<Field>();
    const invalid = (event: Event) => {
      const input = event.target;
      if (!field(input) || (input.validity.customError && !managed.has(input))) return;
      input.setCustomValidity(hint(input)); managed.add(input);
    };
    const clear = (event: Event) => {
      if (field(event.target) && managed.delete(event.target)) event.target.setCustomValidity("");
    };
    document.addEventListener("invalid", invalid, true);
    document.addEventListener("input", clear, true);
    document.addEventListener("change", clear, true);
    return () => {
      document.removeEventListener("invalid", invalid, true);
      document.removeEventListener("input", clear, true);
      document.removeEventListener("change", clear, true);
    };
  }, []);
  return null;
}
