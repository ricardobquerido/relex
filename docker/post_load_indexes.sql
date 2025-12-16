-- ============================================================
-- Indexes for normalized, partitioned orders table
-- Run AFTER bulk load
-- ============================================================

-- Fast lookup by order ID (API GET /orders/{id})
CREATE INDEX IF NOT EXISTS idx_orders_id
ON orders (id);

-- Foreign key helper indexes
CREATE INDEX IF NOT EXISTS idx_orders_location_fk
ON orders (location_id);

CREATE INDEX IF NOT EXISTS idx_orders_product_fk
ON orders (product_id);

-- Orders for a location in a date range
-- INCLUDE enables index-only scans
CREATE INDEX IF NOT EXISTS idx_orders_location_date
ON orders (location_id, order_date)
INCLUDE (product_id, quantity, submitted_at);

-- Analytics-friendly index for large scans by product
CREATE INDEX IF NOT EXISTS idx_orders_product_brin
ON orders USING BRIN (product_id);

-- Filter by Status (e.g. for workflow or analytics)
CREATE INDEX IF NOT EXISTS idx_orders_status
ON orders (status);

-- Update planner statistics
ANALYZE orders;
