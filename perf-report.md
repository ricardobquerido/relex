## Performance Observations (Local Benchmark)

_Generated on: 2025-12-18 20:37:27_

Environment:
- OS: Microsoft Windows NT 10.0.26100.0
- CPU: AMD Ryzen 5 5600G with Radeon Graphics         
- RAM (GB): 15.8

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

