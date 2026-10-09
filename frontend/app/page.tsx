"use client";

import { useEffect, useState } from "react";
import { Cart } from "@/components/Cart";
import { ProductGrid } from "@/components/ProductGrid";
import { ReceiptDialog } from "@/components/ReceiptDialog";
import { SiteHeader } from "@/components/SiteHeader";
import { Toaster } from "@/components/Toaster";
import { API } from "@/lib/api";
import { translate, useT } from "@/lib/i18n";
import { useShop } from "@/lib/useShop";
import { useToasts } from "@/lib/useToasts";
import styles from "./page.module.css";

const SEARCH_DELAY_MS = 300;

export default function Home() {
  const { toasts, notify, dismiss } = useToasts();
  const shop = useShop(notify);
  const { t, lang } = useT();
  const [query, setQuery] = useState("");
  const { search } = shop;

  // keep the browser tab title and <html lang> in the chosen language
  useEffect(() => {
    document.documentElement.lang = lang;
    document.title = translate(lang, "brand");
  }, [lang]);

  // search a moment after typing stops, not on every key
  const trimmed = query.trim();
  useEffect(() => {
    const timer = setTimeout(() => search(trimmed), SEARCH_DELAY_MS);
    return () => clearTimeout(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- `search` is recreated every render; only the text should restart the timer
  }, [trimmed]);

  const inCart = (code: string) => shop.cart.items.find((i) => i.productCode === code)?.quantity ?? 0;
  // the cart lines carry their own "available", so this works for items that are not on the page being shown
  const canIncrease = (code: string) => (shop.cart.items.find((i) => i.productCode === code)?.available ?? 0) > 0;
  const count = shop.cart.items.reduce((n, i) => n + i.quantity, 0);

  return (
    <div id="top">
      <SiteHeader count={count} total={shop.cart.total} />

      <main className={styles.layout}>
        <section id="products" className={styles.panel} aria-labelledby="products-title">
          <h1 id="products-title" className={styles.title}>{t("title")}</h1>
          <p className={styles.subtitle}>{t("subtitle")}</p>

          <input
            type="search"
            className={styles.search}
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder={t("searchPlaceholder")}
            aria-label={t("searchLabel")}
            maxLength={100}
          />

          {shop.loadFailed ? (
            <div className={styles.failed} role="alert">
              <p>{t("failed", { url: API })}</p>
              <button type="button" onClick={shop.reload}>{t("retry")}</button>
            </div>
          ) : shop.products === null ? (
            <p className={styles.loading}>{t("loading")}</p>
          ) : shop.products.length === 0 ? (
            <p className={styles.loading}>{t("noResults", { q: trimmed })}</p>
          ) : (
            <>
              <ProductGrid products={shop.products} inCart={inCart} onAdd={shop.add} onStep={shop.step} />
              {shop.hasMore && (
                <div className={styles.more}>
                  <button type="button" onClick={shop.loadMore} disabled={shop.loadingMore}>
                    {shop.loadingMore ? t("loading") : t("loadMore")}
                  </button>
                </div>
              )}
            </>
          )}
        </section>

        <aside className={styles.side}>
          <Cart
            cart={shop.cart}
            busy={shop.busy}
            canIncrease={canIncrease}
            onStep={shop.step}
            onRemove={shop.remove}
            onClear={shop.clear}
            onCheckout={shop.checkout}
          />
        </aside>
      </main>

      <ReceiptDialog receipt={shop.receipt} onClose={shop.closeReceipt} />
      <Toaster toasts={toasts} onDismiss={dismiss} />
    </div>
  );
}
