"use client";

import { useSyncExternalStore } from "react";

export type Lang = "th" | "en";
type Params = Record<string, string | number>;

const th = {
  brand: "คลังสินค้า",
  menu: "เมนูหลัก",
  language: "ภาษา",
  navProducts: "สินค้าในคลัง",
  navCart: "ตะกร้าเบิกสินค้า",
  cartPill: "ตะกร้าเบิก",

  title: "สินค้าในคลัง",
  subtitle: "ดูสต็อกคงเหลือ และเบิกสินค้าออกจากคลัง",
  loading: "กำลังโหลด…",
  failed: "เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ ตรวจสอบว่า backend ทำงานอยู่ที่ {url} และเปิด MySQL แล้ว",
  retry: "ลองอีกครั้ง",
  searchLabel: "ค้นหาสินค้า",
  searchPlaceholder: "พิมพ์รหัสหรือชื่อสินค้า",
  noResults: "ไม่พบสินค้าที่ขึ้นต้นด้วย “{q}”",
  loadMore: "โหลดเพิ่ม",

  baht: "บาท",
  perUnit: "บาท/หน่วย",
  code: "รหัสสินค้า {code}",
  inStock: "คงเหลือ {n} ชิ้น",
  outOfStock: "หมดสต็อก",
  addToCart: "เพิ่มลงตะกร้าเบิก",
  decrease: "ลด {name}",
  increase: "เพิ่ม {name}",
  removeItem: "ลบ {name}",
  remove: "ลบ",

  cartTitle: "ตะกร้าเบิกสินค้า",
  cartEmpty: "ยังไม่มีสินค้าที่จะเบิก",
  total: "มูลค่ารวม",
  confirm: "ยืนยันการเบิก",
  clear: "ล้างตะกร้า",

  issued: "เบิกสินค้าสำเร็จ",
  issueNo: "ใบเบิก {no}",
  close: "ปิด",

  errNetwork: "เชื่อมต่อเซิร์ฟเวอร์ไม่ได้",
  errHttp: "เกิดข้อผิดพลาด ({n})",
  errMoreAvail: "สต็อกไม่พอ เพิ่มได้อีกเพียง {n} ชิ้น",
  errMaxInCart: "สต็อกไม่พอ ในตะกร้าได้สูงสุด {n} ชิ้น",
  errStockFor: "สต็อกไม่พอสำหรับ {code}",
  errEmptyCart: "ตะกร้าว่าง",
  errNotFound: "ไม่พบสินค้า",
  errServer: "เกิดข้อผิดพลาด กรุณาลองอีกครั้ง",
  errInvalid: "ข้อมูลที่ส่งไม่ถูกต้อง",
  notifications: "การแจ้งเตือน",
};

export type Key = keyof typeof th;

const en: Record<Key, string> = {
  brand: "Warehouse",
  menu: "Main menu",
  language: "Language",
  navProducts: "Stock",
  navCart: "Issue cart",
  cartPill: "Cart",

  title: "Warehouse stock",
  subtitle: "See what is in stock and issue items from the warehouse",
  loading: "Loading…",
  failed: "Cannot reach the server. Check that the backend is running at {url} and MySQL is on.",
  retry: "Try again",
  searchLabel: "Search items",
  searchPlaceholder: "Type an item code or name",
  noResults: "No items start with “{q}”",
  loadMore: "Load more",

  baht: "THB",
  perUnit: "THB/unit",
  code: "Item code {code}",
  inStock: "{n} in stock",
  outOfStock: "Out of stock",
  addToCart: "Add to issue cart",
  decrease: "Decrease {name}",
  increase: "Increase {name}",
  removeItem: "Remove {name}",
  remove: "Remove",

  cartTitle: "Issue cart",
  cartEmpty: "Nothing to issue yet",
  total: "Total value",
  confirm: "Confirm issue",
  clear: "Clear cart",

  issued: "Items issued",
  issueNo: "Issue slip {no}",
  close: "Close",

  errNetwork: "Cannot reach the server",
  errHttp: "Something went wrong ({n})",
  errMoreAvail: "Not enough stock. Only {n} more available.",
  errMaxInCart: "Not enough stock. At most {n} can be in the cart.",
  errStockFor: "Not enough stock for {code}",
  errEmptyCart: "The cart is empty",
  errNotFound: "Product not found",
  errServer: "Something went wrong. Please try again.",
  errInvalid: "The request was not valid",
  notifications: "Notifications",
};

const dictionary = { th, en };

export function translate(lang: Lang, key: Key, params?: Params): string {
  return dictionary[lang][key].replace(/\{(\w+)\}/g, (_, name) => String(params?.[name] ?? ""));
}

/** The API sends its error messages in English; show the known ones in the chosen language. */
export function translateError(lang: Lang, message: string): string {
  const t = (key: Key, params?: Params) => translate(lang, key, params);
  let m: RegExpMatchArray | null;
  if (message === "NETWORK") return t("errNetwork");
  if ((m = message.match(/^HTTP_(\d+)$/))) return t("errHttp", { n: m[1] });
  if ((m = message.match(/^Not enough stock\. Only (\d+) more available\.$/))) return t("errMoreAvail", { n: m[1] });
  if ((m = message.match(/^Not enough stock\. Only (\d+) can be in cart\.$/))) return t("errMaxInCart", { n: m[1] });
  if ((m = message.match(/^Not enough stock for (.+)$/))) return t("errStockFor", { code: m[1] });
  if (message === "Cart is empty") return t("errEmptyCart");
  if (message === "Product not found") return t("errNotFound");
  if (message === "Something went wrong. Please try again.") return t("errServer");
  if (message.startsWith("Invalid request")) return t("errInvalid");
  return message;
}

// The chosen language is kept in localStorage; this tiny store lets every component re-render when it changes.
const listeners = new Set<() => void>();

function getLang(): Lang {
  try {
    return localStorage.getItem("lang") === "en" ? "en" : "th";
  } catch {
    return "th";
  }
}

function subscribe(callback: () => void) {
  listeners.add(callback);
  window.addEventListener("storage", callback);
  return () => {
    listeners.delete(callback);
    window.removeEventListener("storage", callback);
  };
}

export function setLang(lang: Lang) {
  try {
    localStorage.setItem("lang", lang);
  } catch {
    /* storage blocked: the switch just will not be remembered */
  }
  listeners.forEach((callback) => callback());
}

export function useT() {
  const lang = useSyncExternalStore(subscribe, getLang, (): Lang => "th");
  return {
    lang,
    t: (key: Key, params?: Params) => translate(lang, key, params),
    error: (message: string) => translateError(lang, message),
  };
}
