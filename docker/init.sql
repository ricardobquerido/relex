CREATE TABLE orders (
  id UUID PRIMARY KEY,
  location_code TEXT NOT NULL,
  product_code TEXT NOT NULL,
  order_date DATE NOT NULL,
  quantity INT NOT NULL,
  submitted_by TEXT NOT NULL,
  submitted_at TIMESTAMPTZ NOT NULL
);
