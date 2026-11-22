"use client";

import { useEffect, useRef, useState } from "react";
import { api } from "@/lib/api-client";

type Resource<T> = { path: string; revision: number; data: T | null; error: string };

export function useApiResource<T>(path: string, revalidateOnFocus = false, initialData?: T, enabled = true) {
  const [revision, setRevision] = useState(0);
  const initial = useRef({ path, available: initialData !== undefined });
  const [resource, setResource] = useState<Resource<T> | null>(() => initialData === undefined ? null : { path, revision: 0, data: initialData, error: "" });
  useEffect(() => {
    if (!enabled) return;
    // Authenticated SSR data is already fresh. Focus/retry still increments the
    // revision and fetches normally; other callers retain client loading.
    if (initial.current.path !== path) initial.current.available = false;
    if (revision === 0 && initial.current.available) return;
    const controller = new AbortController();
    api<T>(path, { signal: controller.signal })
      .then(data => { if (!controller.signal.aborted) setResource({ path, revision, data, error: "" }); })
      .catch(cause => {
        if (!controller.signal.aborted)
          setResource({ path, revision, data: null, error: cause instanceof Error ? cause.message : "دریافت انجام نشد." });
      });
    return () => controller.abort();
  }, [path, revision, enabled]);
  useEffect(() => {
    if (!revalidateOnFocus) return;
    const refresh = () => { if (document.visibilityState === "visible") setRevision(value => value + 1); };
    window.addEventListener("focus", refresh);
    document.addEventListener("visibilitychange", refresh);
    return () => {
      window.removeEventListener("focus", refresh);
      document.removeEventListener("visibilitychange", refresh);
    };
  }, [revalidateOnFocus]);
  const current = resource?.path === path && resource.revision === revision;
  return {
    data: current ? resource.data : null,
    error: current ? resource.error : "",
    loading: !current,
    reload: () => setRevision(value => value + 1),
  };
}
