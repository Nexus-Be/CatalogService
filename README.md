# CATALOG SERVICE — ARCHITECTURE & FOLDER STRUCTURE GUIDE
## (CẨM NANG CẤU TRÚC THƯ MỤC & PHÁT TRIỂN DÀNH CHO DEVELOPER VÀ AI AGENT)

> **Trạng thái triển khai (2026-10-24):** ⚠️ Một phần — **0/13 rpc gRPC** (repo không có file `.proto`, không có package `Grpc.*`), **13/13 action REST** chạy được nhưng đọc/ghi **mock in-memory** (`MockCatalogStore`), **0 test**.
> **Nguồn sự thật:** code trong repo này; mọi tuyên bố đều kèm `file.cs:dòng`. Không có liên kết nào trỏ ra ngoài repo (inward-only).
> **Cập nhật lần cuối:** 2026-10-24.

> **Thông tin dịch vụ**:
> - **Service Port**: `8002` khi chạy Docker (`Dockerfile:3-4`); chạy local bằng `dotnet run` là `5272` (`Catalog.Api/Properties/launchSettings.json:17`), profile https là `7232` (`:27`). File `Catalog.Api/Catalog.Api.http:1` còn trỏ `5099` — ❌ sai, không dùng.
> - **Database**: PostgreSQL (`category_db`), port máy host `5434` (`docker-compose.yml:19`, `Catalog.Api/appsettings.json:10`).
> - **Nhiệm vụ chính**: quản lý danh mục, sản phẩm, biến thể (variants), ảnh, đánh giá; **kho ảo hiện chỉ là mock REST**, không có entity/bảng tồn kho (`Catalog.Domain/Entities/` không có `Inventory`).
> - **Bề mặt giao tiếp thật**: REST `/api/v1/catalog/{products,categories,inventory}` (`Catalog.Api/Controllers/`). ❌ Không có gRPC.

---

## 1. SƠ ĐỒ PHÂN TẦNG VÀ CẤU TRÚC THƯ MỤC CHUẨN

```
src/Services/Catalog/
│
├── Catalog.Domain/                                    # 1. TẦNG DOMAIN (POCO 100%, ZERO DEPENDENCY)
│   ├── Base/                                          # BaseEntity, AuditableEntity, IAuditable, ISoftDelete
│   ├── Entities/                                      # 9 entity: Product, Category, ProductVariant,
│   │                                                  #   ProductVariantAttribute, ProductImage, ProductReview,
│   │                                                  #   ProductRatingStats, PurchaseRecord, AuditLog
│   ├── Enums/                                         # 7 enum: ProductStatus, SaleType, ReviewStatus,
│   │                                                  #   ProductInactiveReason, VariantStatus,
│   │                                                  #   VariantInactiveReason, AuditAction
│   └── Exceptions/                                    # DomainException, EntityNotFoundException,
│                                                      #   BusinessRuleValidationException
│
├── Catalog.Application/                               # 2. TẦNG APPLICATION (USE CASES & BUSINESS LOGIC)
│   ├── Interfaces/
│   │   └── ICatalogDbContext.cs                       # 9 DbSet + SaveChangesAsync
│   ├── Services/                                      # ĐÚNG: dịch vụ theo tính năng (không có Features/)
│   │   ├── Product/                                   # IProductService, ProductService, ProductDtos
│   │   └── Category/                                  # ICategoryService, CategoryService, CategoryDtos
│   └── DependencyInjection.cs                         # AddApplicationServices()
│
├── Catalog.Infrastructure/                            # 3. TẦNG INFRASTRUCTURE (CÔNG NGHỆ & HẠ TẦNG CỤ THỂ)
│   ├── Persistence/
│   │   ├── AppDbContext.cs                            # Triển khai ICatalogDbContext (+ CatalogDbContext kế thừa, chưa dùng)
│   │   ├── Configurations/                            # 9 Fluent API Configuration
│   │   ├── Migrations/                                # 1 migration: 20261024103127_InitialCatalogSchema
│   │   └── Seeds/CatalogSeedData.cs                   # HasData: 5 category, 5 product, 5 variant, 5 image, 5 rating
│   └── DependencyInjection.cs                         # AddInfrastructureServices(configuration)
│
└── Catalog.Api/                                       # 4. TẦNG API / HOST (ENTRY POINT & MIDDLEWARE)
    ├── Controllers/                                   # 3 controller REST (13 action)
    │   ├── ProductsController.cs                      # 6 action
    │   ├── CategoriesController.cs                     # 5 action
    │   └── InventoryController.cs                     # 2 action
    ├── Mock/MockCatalogStore.cs                       # Store in-memory 211 dòng, nạp Seeds/*.json trong ctor
    ├── Middleware/GlobalExceptionHandlerMiddleware.cs # Bắt ngoại lệ, trả ProblemDetails (anonymous object)
    ├── Responses/                                     # ApiResponse, ApiErrorResponse, ApiResponseFactory
    ├── Seeds/                                         # products.json (3), categories.json (3) — nguồn seed của mock
    ├── Properties/launchSettings.json                 # http 5272, https 7232, IIS 11532
    ├── appsettings.json                               # ConnectionStrings, RabbitMq, Otel (Otel không được dùng)
    └── Program.cs                                     # Kestrel HTTP/1+HTTP/2, DI, MapControllers, /health
```

