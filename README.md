# RELEX Backend Challenge - High Performance Order API

This repository contains a high-performance backend solution designed to handle and serve 100M+ order records efficiently.

## 🚀 Key Features

*   **Scale**: Capable of ingesting and querying 100M+ records.
*   **Architecture**: .NET 10 Vertical Slice Architecture for modularity and performance isolation.
*   **Database**: PostgreSQL 15 with Declarative Partitioning (by Month) and Normalized Schema.
*   **Performance**:
    *   **Ingestion**: Python generator + `COPY` protocol via Docker networking (approx 100k+ rows/sec).
    *   **Reads**: EF Core `AsNoTracking` with optimized indexes (B-Tree + Covering Indexes).
    *   **Writes**: Single-record writes via EF Core, Bulk inserts via raw Npgsql Binary Import.
    *   **Validation**: Domain rules (Cut-off times, Status checks) enforced.

## 🛠️ Prerequisites

*   Docker & Docker Compose
*   .NET 10 SDK (for local API development)
*   Python 3.x (for data generation, if running outside Docker)

## 📖 API Endpoints

### Orders
*   `GET /orders` - List orders (Paged, Filterable by Location/Date).
*   `GET /orders/{id}` - Retrieve a single order.
*   `POST /orders` - Create a new order.
*   `PUT /orders/{id}` - Update an existing order.
*   `DELETE /orders/{id}` - Delete an order (Validation: Must be Pending and future date).
*   `GET /orders/stats` - Aggregated statistics (Count, Sum, Avg, Date Range).
*   `POST /orders/bulk` - High-performance bulk insert (Streamed).

### Reference Data
*   `GET /locations` - List all available locations.
*   `GET /products` - List all available products.

### Monitoring
*   `GET /health` - Application health check.

## 🏃 Quick Start

### 1. Start Infrastructure & Load Data
Dockerized pipeline to initialize the DB, generate data, and load it efficiently.

```bash
cd ingestion

# Usage: ./load.sh <preset> <parts>
# Presets: small (100k), medium (1M), large (10M), xlarge (100M)
./load.sh medium 4
```

This script will:
1.  Spin up Postgres (tuned for bulk writing).
2.  Generate lookup data (`locations.csv`, `products.csv`).
3.  Generate random order data partitioned into CSV parts.
4.  Bulk load everything using `COPY`.
5.  Create indexes **after** the load to maximize speed.

### 2. Run the API

```bash
cd src
dotnet run --project Relex.Api/Relex.Api.csproj
```

The API will be available at: `http://localhost:5080` (or the port if specified in launch profile).
Swagger UI: `http://localhost:5080/swagger`

## 🧪 Running Tests

This solution includes a comprehensive test suite covering Unit and Integration scenarios.

### 1. Unit Tests
Located in `src/Relex.Tests/OrderUnitTests.cs`. These tests verify domain logic, validation rules, and isolated handler behavior using an In-Memory Database and Moq.

```bash
cd src
dotnet test --filter "FullyQualifiedName~UnitTests"
```

### 2. Integration Tests
Located in `src/Relex.Tests/OrderIntegrationTests.cs`. These tests spin up a real **PostgreSQL Testcontainer** to verify the full API pipeline, including database persistence, constraints, and raw SQL/COPY operations.

*Requires Docker to be running.*

```bash
cd src
dotnet test
```

## ⚖️ Significant Trade-offs

### 1. Raw `COPY` (Binary Import) vs. EF Core `AddRange`
*   **Decision**: Used `NpgsqlBinaryImporter` (PostgreSQL `COPY`) for the `POST /orders/bulk` endpoint.
*   **Alternative**: EF Core `AddRangeAsync` / `BulkExtensions`.
*   **Why**: `AddRange` tracks changes and generates SQL `INSERT` statements, which caps out at a few thousand rows/sec. `COPY` bypasses the SQL parser and writes binary data directly, achieving **100k+ rows/sec**. For daily replenishment of 100M rows, this speed difference is critical.

### 2. In-Memory Lookup Cache vs. Database Joins
*   **Decision**: Loaded `Locations` and `Products` into a Singleton `LookupCache` at startup.
*   **Alternative**: Performing a `db.Locations.Find()` or `JOIN` for every row during validation.
*   **Why**: Ingesting 100M rows implies 200M foreign key lookups. Hitting the DB for each one would cause massive I/O latency (N+1 problem). An in-memory dictionary (`O(1)`) allows the ingestion pipeline to be CPU-bound rather than I/O-bound.

### 3. JSON Streaming (IAsyncEnumerable) vs. Complete Payload Loading
*   **Decision**: The bulk endpoint streams the request body using `System.Text.Json`'s `DeserializeAsyncEnumerable`.
*   **Alternative**: Binding the entire `List<Order>` to memory (Standard Model Binding).
*   **Why**: Loading a 1GB JSON payload into RAM causes Large Object Heap (LOH) fragmentation and OutOfMemory exceptions. Streaming processes items one-by-one, keeping memory usage constant regardless of input size.

