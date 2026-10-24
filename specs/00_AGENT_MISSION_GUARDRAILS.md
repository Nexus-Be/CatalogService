# 00 — NHIỆM VỤ CỐT LÕI & RÀO CHẮN CHỐNG ẢO GIÁC (CATALOG SERVICE)

> **Trạng thái triển khai (2026-10-24):** ⚠️ Một phần — **0/13 rpc gRPC** (không có `.proto`), **13 action REST mock in-memory** đã chạy, **0 event**, **0 test**.
> **Nguồn sự thật:** code trong repo này (bằng chứng `file.cs:dòng`); không tham chiếu tài liệu ngoài repo.
> **Cập nhật lần cuối:** 2026-10-24.

---

## 1. NHIỆM VỤ & RANH GIỚI MIỀN (BOUNDED CONTEXT)

**CatalogService** chịu trách nhiệm độc quyền về việc quản lý danh mục sản phẩm và thông tin sản phẩm; phần kho ảo hiện **chỉ được mô phỏng ở tầng REST**, chưa có mô hình dữ liệu.

- ✅ **CÓ** — Quản lý cây danh mục sản phẩm (`Category` phân cấp qua `ParentId`, `Level`, `SortOrder`, `IsDisabled`) trong domain + REST mock: `Catalog.Domain/Entities/Category.cs:5-18`, `Catalog.Api/Controllers/CategoriesController.cs:29`.
- ✅ **CÓ** — Quản lý thông tin sản phẩm (`Product`): tên, mô tả, thương hiệu, `SaleType`, `Status`, `Specifications` (jsonb). ⚠️ **Không có** `Price`, `Currency`, `Slug` trên `Product` — giá nằm ở `ProductVariant.Price`/`Currency`: `Catalog.Domain/Entities/Product.cs:6-26`, `Catalog.Domain/Entities/ProductVariant.cs:11-12`.
- ⚠️ **MỘT PHẦN** — Kho ảo: **không có entity/bảng `Inventory`** trong domain hay DB. Chỉ có `InventoryDto` in-memory do `MockCatalogStore` giữ (`Catalog.Api/Mock/MockCatalogStore.cs:11-15, 188-210`). `GetInventory` trả `Available=100` cho cả product không tồn tại (`MockCatalogStore.cs:193`) — đây là dữ liệu bịa, không phải nghiệp vụ.
- ❌ **CHƯA CÓ** (đặc tả mục tiêu) — Tham gia **Checkout Saga**: lắng nghe event từ `CommerceService` để giữ/nhả kho. Trong repo **0 consumer, 0 publish** (`grep IConsumer|IPublishEndpoint` = 0); MassTransit chỉ được đăng ký hạ tầng (`Catalog.Infrastructure/DependencyInjection.cs:23-48`).
- ❌ **CHƯA CÓ** (đặc tả mục tiêu) — gRPC Server tại cổng `8002` cho ApiGateway: repo **không có file `.proto`, không có package `Grpc.*`, không có class kế thừa `*Base`**. Bề mặt thật là REST mock (`Catalog.Api/Controllers/`).

---

## 2. NHỮNG ĐIỀU CẤM KỴ ĐỐI VỚI AI AGENTS (ANTI-HALLUCINATION GUARDRAILS)

1. **TUYỆT ĐỐI KHÔNG DÙNG REPOSITORY PATTERN BỌC NGOÀI EF CORE**:
   - Tuân thủ nguyên tắc kiến trúc hệ thống: Không sử dụng Repository Pattern bọc ngoài EF Core.
   - Sử dụng trực tiếp `AppDbContext`/`ICatalogDbContext` và `DbSet<T>` trong tầng Application (đúng như `Catalog.Application/Services/Product/ProductService.cs:13-18`).
   - Không được tạo `IProductRepository`, `ICategoryRepository`, `UnitOfWork`.

2. **KHÔNG NHẦM LẪN GIỮA KHO ẢO (CATALOG) VÀ KHO VẬT LÝ (FULFILLMENT)**:
   - `CatalogService` chỉ được quản lý số lượng tồn kho ảo có sẵn để bán (`Available`).
   - Kho bãi vật lý thực tế (kích thước, trọng lượng kiện hàng, vị trí giá kệ, mã tracking) thuộc về `FulfillmentService`; không đưa logic tính phí ship hay theo dõi vận đơn vào CatalogService.
   - ⚠️ Lưu ý hiện trạng: entity kho (`InventoryItem`, `Warehouse`, `InventoryLedger`, `InventoryReservation`) **không có trong repo này**, và `Catalog.Domain` cũng không có `Inventory` — đừng "tưởng tượng" là đã có.

3. **TUYỆT ĐỐI KHÔNG THAY ĐỔI CỔNG 8002 VÀ DATABASE 5434**:
   - Port đặc tả (Docker): `8002` — `Dockerfile:3-4`.
   - Port chạy local thật: `5272` (http) / `7232` (https) — `Catalog.Api/Properties/launchSettings.json:17,27`. (`Catalog.Api/Catalog.Api.http:1` đang trỏ `5099` ❌ sai.)
   - Chuỗi kết nối DB trỏ tới `localhost:5434/category_db` (`Catalog.Api/appsettings.json:10`; mật khẩu mẫu hoá trong `appsettings.example.json:10`).

4. **TUYỆT ĐỐI KHÔNG TỰ Ý SỬA PROTOBUF MÀ KHÔNG ĐỒNG BỘ GATEWAY**:
   - ⚠️ Hiện **không tồn tại** `Contracts/shop.proto` trong repo này và cũng không có package Grpc. Hợp đồng `CatalogApi` (13 rpc) trong `specs/02_API_GRPC_CONTRACTS.md` là **đặc tả mục tiêu, chưa triển khai**.
   - Khi nào bổ sung Protobuf, phải đồng bộ với Gateway trước; hiện tại mọi giao tiếp khả dụng là REST mock dưới `/api/v1/catalog/*`.

---

## 3. TRẠNG THÁI TRIỂN KHAI THỰC TẾ (ĐỐI CHIẾU NHANH)

| Hạng mục | Trạng thái | Bằng chứng |
|---|---|---|
| REST API 13 action (`products`, `categories`, `inventory`) | ✅ Có (mock in-memory) | `Catalog.Api/Controllers/*.cs`, `Program.cs:69` |
| gRPC 13 rpc | ❌ Chưa có | không có `.proto`/`Grpc.*` package |
| EF Core + PostgreSQL (9 bảng + 3 bảng outbox) | ✅ Có | `AppDbContext.cs:14-22`, migration `20261024103127_InitialCatalogSchema` |
| REST đọc/ghi PostgreSQL | ❌ Chưa có (controller dùng mock) | `ProductsController.cs:18-25` |
| RabbitMQ consumer/publisher | ❌ Chưa có (chỉ đăng ký hạ tầng) | `Catalog.Infrastructure/DependencyInjection.cs:23-48` |
| Xác thực/phân quyền | ❌ Chưa có (0 `[Authorize]`, 0 package JWT) | `Catalog.Api/Catalog.Api.csproj:15-19` |
| Test | ❌ Chưa có (0 project test) | `Catalog.sln` chỉ có 4 project |
| Health check | ✅ Có | `Program.cs:66` |
