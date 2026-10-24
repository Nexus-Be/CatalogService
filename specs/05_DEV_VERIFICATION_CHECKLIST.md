# 05 — DANH SÁCH KIỂM CHỨNG & NGHIỆM THU (CATALOG SERVICE CHECKLIST)

> **Trạng thái triển khai (2026-10-24):** ⚠️ Một phần — `dotnet build` được kỳ vọng pass (4/4 project, chưa có cảnh báo chặn), nhưng **`dotnet test` không có bài test nào** (0 project test) nên bước "kiểm thử tự động" hiện **không kiểm chứng được gì**; service chạy **REST** chứ không phải gRPC.
> **Nguồn sự thật:** code trong repo này (bằng chứng `file:dòng`); không tham chiếu tài liệu ngoài repo.
> **Cập nhật lần cuối:** 2026-10-24.

---

## 1. LỆNH BIÊN DỊCH & CHẠY THỬ NGHIỆM

```bash
# 1. Biên dịch solution Catalog (workspace: src/Services/Catalog/)
dotnet build Catalog.sln -c Release

# 2. Khởi chạy service — REST, không phải gRPC.
#    Local: http://localhost:5272 (launchSettings.json:17) | Docker: http://localhost:8002 (Dockerfile:3-4)
dotnet run --project Catalog.Api
#    Swagger chỉ bật ở môi trường Development: http://localhost:5272/swagger (Program.cs:43-47)

# 3. Thử nghiệm trực tiếp (không cần qua service khác)
curl http://localhost:5272/health
curl "http://localhost:5272/api/v1/catalog/products?page=1&pageSize=10"
curl "http://localhost:5272/api/v1/catalog/products/seller/11111111-1111-1111-1111-000000000004"
curl "http://localhost:5272/api/v1/catalog/inventory/22222222-2222-2222-2222-000000000001"
```

- File `Catalog.Api/Catalog.Api.http` có sẵn các request mẫu, ⚠️ nhưng `@Host` đang là `http://localhost:5099` (`Catalog.Api.http:1`) — **sai**, phải sửa thành `5272` (local) hoặc `8002` (Docker).
- ❌ Không kiểm chứng qua ApiGateway bằng `gateway.runtime.json` như tài liệu cũ hướng dẫn: file đó thuộc repo khác (ngoài phạm vi repo này), và Catalog hiện **không có gRPC** để Gateway gọi theo mode `Real`.
- ❌ `dotnet test Catalog.sln -c Release` chạy **0 bài test** — `Catalog.sln` chỉ có 4 project (`Catalog.Api`, `Catalog.Application`, `Catalog.Domain`, `Catalog.Infrastructure`), không có project test. Build CI (`.github/workflows/ci-cd.yml`, 47 dòng) chỉ build/push Docker image, **không chạy `dotnet test`**.

---

## 2. CHECKLIST TRƯỚC KHI COMMIT CHO AI AGENTS

- [ ] **Biên dịch thành công 100%**: `dotnet build Catalog.sln -c Release` không lỗi. (Chưa có ngưỡng cảnh báo nào được cấu hình — không có `TreatWarningsAsErrors`.)
- [ ] **Không vi phạm rào chắn**: Không dùng Repository Pattern bọc ngoài EF Core (hiện đúng: `Catalog.Application/Interfaces/ICatalogDbContext.cs`).
- [ ] **Port & Database**: Docker lắng nghe `8002` (`Dockerfile:4`), local `5272` (`launchSettings.json:17`); DB `category_db` port `5434` (`docker-compose.yml:19`).
- [ ] **Kiểm tra Concurrency**: ❌ **chưa có gì để kiểm** — không `RowVersion`/`xmin`/`IsConcurrencyToken`; chống overselling hiện chỉ dựa vào `lock (_lock)` của mock in-memory (`MockCatalogStore.cs:24-27`). Khi có entity tồn kho thật, mục này mới có hiệu lực.
- [ ] **Không để lộ bí mật**: ⚠️ `Catalog.Api/appsettings.json:10,16` đang chứa mật khẩu PostgreSQL và RabbitMQ plaintext, dù `.gitignore` có chặn `appsettings*.json` (file vẫn đang được track).
- [ ] **Không ký DLL tự động ngoài ý muốn**: ⚠️ `Directory.Build.targets:2-4` tự ký **mọi** `*.dll` trong `TargetDir` bằng cert `CatalogDev` trên Windows, bỏ qua lỗi.
- [ ] **Git Status**: Không commit file rác (`bin/`, `obj/`, `.idea/`, `.env`).

---

## 3. KIỂM CHỨNG THEO HÀNH VI (SMOKE TEST THỦ CÔNG)

| # | Hành vi cần đúng | Cách kiểm | Kỳ vọng hiện tại | Trạng thái | Bằng chứng |
|---|---|---|---|---|---|
| 1 | Health | `GET /health` | `{"status":"Healthy","service":"Catalog.Api"}` | ✅ Có | `Program.cs:66` |
| 2 | Phân trang hợp lệ | `GET /products?page=1&pageSize=10` | 200 + `PagedData` + `pagination` | ✅ Có | `ProductsController.cs:27-45` |
| 3 | Phân trang sai | `GET /products?page=0` | 400 `VAL_INVALID_PAGINATION` | ✅ Có | `ProductsController.cs:32-40` |
| 4 | Product không tồn tại | `GET /products/{guid lạ}` | 404 `PRODUCT_NOT_FOUND` | ✅ Có | `ProductsController.cs:60-69` |
| 5 | Category không tồn tại | `GET /categories/{guid lạ}` | 404 `CATEGORY_NOT_FOUND` | ✅ Có | `CategoriesController.cs:40-49` |
| 6 | Tạo product thiếu field | `POST /products` body rỗng `Name` | 400 `VAL_INVALID_INPUT` | ✅ Có | `ProductsController.cs:80-87` |
| 7 | Ghi dữ liệu tồn tại sau restart | `POST /products` rồi restart | ❌ Mất dữ liệu (singleton in-memory) | ❌ Chưa đạt | `Program.cs:26`, `MockCatalogStore.cs:24-27` |
| 8 | Tồn kho không bịa | `GET /inventory/{guid lạ}` | ❌ Trả `available: 100` cho product không tồn tại | ❌ Chưa đạt | `MockCatalogStore.cs:188-195` |
| 9 | Xoá category còn product con | `DELETE /categories/{id}` | ❌ Xoá luôn, không chặn (không 412) | ❌ Chưa đạt | `MockCatalogStore.cs:124-132` |
| 10 | Phân quyền seller | `PUT /products/{id}` không token | ❌ 200 — không auth, không kiểm tra chủ sở hữu | ❌ Chưa đạt | 0 `[Authorize]` toàn repo |
| 11 | Thống nhất phong bì lỗi | so sánh 400 của controller và 500 của middleware | ❌ 2 schema khác nhau (`ApiErrorResponse` vs anonymous object) | ❌ Chưa đạt | `ApiResponse.cs:53-63` vs `GlobalExceptionHandlerMiddleware.cs:77-85` |
| 12 | Đọc/ghi PostgreSQL | so `GET /products` với bảng `Products` | ❌ Không chạm DB — controller dùng mock | ❌ Chưa đạt | `ProductsController.cs:18-25` |
