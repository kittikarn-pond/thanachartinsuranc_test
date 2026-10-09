export type Product = {
  productCode: string;
  productName: string;
  unitPrice: number;
  /** In stock. */
  quantity: number;
  /** Still free to add to a cart. */
  available: number;
};

/** One page of products. `next` is the code to ask "after" for the following page, or null at the end. */
export type ProductPage = { items: Product[]; next: string | null };

export type CartLine = {
  productCode: string;
  productName: string;
  unitPrice: number;
  quantity: number;
  /** How many more of this product can still be added to a cart. */
  available: number;
};

export type Cart = { items: CartLine[]; total: number };

export type Receipt = { orderId: number; items: CartLine[]; total: number };