**❌ Đường dẫn KHÔNG tồn tại** (các tài liệu cũ nhắc tới): `Catalog.Api/Services/CatalogGrpcService.cs`, `Catalog.Api/Contracts/shop.proto`, `Catalog.Application/Features/`, `Catalog.Application/Common/`, thư mục `.tools/dotnet-ef`.

---

## 2. NGUYÊN TẮC PHÂN CHIA TRÁCH NHIỆM

1. **`Catalog.Domain`**: chứa 9 entity nghiệp vụ sản phẩm, danh mục, biến thể, ảnh, đánh giá, lịch sử mua. Không import thư viện ngoài, không reference EF Core (`Catalog.Domain/Catalog.Domain.csproj` không có `PackageReference`).
2. **`Catalog.Application`**: chứa ca sử dụng. Mỗi tính năng nằm trong `Services/{Feature}/` gồm DTOs, Interface và Implementation (⚠️ mới có `Product` và `Category`, chưa có `Features/`). Truy cập CSDL qua `ICatalogDbContext`; **0 nơi inject `IProductService`/`ICategoryService`** — 2 service này chỉ được đăng ký DI (`Catalog.Application/DependencyInjection.cs:11-12`) và không được controller nào gọi (`Catalog.Api/Controllers/*.cs` chỉ inject `MockCatalogStore` + `IApiResponseFactory`).
3. **`Catalog.Infrastructure`**: triển khai `AppDbContext` kế thừa `ICatalogDbContext` (`Catalog.Infrastructure/Persistence/AppDbContext.cs:8`). Không dùng Generic Repository bọc ngoài EF Core (ADR 05). Có đăng ký MassTransit + EntityFramework Outbox (`DependencyInjection.cs:23-48`) nhưng **0 consumer, 0 publish** (`grep IConsumer|IPublishEndpoint` = 0).
4. **`Catalog.Api`**: cổng tiếp nhận mỏng. Trang bị `GlobalExceptionHandlerMiddleware` (`Program.cs:41`) để bắt ngoại lệ, ⚠️ nhưng phong bì lỗi của middleware **khác** phong bì của controller (xem mục 7).

---

## 3. QUY TRÌNH THÊM TÍNH NĂNG MỚI (DÀNH CHO DEV & AI AGENT)

1. **Bước 1**: Tạo Entity & Enum trong `Catalog.Domain/Entities/`.
2. **Bước 2**: Tạo Configuration trong `Catalog.Infrastructure/Persistence/Configurations/` và khai báo `DbSet<T>` trong `ICatalogDbContext` & `AppDbContext`.
3. **Bước 3**: Tạo thư mục tính năng trong `Catalog.Application/Services/{Feature}/` gồm `I{Feature}Service.cs`, `{Feature}Service.cs`, và `{Feature}Dtos.cs`.
4. **Bước 4**: Đăng ký Service vào `Catalog.Application/DependencyInjection.cs`.
5. **Bước 5**: Gọi Service trong `Catalog.Api/Controllers/{Feature}Controller.cs` và thêm action REST. ❌ Không còn bước "gọi trong `CatalogGrpcService.cs`" — file đó không tồn tại. ⚠️ Hiện tại nếu chỉ làm bước 4 thì service sẽ là dead code: controller phải inject service đó thay vì `MockCatalogStore`.

---

## 4. QUY CHUẨN THÔNG BÁO LỖI (MANDATORY ERROR RULES)

