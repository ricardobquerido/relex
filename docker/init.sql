-- ============================================================
-- Database initialization script (schema + partitions only)
-- This script is executed once when the PostgreSQL container
-- is first initialized.
-- ============================================================

-- ------------------------------------------------------------
-- 1. Lookup tables (dimensions)
-- These tables reduce storage and index bloat in the main
-- fact table (orders) at large scale.
-- IDs are explicit (not SERIAL) to keep ingestion deterministic.
-- ------------------------------------------------------------

CREATE TABLE locations (
    id SMALLINT PRIMARY KEY,
    code TEXT NOT NULL UNIQUE
);

CREATE TABLE products (
    id INT PRIMARY KEY,
    code TEXT NOT NULL UNIQUE
);

-- ------------------------------------------------------------
-- 2. Partitioned fact table: orders
-- Partitioned by order_date (monthly range partitions).
-- ------------------------------------------------------------

CREATE TABLE orders (
    id UUID NOT NULL,
    location_id SMALLINT NOT NULL REFERENCES locations(id),
    product_id INT NOT NULL REFERENCES products(id),
    order_date DATE NOT NULL,
    quantity INT NOT NULL CHECK (quantity > 0),
    submitted_by TEXT NOT NULL,
    submitted_at TIMESTAMPTZ NOT NULL,
    status INT NOT NULL DEFAULT 0,
    PRIMARY KEY (order_date, id)
) PARTITION BY RANGE (order_date);

-- ------------------------------------------------------------
-- 3. Monthly partitions with fixed boundaries
--
-- Fixed date boundaries are used to ensure deterministic and
-- reproducible schema creation across environments.
-- Generated data is guaranteed to fall within this range.
--
-- Coverage: 2023-01-01 (inclusive) to 2026-01-01 (exclusive)
-- ------------------------------------------------------------

DO $$
DECLARE
    start_date DATE := DATE '2023-01-01';
    end_date   DATE := DATE '2026-01-01';
    curr_date  DATE := start_date;
    partition_name TEXT;
BEGIN
    WHILE curr_date < end_date LOOP
        partition_name :=
            'orders_y' || to_char(curr_date, 'YYYY') ||
            '_m' || to_char(curr_date, 'MM');

        EXECUTE format(
            'CREATE TABLE IF NOT EXISTS %I PARTITION OF orders
             FOR VALUES FROM (%L) TO (%L)',
            partition_name,
            curr_date,
            curr_date + INTERVAL '1 month'
        );

        curr_date := curr_date + INTERVAL '1 month';
    END LOOP;
END $$;

-- ------------------------------------------------------------
-- 4. Default partition (safety net)
-- Ensures inserts do not fail if data falls outside the
-- expected partition range.
-- ------------------------------------------------------------

CREATE TABLE orders_default PARTITION OF orders DEFAULT;
