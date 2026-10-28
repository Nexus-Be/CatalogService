# 01 — PHÂN TẦNG CLEAN ARCHITECTURE & QUY TẮC PHỤ THUỘC (CATALOG SERVICE)

> **Trạng thái triển khai (2026-10-29):** ✅ ĐÃ CHUẨN HÓA — 4 tầng Clean Architecture chuẩn hóa: Feature Co-location cho Product và Category tại `Catalog.Application/Services/`, 1-to-1 REST Controllers tại `Catalog.Api/Controllers/ProductsController.cs` và `CategoriesController.cs`, Consumer DI extension `ConsumerExtensions.cs`, 100% RESTful HTTP/JSON (CẤM gRPC).
> **Nguồn sự thật:** code trong repo này (`Catalog.sln`, `Catalog.Application/Services/`, `Catalog.Application/Consumers/`, `Catalog.Api/Controllers/`).
> **Cập nhật lần cuối:** 2026-10-29.

Solution `Catalog.sln` bao gồm 4 project tương ứng với 4 tầng kiến trúc sạch (Clean Architecture):

```
Catalog.sln
├── Catalog.Domain/           # Lõi thực thể (9 entity: Category, Product, ProductVariant, ...)
├── Catalog.Application/      # Use cases (Services/Product, Services/Category, DTOs, ICatalogDbContext)
├── Catalog.Infrastructure/   # EF Core AppDbContext, PostgreSQL mappings, MassTransit Outbox
└── Catalog.Api/              # Điểm khởi chạy REST API (Program.cs, DI setup, Controllers, Mock)
```

> ❌ Các thành phần từng được ghi trong sơ đồ này **không tồn tại**: entity `Inventory`, "Domain Events", tầng Application kiểu CQRS (`Commands/`, `Queries/`, `Event Consumers`), và "gRPC Server". Bằng chứng: `Catalog.Domain/Entities/` chỉ có 9 file entity; `Catalog.Application/` chỉ có `Interfaces/`, `Services/`, `DependencyInjection.cs`; không có `PackageReference` MediatR (`Catalog.Application/Catalog.Application.csproj`).

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
- **`Catalog.Domain`**: Lớp trung tâm, không phụ thuộc bất kỳ project nào (`Catalog.Domain/Catalog.Domain.csproj` có **0** `PackageReference`). Chứa 9 Entity (`Category`, `Product`, `ProductVariant`, `ProductVariantAttribute`, `ProductImage`, `ProductReview`, `ProductRatingStats`, `PurchaseRecord`, `AuditLog`), 7 enum và 3 exception (`DomainException`, `EntityNotFoundException`, `BusinessRuleValidationException`).
  - ⚠️ **Không có** Entity `Inventory`; **không có** Value Object `Money` (giá nằm trực tiếp ở `ProductVariant.Price`/`Currency`); **không có** `InsufficientStockException`; **không có** Domain Events trong toàn repo.
- **`Catalog.Application`**: Phụ thuộc vào `Catalog.Domain`. Khai báo `ICatalogDbContext` (`Catalog.Application/Interfaces/ICatalogDbContext.cs:8-21`, 9 `DbSet` + `SaveChangesAsync`) và 2 dịch vụ thật:
  - `IProductService` (`Services/Product/IProductService.cs:8-12`): `GetByIdAsync`, `ListAsync(page, pageSize)`.
  - `ICategoryService` (`Services/Category/ICategoryService.cs`): `GetByIdAsync`, `ListAllAsync`.
  - ❌ Không có Commands/Queries (`CreateProductCommand`, `GetProductByIdQuery`…), không có Consumer RabbitMQ nào.
  - ⚠️ Cả 2 service được đăng ký DI (`Catalog.Application/DependencyInjection.cs:11-12`) nhưng **0 nơi inject/gọi** — dead code.
- **`Catalog.Infrastructure`**: Phụ thuộc `Catalog.Application` và `Catalog.Domain`. Chứa `AppDbContext` (`Persistence/AppDbContext.cs:8`), 9 `EntityTypeConfiguration` Fluent API, migration `20261024103127_InitialCatalogSchema`, seed `HasData`, và tích hợp MassTransit EntityFramework Outbox (`DependencyInjection.cs:23-48`, có hạ tầng nhưng 0 consumer/0 publish).
  - ⚠️ Class `CatalogDbContext : AppDbContext` (`AppDbContext.cs:37-41`) **không được đăng ký DI** — chỉ `AppDbContext` được dùng (`DependencyInjection.cs:17-20`).
- **`Catalog.Api`**: Điểm khởi động **REST API** — `builder.Services.AddControllers()` (`Program.cs:30`), `app.MapControllers()` (`Program.cs:69`), `app.MapGet("/health", …)` (`Program.cs:66`).
  - ❌ **Không có** `app.MapGrpcService<CatalogGrpcService>()`, không có class `CatalogGrpcService`, không có `Contracts/shop.proto`.

---

## 2. QUY CHUẨN CODE TỪNG TẦNG

