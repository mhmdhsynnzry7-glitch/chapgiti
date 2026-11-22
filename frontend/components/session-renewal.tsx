"use client";
import { useEffect } from "react";
import { ensureSession, type SessionContext } from "@/lib/session-client";

export default function SessionRenewal({ context = "customer" }: { context?: SessionContext }) {
  useEffect(() => {
    const check = () => { if (document.visibilityState === "visible") void ensureSession(context); };
    const timer = window.setInterval(check, 60_000);
    window.addEventListener("focus", check);
    return () => { window.clearInterval(timer); window.removeEventListener("focus", check); };
  }, [context]);
  return null;
}
