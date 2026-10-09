"use client";

import { useT } from "@/lib/i18n";
import type { Toast } from "@/lib/useToasts";
import styles from "./Toaster.module.css";

/** Error toasts, bottom right. Messages arrive as the API sent them and are translated here. */
export function Toaster({ toasts, onDismiss }: { toasts: Toast[]; onDismiss: (id: number) => void }) {
  const { t, error } = useT();

  return (
    <div className={styles.stack} role="region" aria-live="assertive" aria-label={t("notifications")}>
      {toasts.map((toast) => (
        <div key={toast.id} className={styles.toast} role="alert">
          <p>{error(toast.message)}</p>
          <button type="button" className={styles.close} aria-label={t("close")} onClick={() => onDismiss(toast.id)}>
            ×
          </button>
        </div>
      ))}
    </div>
  );
}
