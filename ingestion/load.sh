#!/usr/bin/env bash
set -euo pipefail

# ------------------------------------------------------------
# Pipeline:
# 1. Start PostgreSQL
# 2. Generate lookup + fact CSVs
# 3. Load lookup tables
# 4. Load fact table (partitioned)
# 5. Create post-load indexes
# ------------------------------------------------------------

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

SIZE=${1:-small}
PARTS=${2:-4}

COMPOSE_FILE="../docker/docker-compose.yml"
POST_LOAD_INDEXES="../docker/post_load_indexes.sql"

echo "Starting PostgreSQL (if not already running)..."
docker compose -f "$COMPOSE_FILE" up -d

DB_CONTAINER=$(docker compose -f "$COMPOSE_FILE" ps -q db)

echo "Waiting for PostgreSQL to be ready..."
until docker exec "$DB_CONTAINER" pg_isready -U postgres > /dev/null 2>&1; do
  sleep 1
done

echo "Generating CSV data..."
python generate_data.py "$SIZE" "$PARTS"

# ------------------------------------------------------------
# Helper: COPY a CSV file into a table with explicit columns
# ------------------------------------------------------------
copy_csv() {
  local file="$1"
  local table="$2"
  local columns="$3"

  echo "Loading $(basename "$file") into $table"

  docker cp "$file" "$DB_CONTAINER":/tmp/$(basename "$file")

  docker exec "$DB_CONTAINER" psql -U postgres -d relex -c \
    "\COPY $table ($columns) FROM '/tmp/$(basename "$file")' CSV"
}

echo "Loading lookup tables..."

copy_csv "lookups/locations.csv" "locations" "id,code"
copy_csv "lookups/products.csv"  "products"  "id,code"

echo "Loading orders (partitioned fact table)..."

for file in parts/*.csv; do
  copy_csv "$file" "orders" \
    "id,location_id,product_id,order_date,quantity,submitted_by,submitted_at,status"
done

echo "Creating indexes and updating planner statistics..."

docker cp "$POST_LOAD_INDEXES" "$DB_CONTAINER":/tmp/post_load_indexes.sql
docker exec "$DB_CONTAINER" psql -U postgres -d relex -f /tmp/post_load_indexes.sql

echo "Data load completed successfully."
