import { baht } from "@/lib/format";
import { useT } from "@/lib/i18n";
import type { Product } from "@/lib/types";
import styles from "./ProductGrid.module.css";

const LOW_STOCK = 5;

type Props = {
  products: Product[];
  inCart: (productCode: string) => number;
  onAdd: (productCode: string) => void;
  onStep: (productCode: string, delta: number) => void;
};

export function ProductGrid({ products, inCart, onAdd, onStep }: Props) {
  const { t } = useT();

  return (
    <ul className={styles.grid}>
      {products.map((p) => {
        const qty = inCart(p.productCode);
        const out = p.available === 0;
        const low = !out && p.available <= LOW_STOCK;
        return (
          <li key={p.productCode} className={styles.card}>
            <h3 className={styles.name}>{p.productName}</h3>
            <p className={styles.price}>
              <span className="num">{baht(p.unitPrice)}</span> {t("perUnit")}
            </p>
            <ul className={styles.details}>
              <li>{t("code", { code: p.productCode })}</li>
              <li className={out || low ? styles.warn : undefined}>
                {out ? t("outOfStock") : t("inStock", { n: p.available })}
              </li>
            </ul>

            {qty === 0 ? (
              <button type="button" className={`solid ${styles.buy}`} onClick={() => onAdd(p.productCode)} disabled={out}>
                {t("addToCart")}
              </button>
            ) : (
              <div className={styles.stepper}>
                <button type="button" onClick={() => onStep(p.productCode, -1)} aria-label={t("decrease", { name: p.productName })}>−</button>
                <span className="num" aria-live="polite">{qty}</span>
                <button type="button" onClick={() => onStep(p.productCode, 1)} disabled={out} aria-label={t("increase", { name: p.productName })}>+</button>
              </div>
            )}
          </li>
        );
      })}
    </ul>
  );
}
