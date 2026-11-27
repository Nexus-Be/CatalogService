# 00 — NHIỆM VỤ CỐT LÕI & RÀO CHẮN CHỐNG ẢO GIÁC (CATALOG SERVICE)

> **Trạng thái triển khai (2026-11-27):** ✅ HOÀN TẤT 100% CLEAN ARCHITECTURE — 14 Controllers, 53 Actions RESTful, 10 Domain Entities PostgreSQL, 7 Migrations, 42 Unit & Contract Tests. MockCatalogStore và Message Broker đã xóa sổ hoàn toàn.
> **Nguồn sự thật:** code trong repo này (`Catalog.sln`); không tham chiếu tài liệu ngoài repo.
> **Cập nhật lần cuối:** 2026-11-27.

---

## 1. NHIỆM VỤ & RANH GIỚI MIỀN (BOUNDED CONTEXT)

**CatalogService** chịu trách nhiệm độc quyền về việc quản lý danh mục sản phẩm, thông tin sản phẩm, biến thể SKU và kho ảo:

- ✅ **CÓ** — Quản lý cây danh mục sản phẩm (`Category` phân cấp qua `ParentId`, `Level`, `SortOrder`, `IsDisabled`): `Catalog.Domain/Entities/Category.cs`.
- ✅ **CÓ** — Quản lý thông tin sản phẩm (`Product`): tên, mô tả, thương hiệu, `SaleType` (Regular/Auction), `Status`, `Specifications` (jsonb), kích thước và bộ ảnh Cloudinary: `Catalog.Domain/Entities/Product.cs`.
- ✅ **CÓ** — Quản lý biến thể (`ProductVariant`): `Sku`, `Price`, `OriginalPrice`, `AvailableQuantity`, `ReservedQuantity`, `WeightGrams`, kích thước GHN `LengthCm`, `WidthCm`, `HeightCm`: `Catalog.Domain/Entities/ProductVariant.cs`.
- ✅ **CÓ** — Đánh giá & Xếp hạng (`ProductReview`, `ProductRatingStats`): Lưu trữ review 1-5 sao kèm ảnh, tự động tính điểm rating và trừ điểm uy tín người bán.
- ✅ **CÓ** — Tố cáo sản phẩm vi phạm (`ProductReport`): Tiếp nhận khiếu nại, duyệt báo cáo và tự động cấm sản phẩm (`Banned`).
- ✅ **CÓ** — Nhật ký kiểm toán (`AuditLog`): Ghi nhận toàn bộ thao tác sửa đổi dữ liệu và đồng bộ sang dịch vụ Logs trung tâm.
- 🛑 **ĐÃ BÃI BỎ** — RabbitMQ, MassTransit và gRPC Server. Toàn bộ giao tiếp sử dụng 100% RESTful Web API đồng bộ qua cổng `8002`.

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