### 4. Vertical Slice + Minimal APIs vs. Layered Architecture (Controllers/Services/Repositories)
*   **Decision**: Implemented features as Vertical Slices using .NET Minimal APIs (e.g., `GetOrder.cs`, `CreateOrder.cs`, `BulkInsert.cs`) where each endpoint owns its request/validation/data access.
*   **Alternative**: A classic layered design (Controller -> Service -> Repository) with shared abstractions and cross-cutting pipelines.
*   **Why**: This challenge prioritizes end-to-end performance and clarity of hot paths. Vertical slices keep the critical logic for an operation in one place, making it easy to optimize per endpoint (e.g., raw Npgsql for bulk, EF Core for CRUD) without forcing a single abstraction across very different performance profiles.
*   **Trade-off**: More repetition (similar validation/mapping patterns per feature) and fewer shared extension points compared to a layered approach. In a larger product, would typically add conventions (shared response/validation helpers) or graduate selected slices into reusable application services as duplication appears.

### 5. Single Record Operations by Partition Key (OrderDate) vs. Global ID Lookup
*   **Decision**: The `GET`, `PUT`, and `DELETE` endpoints (`/orders/{id}`) optionally accept `OrderDate` (query param or body).
*   **Alternative**: Querying by `Id` alone.
*   **Why**: PostgreSQL partitioned tables behave like separate tables. Querying by ID without the partition key requires checking *all* partitions (or a global index, which we avoid for insert speed). Providing `OrderDate` allows "Partition Pruning", targeting only the relevant monthly table, which is significantly faster and reduces lock contention.

## 📐 Architecture & Design Decisions

### Database Strategy
*   **Normalization**: Replaced repeated string codes (`LOC-0001`, `PROD-01234`) with integer FKs (`smallint`, `int`). This saves **~1.5GB of storage** per 100M rows and significantly reduces index size.
*   **Partitioning**: Used `PARTITION BY RANGE (order_date)` with monthly partitions. This keeps individual B-Tree indexes small and allows for efficient vacuuming and archiving.
*   **Deferred Indexing**: Secondary indexes are created *after* the initial bulk load. Inserting into an unindexed heap is O(1), whereas updating indexes per row is expensive.

### API Architecture (.NET 10)
*   **Vertical Slices**: Instead of generic Layers (Controller -> Service -> Repo), features are isolated (`GetOrder.cs`, `CreateOrder.cs`, `BulkInsert.cs`). This allows optimizing specific slices (e.g., using raw Npgsql for bulk, EF for CRUD) without affecting others.
*   **Minimal APIs**: Lower memory overhead and faster startup than MVC Controllers.
*   **Hybrid Data Access**:
    *   **EF Core**: Used for `Get` and `Create` (rich domain modeling, safety).
    *   **Npgsql**: Used for `Bulk Insert` (bypassing change tracking for raw speed).

## 📊 Performance Observations (Local Benchmark)

To generate an updated benchmark table on your machine, make sure API is running:
(With Ingestion)
```powershell
./scripts/generate-perf-report.ps1 -BaseUrl "http://localhost:5080" -IngestionPreset medium -IngestionParts 4 -OutputPath perf-report.md
```
(Without Ingestion)
```powershell
.\scripts\generate-perf-report.ps1 -BaseUrl "http://localhost:5080" -SkipIngestion -OutputPath perf-report.md
```

| Operation | Scale | Method | Time / Rate |
|-----------|-------|--------|-------------|
| **Ingestion** | medium (1M rows) | COPY (Docker) | 14.2s |
| **Health** | 1 Request | GET /health | 4.89 ms |
| **List Locations** | All | GET /locations | median 7.98 ms (p95 15.4 ms) |
| **List Products** | All | GET /products | median 33.45 ms (p95 74.01 ms) |
| **Order Stats** | Aggregation | GET /orders/stats | median 9.4 ms (p95 13.21 ms) |
| **Create Order** | 1 Record | Minimal API + EF Core | 12.28 ms |
| **Get Order** | 1 Record | PK Lookup (+ optional partition pruning) | median 2.27 ms (p95 2.83 ms) |
| **Get Order (no OrderDate)** | 1 Record | PK Lookup (all partitions) | median 39.71 ms (p95 46.72 ms) |
| **List Orders** | Page 1 (20) | FK Index Scan | median 27.61 ms (p95 38.91 ms) |
| **Update Order** | 1 Record | PUT /orders/{id} | 36.99 ms |
| **Delete Order** | 1 Record | DELETE /orders/{id} | 35.61 ms |
| **Bulk API** | 10000 Batch | BinaryImport | 252.04 ms |
