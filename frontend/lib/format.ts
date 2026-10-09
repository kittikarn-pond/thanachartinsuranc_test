export const baht = (n: number) =>
  n.toLocaleString("th-TH", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export const orderNo = (id: number) => `#${String(id).padStart(4, "0")}`;
