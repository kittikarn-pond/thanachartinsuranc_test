# Warehouse — Stock + Issue Cart

Warehouse system: see the stock on hand, put items in an issue cart ("ตะกร้าเบิกสินค้า") and confirm the issue
("ยืนยันการเบิก") to take them out of stock. Stock coming in is recorded by receiving it.

In the code and API this is still called a cart and a checkout (as in the original task); only the screens say
"issue". Folder, project and database names (`ShopApi`, `shopdb`) were kept so nothing else had to change.

| Part | Tech | Folder | Port |
|---|---|---|---|
| Frontend | Next.js (TypeScript), plain CSS, no UI library | `frontend/` | 3000 |
| Backend | ASP.NET Core (.NET 10, MVC controllers, C#) REST API | `backend/ShopApi/` | 5000 |
| Database | MySQL (or MariaDB, e.g. XAMPP) | `db/init.sql` | 3306 |

## Prerequisites

- .NET 10 SDK
- Node.js 20+
- MySQL 8 or MariaDB 10.4+ running locally (XAMPP is fine)

## Run

**1. Database** — creates `shopdb`, the `shop` user, tables and mock data. Run it again any time to reset the data.

```
mysql -u root -p --default-character-set=utf8mb4 < db\init.sql
```

XAMPP: `C:\xampp\mysql\bin\mysql.exe -u root --default-character-set=utf8mb4 < db\init.sql`
(add `-P <port> -h 127.0.0.1` if your MySQL is not on 3306).

**Upgrading a database you already have** (so you keep its data): `init.sql` deletes everything, so to get the
`stock_movements` table without a reset run `db/add-stock-movements.sql` instead. It only adds the table and starts the
history from today's stock, and it is safe to run twice.

```
mysql -u shop -pshoppass --default-character-set=utf8mb4 shopdb < db\add-stock-movements.sql
```

The backend connects with `shop` / `shoppass` on port 3306. If your MySQL uses another port or password, change
`ConnectionStrings:Default` in `backend/ShopApi/appsettings.json`, or put it in
`backend/ShopApi/appsettings.Development.json` (loaded by `dotnet run` and Visual Studio, ignored by git).

**2. Backend**

```
cd backend\ShopApi
dotnet run
```

Or open `backend/ShopApi.sln` in Visual Studio and run the `http` profile (listens on http://localhost:5000).

**3. Frontend**

```
cd frontend
npm install
npm run dev
```

Open http://localhost:3000. The API address is `NEXT_PUBLIC_API_URL` (default `http://localhost:5000`).

**Language.** Use TH | EN in the header; the choice is remembered in the browser. All screen text lives in
`frontend/lib/i18n.ts`, and the API's English error messages are translated there too. Product names come from the
database, so they stay as stored (Thai in the mock data).

## Tests

```
cd backend
dotnet test
```

The unit tests cover the cart, stock and product rules and the error handling against an in-memory repository, so they need no
database. The SQL and row locking in `MySqlCartRepository` have no automated tests.

## How it works

**Available stock** is `stock on hand − quantity held in carts`. Adding, changing or removing cart items only changes
what carts hold. Adding more than is available returns 409. Checkout (confirming the issue) deducts the on-hand stock,
saves the order and empties the cart in one transaction. The stock row is locked (`SELECT ... FOR UPDATE`) while a cart changes, so two
users cannot take the last item.

**Receiving stock** adds to the on-hand quantity; it never sets it. Each addition writes a row to `stock_movements`
(product, quantity, optional reference such as a PO number, time) in the same transaction. A delivery with several
products is received in one call: every line is added or none are. The product must already exist, and the total
cannot exceed 2,147,483,647. `db/init.sql` seeds an "Opening balance" movement per product, so the history adds up to
the on-hand stock.

**Many products.** The product list is never loaded whole. The API returns 50 products at a time (keyset paging on
the code, so page 2,000 is as fast as page 1), with an optional prefix search on code or name; the screen has a search
box and a "Load more" button. After a cart action the frontend asks only for the products that action touched
(`by-codes`) instead of reloading the list, and cart lines carry their own `available`. With 100,000 products in a test
database a page, a search and an add-to-cart each took about 5–70 ms; reloading every product with its availability took
about 1.7 s and 3.5 MB of raw rows. Both name and code have indexes, and `cart_items` is indexed by product code.

**Backend layout**

```
Controllers/   HTTP only: routes in, JSON out. ShopExceptionHandler and InvalidRequestResponse turn errors into { message }
Services/      CartService (carts, checkout), StockService (receiving stock) and ProductService (paging, search)
Data/          ICartRepository and MySqlCartRepository (Dapper; all SQL lives here)
Models/        Product, Cart, CartLine, Receipt, StockLine, StockMovement, requests, ShopException
```

## API

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/products?search=&after=&limit=50` | One page of products ordered by code: `{ "items": [...], "next": "P050" }`. Each item has `quantity` (on hand) and `available`. Pass `next` as `after` for the following page (`next` is null on the last page). `search` keeps products whose code or name **starts with** the text. `limit` is 1–100 |
| GET | `/api/products/by-codes?codes=P001,P002` | Just those products (max 200 codes), to refresh a few cards |
| POST | `/api/products/{productCode}/stock` | Receive one product: `{ "quantity": 20, "reference": "PO-1001" }`. `reference` is optional (max 200 chars). Returns the product |
| POST | `/api/stock/receipts` | Receive a delivery: `{ "reference": "PO-1001", "lines": [{ "productCode": "P001", "quantity": 20 }] }`. Each product may appear once. Returns the products |
| GET | `/api/carts/{cartId}` | Cart and total. Each line has `available` (how many more can be added) |
| POST | `/api/carts/{cartId}/items` | Add `{ "productCode": "P001", "quantity": 1 }` |
| PUT | `/api/carts/{cartId}/items/{productCode}` | Set quantity `{ "quantity": 3 }` (0 removes) |
| DELETE | `/api/carts/{cartId}/items/{productCode}` | Remove one item |
| DELETE | `/api/carts/{cartId}` | Clear the cart |
| POST | `/api/carts/{cartId}/checkout` | Check out (confirm the issue), returns the issue slip |

Errors return `{ "message": "..." }` with 400 (invalid request, empty cart), 404 (unknown product), 409 (not enough
stock) or 500 (unexpected; the message is generic and the cause is only in the log). A request body ASP.NET cannot read
(missing field, wrong type, bad JSON) is also a 400, with `{ "message": "Invalid request: <field>" }`.

`cartId` is a GUID the frontend creates and keeps in the browser's localStorage.

The frontend shows every failed action as a toast (red, bottom right, closes after 6 seconds). Messages are translated
in `frontend/lib/i18n.ts`; unknown ones are shown as the API sent them.

## Logging

Every rejected request is logged as a warning with the request and the reason, and every unexpected error as an error
with its stack trace. Output goes to the console and to `backend/ShopApi/logs/shopapi-YYYYMMDD.log` (one file per day,
the last 30 kept; git ignores the folder). Levels and the file path are in the `Serilog` section of
`backend/ShopApi/appsettings.json`. The `Logging` section in `appsettings.Development.json` no longer has any effect.

```
2026-10-09 11:04:59.854 +07:00 [WRN] POST /api/carts/{id}/items rejected (400): Quantity must be greater than 0
```

## Known limitations

- Abandoned carts keep their stock reserved. There is no cart expiry; clearing the cart or re-running `db/init.sql`
  releases it.
- There is no login, so every endpoint is open. `stock_movements` records what came in and when, but not who did it.
- Receiving only works for existing products (unknown codes return 404). New products must be added to the database
  directly.
- Search matches the start of a code or name, not the middle ("ข้าว" finds "ข้าวสาร", "สาร" does not). Searching the
  middle of a name scans the table; Thai has no spaces between words, so a MySQL FULLTEXT index would not help.
- Stock numbers on the cards are refreshed for the products you touch. Changes other people make to other products
  appear when you search, load more or reload the page; the server still refuses anything that is out of stock.
- Each add-to-cart sums that product's cart rows. That is cheap for a page, but at heavy load a `reserved` column on the
  stock row (kept in step in the same transaction) would make it a single-row read.
- `stock_movements` only records stock coming in. Stock going out at checkout is not recorded there.
- Mock data comes from `db/init.sql`; there is no import program.
