"use client";
import { useEffect } from "react";
export function useUnsavedForm(dirty: boolean) {
  useEffect(() => {
    if (!dirty) return;
    const unload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = "";
    };
    const leaving = (event: MouseEvent) => {
      const anchor = (event.target as Element).closest("a");
      if (
        !anchor ||
        anchor.target === "_blank" ||
        event.button !== 0 ||
        event.ctrlKey ||
        event.metaKey ||
        event.shiftKey
      )
        return;
      if (
        anchor.href !== window.location.href &&
        !window.confirm("تغییرات ذخیره نشده‌اند. از فرم خارج می‌شوید؟")
      ) {
        event.preventDefault();
        event.stopImmediatePropagation();
      }
    };
    window.addEventListener("beforeunload", unload);
    document.addEventListener("click", leaving, true);
    return () => {
      window.removeEventListener("beforeunload", unload);
      document.removeEventListener("click", leaving, true);
    };
  }, [dirty]);
}
