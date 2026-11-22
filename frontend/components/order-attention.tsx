import type { ActionState } from "@/lib/api-contracts";

export function OrderAttention({state}: {state?: ActionState}) {
  if (!state) return null;
  return <span className="order-attention">{state.unread && <span className="unread-label">خوانده‌نشده</span>}
    {state.actor !== "none" && <span className={state.required ? "action-required" : "awaiting-person"}>{state.required ? "نیازمند اقدام شما" : state.actor === "admin" ? "در انتظار مدیریت" : "در انتظار مشتری"}</span>}</span>;
}
export function ActionFilter({value, change}: {value:string; change:(value:string)=>void}) {
  return <label className="field action-filter">پیگیری اقدام<select value={value} onChange={event=>change(event.target.value)}>
    <option value="">همه سفارش‌ها</option><option value="new">سفارش‌های جدید</option><option value="unread">خوانده‌نشده</option>
    <option value="mine">نیازمند اقدام من</option><option value="customer">در انتظار مشتری</option><option value="admin">در انتظار مدیریت</option>
  </select></label>;
}
export function ActionSummary({state}: {state: ActionState}) {
  const actor = state.actor === "admin" ? "چاپخانه" : "مشتری";
  return <p className={state.required ? "notice next-action" : "notice"}>{state.actor === "none" ? "اقدام دیگری لازم نیست." : (state.required ? "اقدام شما" : "اقدام بعدی " + actor) + " — " + state.label}</p>;
}
