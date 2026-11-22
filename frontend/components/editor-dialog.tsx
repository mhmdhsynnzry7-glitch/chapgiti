"use client";
import { useEffect, useId, useRef, type ReactNode } from "react";
import { createPortal } from "react-dom";
import styles from "./management.module.css";
export default function EditorDialog({
  title,
  close,
  children,
}: {
  title: string;
  close: () => void;
  children: ReactNode;
}) {
  const ref = useRef<HTMLDialogElement>(null),
    id = useId();
  useEffect(() => {
    const dialog = ref.current;
    dialog?.showModal();
    return () => dialog?.close();
  }, []);
  return createPortal(
    <dialog
      ref={ref}
      dir="rtl"
      className={styles.dialog}
      aria-labelledby={id}
      onCancel={(e) => {
        e.preventDefault();
        close();
      }}
      onClick={(e) => {
        if (e.target === ref.current) {
          const rect = ref.current.getBoundingClientRect();
          if (
            e.clientX < rect.left ||
            e.clientX > rect.right ||
            e.clientY < rect.top ||
            e.clientY > rect.bottom
          )
            close();
        }
      }}
    >
      <header>
        <h2 id={id}>{title}</h2>
        <button type="button" onClick={close} aria-label="بستن">
          ×
        </button>
      </header>
      {children}
    </dialog>,
    document.body,
  );
}