1. **100% English**: tuyệt đối không dùng tiếng Việt trong chuỗi lỗi trả cho client.
2. **Sentence case**: viết hoa duy nhất chữ cái đầu (`"Product not found."` — đúng như `ProductsController.cs:65`).
3. **Error Code SCREAMING_SNAKE_CASE**: mã đang dùng trong code: `VAL_INVALID_PAGINATION` (`ProductsController.cs:39`), `PRODUCT_NOT_FOUND` (`:68`), `CATEGORY_NOT_FOUND` (`CategoriesController.cs:48`), `VAL_INVALID_INPUT` (`ProductsController.cs:86`), và ở middleware: `NOT_FOUND`, `BUSINESS_RULE_VIOLATION`, `DOMAIN_ERROR`, `VAL_INVALID_ARGUMENT`, `INTERNAL_SERVER_ERROR` (`GlobalExceptionHandlerMiddleware.cs:36-63`).

⚠️ Hai phong bì lỗi đang song song tồn tại: `ApiErrorResponse` (`Responses/ApiResponse.cs:53`, content-type `application/json`) và object anonymous trong middleware (`GlobalExceptionHandlerMiddleware.cs:77-85`, content-type `application/problem+json`). Khi bổ sung endpoint mới, phải chọn một — hiện code chưa thống nhất.

---

## 5. CÁCH CHẠY VÀ KIỂM THỬ ĐỘC LẬP SERVICE

```bash
# 1. Bật database của Catalog Service (Port 5434):
cd src/Services/Catalog
docker compose up -d catalog-db

# 2. EF Core CLI: dùng dotnet-ef toàn cục (❌ KHÔNG có ./.tools/dotnet-ef trong repo)
dotnet tool install --global dotnet-ef --version 8.0.10   # nếu chưa có
dotnet ef migrations add <TenMigration> --project Catalog.Infrastructure --startup-project Catalog.Api

# 3. Chạy service (local: http://localhost:5272, swagger tại /swagger):
dotnet run --project Catalog.Api
```

- Migration **đã có sẵn** `20261024103127_InitialCatalogSchema`; `Program.cs:50-64` tự gọi `db.Database.Migrate()` khi khởi động và chỉ log warning nếu DB không sẵn sàng.
- Kiểm thử API bằng `Catalog.Api/Catalog.Api.http` (⚠️ phải sửa `@Host` từ `5099` thành `5272` hoặc `8002`).
- ❌ Không có project test: `Catalog.sln` chỉ có 4 project (`Api`, `Application`, `Domain`, `Infrastructure`); `dotnet test Catalog.sln` không chạy bài test nào.

---

## 6. BỀ MẶT REST HIỆN CÓ (13 ACTION, TẤT CẢ LÀ MOCK)

| # | Method & Route | Action | Nguồn dữ liệu | Trạng thái | Bằng chứng |
|---|---|---|---|---|---|
| 1 | `GET /api/v1/catalog/products` | `ListProducts` | `MockCatalogStore.GetProducts` | ⚠️ Mock (không đọc DB) | `ProductsController.cs:27-45` |
| 2 | `GET /api/v1/catalog/products/seller/{sellerId}` | `ListProductsBySeller` | `MockCatalogStore.GetProducts(sellerId:)` | ⚠️ Mock | `ProductsController.cs:47-53` |
| 3 | `GET /api/v1/catalog/products/{id}` | `GetProduct` | `MockCatalogStore.GetProductById` | ⚠️ Mock | `ProductsController.cs:55-73` |
| 4 | `POST /api/v1/catalog/products` | `CreateProduct` | `MockCatalogStore.CreateProduct` | ⚠️ Mock (mất khi restart) | `ProductsController.cs:75-92` |
| 5 | `PUT /api/v1/catalog/products/{id}` | `UpdateProduct` | `MockCatalogStore.UpdateProduct` | ⚠️ Mock | `ProductsController.cs:94-112` |
| 6 | `DELETE /api/v1/catalog/products/{id}` | `DeleteProduct` | `MockCatalogStore.DeleteProduct` | ⚠️ Mock | `ProductsController.cs:114-132` |
| 7 | `GET /api/v1/catalog/categories` | `ListCategories` | `MockCatalogStore.GetCategories` | ⚠️ Mock | `CategoriesController.cs:27-33` |
| 8 | `GET /api/v1/catalog/categories/{id}` | `GetCategory` | `MockCatalogStore.GetCategoryById` | ⚠️ Mock | `CategoriesController.cs:35-53` |
| 9 | `POST /api/v1/catalog/categories` | `CreateCategory` | `MockCatalogStore.CreateCategory` | ⚠️ Mock | `CategoriesController.cs:55-72` |
| 10 | `PUT /api/v1/catalog/categories/{id}` | `UpdateCategory` | `MockCatalogStore.UpdateCategory` | ⚠️ Mock | `CategoriesController.cs:74-92` |
| 11 | `DELETE /api/v1/catalog/categories/{id}` | `DeleteCategory` | `MockCatalogStore.DeleteCategory` | ⚠️ Mock | `CategoriesController.cs:94-112` |
| 12 | `GET /api/v1/catalog/inventory/{productId}` | `GetInventory` | `MockCatalogStore.GetInventory` | ⚠️ Mock — **bịa `Available=100`** cho product không tồn tại | `InventoryController.cs:25-31`, `MockCatalogStore.cs:193` |
| 13 | `POST /api/v1/catalog/inventory/{productId}/adjust` | `AdjustInventory` | `MockCatalogStore.AdjustInventory` | ⚠️ Mock — product lạ cũng được tạo mới với `Available=100` | `InventoryController.cs:33-39`, `MockCatalogStore.cs:203` |

