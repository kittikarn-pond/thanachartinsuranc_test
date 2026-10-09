import type { Metadata } from "next";
import { Kanit, Sarabun } from "next/font/google";
import "./globals.css";

const body = Sarabun({ subsets: ["thai", "latin"], weight: ["400", "600"], variable: "--font-body", display: "swap" });
const display = Kanit({ subsets: ["thai", "latin"], weight: ["300", "400"], variable: "--font-display", display: "swap" });

export const metadata: Metadata = {
  title: "คลังสินค้า",
  description: "ดูสต็อกคงเหลือและเบิกสินค้าออกจากคลัง",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="th" className={`${body.variable} ${display.variable}`}>
      <body>{children}</body>
    </html>
  );
}
