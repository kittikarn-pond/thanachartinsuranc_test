"use client";

import { useCallback, useRef, useState } from "react";

export type Toast = { id: number; message: string };

const SHOW_MS = 6000;
const MAX_TOASTS = 3;

/** Error toasts that close themselves. The same message shown again replaces the old one instead of stacking. */
export function useToasts() {
  const [toasts, setToasts] = useState<Toast[]>([]);
  const nextId = useRef(0);

  const dismiss = useCallback((id: number) => setToasts((all) => all.filter((t) => t.id !== id)), []);

  const notify = useCallback(
    (message: string) => {
      const id = nextId.current++;
      setToasts((all) => [...all.filter((t) => t.message !== message), { id, message }].slice(-MAX_TOASTS));
      setTimeout(() => dismiss(id), SHOW_MS);
    },
    [dismiss],
  );

  return { toasts, notify, dismiss };
}