Ngoài ra: `GET /health` (`Program.cs:66`).

---

## 7. NHỮNG ĐIỀU CHƯA CÓ & RỦI RO ĐÃ BIẾT (đọc trước khi sửa code)

| ID | Vấn đề | Trạng thái | Bằng chứng |
|---|---|---|---|
| R-01 | Không có gRPC: 0 file `.proto`, 0 package `Grpc.*`, 0 class kế thừa `*Base` | ❌ Chưa có (đặc tả mục tiêu 13 rpc) | không tìm thấy trong repo; `specs/02_API_GRPC_CONTRACTS.md` là đặc tả |
| R-02 | Không có xác thực/phân quyền: 0 `[Authorize]`, 0 package JWT; mọi POST/PUT/DELETE mở công khai | ❌ Chưa có | `grep Authorize` = 0; `Catalog.Api/Catalog.Api.csproj:15-19` |
| R-03 | Hai phong bì lỗi + hai nguồn `TraceId` (một ưu tiên `HttpContext.TraceIdentifier`, một ưu tiên `Activity.Current?.Id`) | ⚠️ Không nhất quán | `ApiResponseFactory.cs:18-23` vs `GlobalExceptionHandlerMiddleware.cs:34` |
| R-04 | `MockCatalogStore` là singleton in-memory; dữ liệu mất khi restart; nuốt lỗi khi đọc seed bằng `catch { }` rỗng | ⚠️ Mock | `MockCatalogStore.cs:60-63`, `Program.cs:26` |
| R-05 | Hai hệ seed id khác nhau: mock JSON (`aaaaaaaa…`, `2222…`) vs EF `HasData` (`10000000…`, `11000000…`) | ⚠️ Trùng lặp nguồn | `Catalog.Api/Seeds/*.json` vs `Catalog.Infrastructure/Persistence/Seeds/CatalogSeedData.cs:12-19,85-90` |
| R-06 | `Directory.Build.targets` tự ký **mọi** `*.dll` trong `TargetDir` bằng cert `CatalogDev` (Windows), bỏ qua lỗi | ⚠️ Rủi ro chuỗi cung ứng | `Directory.Build.targets:2-4` |
| R-07 | `appsettings.json` chứa mật khẩu thật; `Otel` config và `OTEL_EXPORTER_OTLP_ENDPOINT` không được code nào đọc (0 package OpenTelemetry) | ⚠️ Cấu hình chết/lộ secret | `appsettings.json:10,16,19`, `docker-compose.yml:47` |
| R-08 | `CatalogDbContext` (`AppDbContext.cs:37-41`) không được đăng ký DI — class chết | ⚠️ Dead code | `Catalog.Infrastructure/DependencyInjection.cs:17-20` |
| R-09 | `ProductRatingStats` chỉ có seed, không có luồng nào ghi | ❌ Chưa có luồng cập nhật | `CatalogSeedData.cs:329-386`; 0 lệnh ghi khác |
| R-10 | Tài liệu cũ trỏ `./.tools/dotnet-ef` (không tồn tại, lại trỏ ra ngoài repo) | ✅ Đã sửa trong file này | mục 5 |
