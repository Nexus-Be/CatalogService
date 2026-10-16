# 01 — PHÂN TẦNG CLEAN ARCHITECTURE & QUY TẮC PHỤ THUỘC (CATALOG SERVICE)

Solution `Catalog.sln` bao gồm 4 project tương ứng với 4 tầng kiến trúc sạch (Clean Architecture):

```
Catalog.sln
├── Catalog.Domain/           # Lõi thực thể (Category, Product, Inventory, Domain Events)
├── Catalog.Application/      # Use cases CQRS (Commands, Queries, DTOs, Event Consumers)
├── Catalog.Infrastructure/   # EF Core DbContext, PostgreSQL mappings, Outbox Pattern
└── Catalog.Api/              # Điểm khởi chạy gRPC Server (Program.cs, DI setup)
```

---

## 1. NGUYÊN TẮC LUỒNG PHỤ THUỘC (DEPENDENCY RULE)

```
[ Catalog.Api ] ────────────┐
                            │
                            ▼
               [ Catalog.Application ] ──▶ [ Catalog.Domain ]
                            ▲
                            │
[ Catalog.Infrastructure ] ─┘
```
- **`Catalog.Domain`**: Lớp trung tâm, không phụ thuộc bất kỳ project nào. Chứa Entity `Product`, `Category`, `Inventory`, Value Object `Money`, và các Domain Exceptions (ví dụ: `InsufficientStockException`).
- **`Catalog.Application`**: Phụ thuộc vào `Catalog.Domain`. Khai báo interface `ICatalogDbContext`, các Commands (ví dụ `CreateProductCommand`, `AdjustInventoryCommand`), Queries (ví dụ `GetProductByIdQuery`, `ListProductsQuery`) và các Consumers nghe RabbitMQ.
- **`Catalog.Infrastructure`**: Phụ thuộc `Catalog.Application` và `Catalog.Domain`. Chứa `CatalogDbContext`, cấu hình EntityTypeConfiguration Fluent API, migrations, và tích hợp MassTransit EntityFramework Outbox.
- **`Catalog.Api`**: Điểm khởi động gRPC Server, đăng ký `app.MapGrpcService<CatalogGrpcService>()`.

---

## 2. QUY CHUẨN CODE TỪNG TẦNG

1. **`Catalog.Domain`**:
   - Entities: `Category`, `Product`, `Inventory`.
   - Thuộc tính `Available` của `Inventory` phải có phương thức `Deduct(int quantity)` và `Add(int quantity)` đảm bảo `Available >= 0`. Nếu số lượng không đủ, ném ra domain exception.

2. **`Catalog.Application`**:
   - Tách biệt `Commands/` và `Queries/` qua `MediatR`.
   - Với các Queries phân trang (`ListProducts`, `ListCategories`): luôn dùng `.AsNoTracking()` và áp dụng `.Skip().Take()` theo `pageIndex` và `pageSize` (tối đa 100 items/trang).

3. **`Catalog.Infrastructure`**:
   - Cấu hình Index trên các trường tìm kiếm thường xuyên: `Product.Slug` (Unique Index), `Product.CategoryId` (Index), `Product.SellerId` (Index).