1. **`Catalog.Domain`**:
   - Entities hiện có: `Category`, `Product`, `ProductVariant`, `ProductVariantAttribute`, `ProductImage`, `ProductReview`, `ProductRatingStats`, `PurchaseRecord`, `AuditLog`. ❌ **Không có** `Inventory`.
   - ⚠️ Quy tắc "`Inventory.Available` phải có `Deduct(int)` / `Add(int)` đảm bảo `Available >= 0`" **chưa có gì để áp dụng**: tồn kho hiện là `InventoryDto` in-memory (`Catalog.Api/Mock/MockCatalogStore.cs:11-15`) với clamp `Math.Max(0, …)` ở tầng mock (`:205`). Khi tạo entity thật, phải đặt logic bất biến trong domain, không đặt trong controller.
   - ⚠️ Toàn repo có **0 lệnh `throw new`** — 3 exception của domain chưa từng được ném; `GlobalExceptionHandlerMiddleware` vì thế chỉ phòng thủ.

2. **`Catalog.Application`**:
   - ❌ **Không dùng MediatR**: `Catalog.Application.csproj` không có package MediatR, không có `Commands/`/`Queries/`. Cấu trúc thật là `Services/{Feature}/` gồm `I{Feature}Service.cs`, `{Feature}Service.cs`, `{Feature}Dtos.cs`. (Repo vừa merge PR mock nên **không còn `Features/`** — đừng tạo lại theo tên cũ.)
   - ✅ Quy tắc phân trang có thật: `ProductService.ListAsync` dùng `.AsNoTracking()`, `OrderByDescending(CreatedAt)`, `.Skip().Take()` (`Catalog.Application/Services/Product/ProductService.cs`). ⚠️ Việc chặn `pageSize ≤ 100` nằm ở **controller**, không phải service: `ProductsController.cs:29-40` trả `VAL_INVALID_PAGINATION` khi `pageSize` ngoài `1..100`; gọi thẳng service sẽ không được bảo vệ.

3. **`Catalog.Infrastructure`**:
   - ❌ Quy tắc "Unique Index trên `Product.Slug`" **không áp dụng được**: `Product` không có thuộc tính `Slug` (`Catalog.Domain/Entities/Product.cs:6-26`).
   - ✅ Index thật đang có: unique index trên `ProductVariant.SkuCode` (`Persistence/Configurations/ProductVariantConfiguration.cs:13`). Index trên `Product.CategoryId` / `Product.SellerId` **chưa được khai báo** (chỉ là FK mặc định của EF, `ProductConfiguration.cs`).
   - ⚠️ 9 cấu hình **không** khai báo `ToTable(...)` → tên bảng theo mặc định EF (PascalCase số nhiều), khớp migration 12 bảng.

---

## 3. TRẠNG THÁI TỪNG TẦNG (ĐỐI CHIẾU NHANH)

| Tầng | Kỳ vọng theo đặc tả | Thực tế | Trạng thái | Bằng chứng |
|---|---|---|---|---|
| Domain | Entity + VO `Money` + Domain Events + `InsufficientStockException` | 9 entity, 7 enum, 3 exception (0 throw), không VO, không event | ⚠️ Một phần | `Catalog.Domain/Entities/*.cs`, `Catalog.Domain/Exceptions/` |
| Application | CQRS + MediatR + Consumers | 2 service (4 method), 9 DbSet, không MediatR, 0 consumer | ❌ Chưa có CQRS | `Catalog.Application/DependencyInjection.cs:11-12` |
| Application được API sử dụng | Controller gọi service | 0 controller inject service; controller gọi `MockCatalogStore` | ❌ Chưa nối | `ProductsController.cs:18-25` |
| Infrastructure | DbContext + config + outbox | Đủ (9 config, 1 migration, outbox đăng ký) nhưng 0 publish/consume | ⚠️ Một phần | `DependencyInjection.cs:17-48` |
| Api | gRPC server + middleware | REST mock 13 action + middleware + Swagger | ⚠️ Khác đặc tả | `Program.cs:30-47,69` |
| Test | Test project cho từng tầng | 0 project test | ❌ Chưa có | `Catalog.sln` (4 project) |

---

## 4. QUY CHUẨN CẤU TRÚC CHUẨN HÓA (SERVICE BLUEPRINT)
1. **Giao tiếp đồng bộ (RESTful)**:
   - `Catalog.Api/Controllers/`: Thin Dispatcher Controller, chỉ nhận request, gọi Service Interface trong Application, trả về `IActionResult`. Tuyệt đối cấm logic nghiệp vụ, cấm map anonymous object, cấm khai báo class DTO trong file controller.
   - `Catalog.Application/Services/{Feature}/`: Mỗi feature (`Product`, `Category`...) chứa `I{Feature}Service.cs`, `{Feature}Service.cs`, và Request/Response DTOs dùng chung cho Controller.
2. **Giao tiếp bất đồng bộ (RabbitMQ MassTransit)**:
   - `Catalog.Application/Contracts/`: Chứa các Integration Events (ví dụ: `StockReservedIntegrationEvent`, `StockReleasedIntegrationEvent`).
   - `Catalog.Infrastructure/Consumers/`: Chứa các MassTransit Consumer triển khai `IConsumer<TEvent>`.
3. **Quản lý Dependency Injection**:
   - `Catalog.Application/DependencyInjection.cs`: Đăng ký toàn bộ Application Services.
   - `Catalog.Infrastructure/DependencyInjection.cs`: Đăng ký `AppDbContext`, MassTransit Bus, Consumers, và các dịch vụ hạ tầng.
4. **Validation**:
   - Input format validation đặt tại Request DTOs qua Data Annotations (`[Required]`, `[Range]`...).
   - Business validation đặt tại Application Services hoặc Domain Exceptions.

