"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { call } from "./api";
import type { Cart, Product, ProductPage, Receipt } from "./types";

const EMPTY_CART: Cart = { items: [], total: 0 };
const PAGE_SIZE = 50;
const CODES_PER_REQUEST = 100;

/**
 * Products (one page at a time, with search), the cart, and every cart action.
 * Actions run one after another so quick clicks are never lost, and after each one only the
 * products it touched are refreshed, never the whole list.
 * A failed action calls onError with the API's message.
 */
export function useShop(onError: (message: string) => void) {
  const cartId = useRef<string | null>(null);
  const [products, setProducts] = useState<Product[] | null>(null);
  const [next, setNext] = useState<string | null>(null);
  const [loadingMore, setLoadingMore] = useState(false);
  const [cart, setCart] = useState<Cart>(EMPTY_CART);
  const [loadFailed, setLoadFailed] = useState(false);
  const [receipt, setReceipt] = useState<Receipt | null>(null);
  const [pending, setPending] = useState(0);
  const queue = useRef<Promise<unknown>>(Promise.resolve());
  const cartRef = useRef<Cart>(EMPTY_CART); // latest cart, readable from queued actions
  const searchText = useRef("");
  const listVersion = useRef(0); // bumped by every new search, so late answers to old searches are dropped

  const applyCart = useCallback((c: Cart) => {
    cartRef.current = c;
    setCart(c);
  }, []);

  const fetchPage = useCallback((query: string, after: string | null) => {
    const params = new URLSearchParams({ limit: String(PAGE_SIZE) });
    if (query) params.set("search", query);
    if (after) params.set("after", after);
    return call<ProductPage>(`/api/products?${params}`);
  }, []);

  const loadFirstPage = useCallback(
    async (query: string) => {
      const version = ++listVersion.current;
      const page = await fetchPage(query, null);
      if (version !== listVersion.current) return;
      setProducts(page.items);
      setNext(page.next);
    },
    [fetchPage],
  );

  const load = useCallback(
    async (id: string) => {
      try {
        const [, c] = await Promise.all([
          loadFirstPage(searchText.current),
          call<Cart>(`/api/carts/${id}`),
        ]);
        applyCart(c);
        setLoadFailed(false);
      } catch {
        setLoadFailed(true);
      }
    },
    [loadFirstPage, applyCart],
  );

  useEffect(() => {
    let id = localStorage.getItem("cartId");
    if (!id) {
      id = crypto.randomUUID();
      localStorage.setItem("cartId", id);
    }
    cartId.current = id;
    load(id);
  }, [load]);

  /** Fresh stock numbers for these products, patched into the cards that are on screen. */
  const refreshCodes = useCallback(async (codes: string[]) => {
    const fresh = new Map<string, Product>();
    for (let i = 0; i < codes.length; i += CODES_PER_REQUEST) {
      const chunk = codes.slice(i, i + CODES_PER_REQUEST);
      const found = await call<Product[]>(
        `/api/products/by-codes?codes=${encodeURIComponent(chunk.join(","))}`,
      );
      found.forEach((p) => fresh.set(p.productCode, p));
    }
    if (fresh.size > 0)
      setProducts(
        (list) => list && list.map((p) => fresh.get(p.productCode) ?? p),
      );
  }, []);

  /** `touched` says which products the action changes. It is read just before the action runs. */
  function run<T>(
    fn: () => Promise<T>,
    apply: (result: T) => void,
    touched: () => string[],
  ) {
    setPending((n) => n + 1);
    queue.current = queue.current.then(async () => {
      const codes = touched();
      try {
        apply(await fn());
      } catch (e) {
        onError((e as Error).message);
      }
      await refreshCodes(codes).catch(() => {});
      setPending((n) => n - 1);
    });
  }

  const base = () => `/api/carts/${cartId.current}`;
  const json = (body: unknown) => JSON.stringify(body);
  const one = (code: string) => () => [code];
  const wholeCart = () => cartRef.current.items.map((i) => i.productCode);

  return {
    products,
    hasMore: next !== null,
    loadingMore,
    cart,
    receipt,
    loadFailed,
    busy: pending > 0,
    reload: () => cartId.current && load(cartId.current),
    closeReceipt: () => setReceipt(null),

    /** Show the first page of products matching this text (empty text = all products). */
    search: (query: string) => {
      if (query === searchText.current) return; // already showing this (the page loads "" itself on start)
      searchText.current = query;
      loadFirstPage(query).catch((e) => onError((e as Error).message));
    },
    loadMore: async () => {
      if (next === null || loadingMore) return;
      const version = listVersion.current;
      setLoadingMore(true);
      try {
        const page = await fetchPage(searchText.current, next);
        if (version !== listVersion.current) return; // the search changed while this was loading
        setProducts((list) => [...(list ?? []), ...page.items]);
        setNext(page.next);
      } catch (e) {
        onError((e as Error).message);
      } finally {
        setLoadingMore(false);
      }
    },

    add: (code: string) =>
      run(
        () =>
          call<Cart>(`${base()}/items`, {
            method: "POST",
            body: json({ productCode: code, quantity: 1 }),
          }),
        applyCart,
        one(code),
      ),
    // +1 / -1 on a cart line. The new quantity is worked out when the action runs (not when it is clicked),
    // so fast double clicks add up instead of both sending the same number.
    step: (code: string, delta: number) =>
      run(
        () => {
          const current =
            cartRef.current.items.find((i) => i.productCode === code)
              ?.quantity ?? 0;
          return call<Cart>(`${base()}/items/${code}`, {
            method: "PUT",
            body: json({ quantity: Math.max(0, current + delta) }),
          });
        },
        applyCart,
        one(code),
      ),
    remove: (code: string) =>
      run(
        () => call<Cart>(`${base()}/items/${code}`, { method: "DELETE" }),
        applyCart,
        one(code),
      ),
    clear: () =>
      run(() => call<Cart>(base(), { method: "DELETE" }), applyCart, wholeCart),
    checkout: () =>
      run(
        () => call<Receipt>(`${base()}/checkout`, { method: "POST" }),
        (r) => {
          setReceipt(r);
          applyCart(EMPTY_CART);
        },
        wholeCart,
      ),
  };
}
