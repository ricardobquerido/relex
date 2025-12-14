import csv
import os
import sys
import uuid
import random
from datetime import datetime, timedelta, date

# ------------------------------------------------------------
# Configuration (deterministic ID spaces)
# ------------------------------------------------------------

PRESETS = {
    "small": 100_000,
    "medium": 1_000_000,
    "large": 10_000_000,
    "xlarge": 100_000_000,
}

# Dimension sizes (must match lookup table seeding)
LOCATION_COUNT = 1000      # SMALLINT
PRODUCT_COUNT  = 5000      # INT

SUBMITTED_BY = "test@relex.com"

# ------------------------------------------------------------
# Helpers
# ------------------------------------------------------------

def resolve_row_count(arg: str) -> int:
    if arg in PRESETS:
        return PRESETS[arg]
    return int(arg)

def generate_lookups():
    print("Generating lookups...")

    os.makedirs("lookups", exist_ok=True)
    
    # Locations
    with open("lookups/locations.csv", "w", newline="") as f:
        writer = csv.writer(f)
        for i in range(1, LOCATION_COUNT + 1):
            writer.writerow([i, f"LOC-{i:04d}"])
    print(f"Created lookups/locations.csv ({LOCATION_COUNT} rows)")

    # Products
    with open("lookups/products.csv", "w", newline="") as f:
        writer = csv.writer(f)
        for i in range(1, PRODUCT_COUNT + 1):
            writer.writerow([i, f"PROD-{i:05d}"])
    print(f"Created lookups/products.csv ({PRODUCT_COUNT} rows)")

def random_order_date() -> date:
    start = date(2023, 1, 1)
    end   = date(2025, 12, 31)
    delta_days = (end - start).days
    return start + timedelta(days=random.randint(0, delta_days))

def random_submitted_at(order_date: date) -> datetime:
    seconds = random.randint(0, 86399)
    return datetime.combine(order_date, datetime.min.time()) + timedelta(seconds=seconds)

def generate_row():
    order_date = random_order_date()
    # Schema: id, location_id, product_id, order_date, quantity, submitted_by, submitted_at
    return [
        str(uuid.uuid4()),
        random.randint(1, LOCATION_COUNT),
        random.randint(1, PRODUCT_COUNT),
        order_date.isoformat(),
        random.randint(1, 100),
        SUBMITTED_BY,
        random_submitted_at(order_date).isoformat()
    ]

# ------------------------------------------------------------
# Main
# ------------------------------------------------------------

def main():
    if len(sys.argv) != 3:
        print("Usage: python generate_data.py <preset|row_count> <parts>")
        sys.exit(1)

    total_rows = resolve_row_count(sys.argv[1])
    parts = int(sys.argv[2])

    # Generate dimensions first
    generate_lookups()

    os.makedirs("parts", exist_ok=True)

    rows_per_part = total_rows // parts
    remainder = total_rows % parts

    print(f"Generating {total_rows:,} orders into {parts} parts")

    for part in range(parts):
        rows = rows_per_part + (1 if part < remainder else 0)
        path = f"parts/part_{part:03d}.csv"

        with open(path, "w", newline="") as f:
            writer = csv.writer(f)
            for _ in range(rows):
                writer.writerow(generate_row())

        print(f"Created {path} ({rows:,} rows)")

if __name__ == "__main__":
    main()
