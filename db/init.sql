DROP DATABASE IF EXISTS shopdb;
CREATE DATABASE shopdb CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER IF NOT EXISTS 'shop'@'localhost' IDENTIFIED BY 'shoppass';
GRANT ALL PRIVILEGES ON shopdb.* TO 'shop'@'localhost';
FLUSH PRIVILEGES;
USE shopdb;

SET NAMES utf8mb4;

CREATE TABLE products (
  product_code VARCHAR(20)   NOT NULL PRIMARY KEY,
  product_name VARCHAR(200)  NOT NULL,
  unit_price   DECIMAL(10,2) NOT NULL,
  INDEX ix_products_name (product_name)   -- product search matches the start of the name
) ENGINE=InnoDB;

-- On-hand stock. Quantity reserved by carts is tracked in cart_items.
CREATE TABLE stock (
  product_code VARCHAR(20) NOT NULL PRIMARY KEY,
  quantity     INT         NOT NULL CHECK (quantity >= 0),
  CONSTRAINT fk_stock_product FOREIGN KEY (product_code) REFERENCES products(product_code)
) ENGINE=InnoDB;

-- History of stock coming in (goods received). One row per product per receipt.
CREATE TABLE stock_movements (
  movement_id  BIGINT       NOT NULL AUTO_INCREMENT PRIMARY KEY,
  product_code VARCHAR(20)  NOT NULL,
  quantity     INT          NOT NULL CHECK (quantity > 0),
  reference    VARCHAR(200) NULL,
  created_at   TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
  INDEX ix_sm_product (product_code, created_at),
  CONSTRAINT fk_sm_product FOREIGN KEY (product_code) REFERENCES products(product_code)
) ENGINE=InnoDB;

CREATE TABLE carts (
  cart_id    CHAR(36)  NOT NULL PRIMARY KEY,
  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB;

CREATE TABLE cart_items (
  cart_id      CHAR(36)    NOT NULL,
  product_code VARCHAR(20) NOT NULL,
  quantity     INT         NOT NULL CHECK (quantity > 0),
  PRIMARY KEY (cart_id, product_code),
  CONSTRAINT fk_ci_cart    FOREIGN KEY (cart_id)      REFERENCES carts(cart_id) ON DELETE CASCADE,
  CONSTRAINT fk_ci_product FOREIGN KEY (product_code) REFERENCES products(product_code)
) ENGINE=InnoDB;

CREATE TABLE orders (
  order_id     BIGINT        NOT NULL AUTO_INCREMENT PRIMARY KEY,
  total_amount DECIMAL(12,2) NOT NULL,
  created_at   TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB;

CREATE TABLE order_items (
  order_id     BIGINT        NOT NULL,
  product_code VARCHAR(20)   NOT NULL,
  product_name VARCHAR(200)  NOT NULL,
  unit_price   DECIMAL(10,2) NOT NULL,
  quantity     INT           NOT NULL,
  PRIMARY KEY (order_id, product_code),
  CONSTRAINT fk_oi_order FOREIGN KEY (order_id) REFERENCES orders(order_id)
) ENGINE=InnoDB;

-- Mock data
INSERT INTO products (product_code, product_name, unit_price) VALUES
  ('P001', 'น้ำดื่ม 600 มล.',          7.00),
  ('P002', 'มาม่า รสต้มยำกุ้ง',        8.00),
  ('P003', 'ข้าวสาร 5 กก.',          165.00),
  ('P004', 'นมสด 1 ลิตร',             48.00),
  ('P005', 'ไข่ไก่ เบอร์ 2 (แผง 30)', 120.00),
  ('P006', 'น้ำมันพืช 1 ลิตร',        59.00),
  ('P007', 'กาแฟ 3-in-1 (กล่อง 27 ซอง)', 99.00),
  ('P008', 'สบู่ก้อน',                 25.00);

INSERT INTO stock (product_code, quantity) VALUES
  ('P001', 100),
  ('P002', 50),
  ('P003', 20),
  ('P004', 30),
  ('P005', 15),
  ('P006', 25),
  ('P007', 10),
  ('P008', 3);

-- Opening balance, so the movement history adds up to the on-hand stock.
INSERT INTO stock_movements (product_code, quantity, reference)
SELECT product_code, quantity, 'Opening balance' FROM stock WHERE quantity > 0;
