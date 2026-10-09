import { useEffect, useRef } from "react";
import { baht, orderNo } from "@/lib/format";
import { useT } from "@/lib/i18n";
import type { Receipt } from "@/lib/types";
import styles from "./ReceiptDialog.module.css";

/** Shown after a successful check out (the issue is confirmed). */
export function ReceiptDialog({ receipt, onClose }: { receipt: Receipt | null; onClose: () => void }) {
  const { t } = useT();
  const ref = useRef<HTMLDialogElement>(null);

  useEffect(() => {
    const dialog = ref.current;
    if (!dialog) return;
    if (receipt && !dialog.open) dialog.showModal();
    if (!receipt && dialog.open) dialog.close();
  }, [receipt]);

  return (
    <dialog ref={ref} className={styles.dialog} onClose={onClose}>
      {receipt && (
        <>
          <h2>{t("issued")}</h2>
          <p className={styles.order}>{t("issueNo", { no: orderNo(receipt.orderId) })}</p>
          <ul>
            {receipt.items.map((i) => (
              <li key={i.productCode}>
                <span>{i.productName} × {i.quantity}</span>
                <span className="num">{baht(i.unitPrice * i.quantity)}</span>
              </li>
            ))}
          </ul>
          <p className={styles.total}>
            <span>{t("total")}</span>
            <strong className="num">{baht(receipt.total)} {t("baht")}</strong>
          </p>
          <button type="button" className="solid" onClick={onClose}>{t("close")}</button>
        </>
      )}
    </dialog>
  );
}
