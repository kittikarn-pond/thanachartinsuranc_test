import { baht } from "@/lib/format";
import { setLang, useT, type Lang } from "@/lib/i18n";
import styles from "./SiteHeader.module.css";

const LANGS: Lang[] = ["th", "en"];

export function SiteHeader({ count, total }: { count: number; total: number }) {
  const { t, lang } = useT();

  return (
    <header className={styles.header}>
      <div className={styles.bar} />
      <div className={styles.inner}>
        <a href="#top" className={styles.brand}>{t("brand")}</a>

        <nav className={styles.nav} aria-label={t("menu")}>
          <a href="#products">{t("navProducts")}</a>
          <a href="#cart">{t("navCart")}</a>
        </nav>

        <div className={styles.right}>
          <div className={styles.langs} role="group" aria-label={t("language")}>
            {LANGS.map((l) => (
              <button key={l} type="button" lang={l} aria-pressed={lang === l} onClick={() => setLang(l)}>
                {l.toUpperCase()}
              </button>
            ))}
          </div>

          <a href="#cart" className={styles.cart}>
            {t("cartPill")} <strong className="num">{count}</strong>
            {count > 0 && <span className={`${styles.sum} num`}>{baht(total)} {t("baht")}</span>}
          </a>
        </div>
      </div>
    </header>
  );
}
