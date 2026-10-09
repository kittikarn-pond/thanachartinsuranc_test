import { baht } from "@/lib/format";
import { useT } from "@/lib/i18n";
import type { Cart as CartData } from "@/lib/types";
import styles from "./Cart.module.css";

type Props = {
  cart: CartData;
  busy: boolean;
  canIncrease: (productCode: string) => boolean;
  onStep: (productCode: string, delta: number) => void;
  onRemove: (productCode: string) => void;
  onClear: () => void;
  onCheckout: () => void;
};

export function Cart({ cart, busy, canIncrease, onStep, onRemove, onClear, onCheckout }: Props) {
  const { t } = useT();

  return (
    <section id="cart" className={styles.cart} aria-labelledby="cart-title">
      <h2 id="cart-title">{t("cartTitle")}</h2>

      {cart.items.length === 0 ? (
        <p className={styles.empty}>{t("cartEmpty")}</p>
      ) : (
        <>
          <ul>
            {cart.items.map((i) => (
              <li key={i.productCode} className={styles.item}>
                <div className={styles.top}>
                  <span>{i.productName}</span>
                  <span className="num">{baht(i.unitPrice * i.quantity)}</span>
                </div>
                <div className={styles.bottom}>
                  <div className={styles.stepper}>
                    <button type="button" onClick={() => onStep(i.productCode, -1)} disabled={i.quantity <= 1}
                      aria-label={t("decrease", { name: i.productName })}>−</button>
                    <span className="num">{i.quantity}</span>
                    <button type="button" onClick={() => onStep(i.productCode, 1)} disabled={!canIncrease(i.productCode)}
                      aria-label={t("increase", { name: i.productName })}>+</button>
                    <span className={styles.each}>× {baht(i.unitPrice)}</span>
                  </div>
                  <button type="button" className="plain" onClick={() => onRemove(i.productCode)}
                    aria-label={t("removeItem", { name: i.productName })}>{t("remove")}</button>
                </div>
              </li>
            ))}
          </ul>

          <p className={styles.total}>
            <span>{t("total")}</span>
            <strong className="num">{baht(cart.total)} {t("baht")}</strong>
          </p>
          <button type="button" className={`solid ${styles.checkout}`} onClick={onCheckout} disabled={busy}>
            {t("confirm")}
          </button>
          <button type="button" className={`plain ${styles.clear}`} onClick={onClear} disabled={busy}>
            {t("clear")}
          </button>
        </>
      )}
    </section>
  );
}
