---
name: service-api-doc
description: Pre-code API documentation standard for CatalogService (port 8002). Mandates drafting a service-level technical API doc before coding, referencing existing domain entities, enums, DTOs, and error patterns for system-wide consistency. Governs pure DTO contracts, validation attributes, and RFC 9110 error matrices inside CatalogService.
---

# CatalogService API Pre-Code Documentation Skill

> **Scope:** CatalogService repository only (`src/Services/Catalog/`). 100% Inward-Only. Never reference the root workspace or sibling services.

This skill governs the **mandatory pre-code documentation lifecycle** within CatalogService. Before any C# code is written or modified for a REST endpoint, the developer or AI agent must create a technical API specification inside `docs/api/`.

---

## 1. Ranh giới Tài liệu CatalogService

* **Vị trí lưu trữ:** `docs/api/{Feature}/{HTTP_METHOD}_{ENDPOINT_SLUG}.md`
* **Thời điểm lập:** **BẮT BUỘC TRƯỚC KHI VIẾT CODE (Pre-Code)**.
* **Định dạng dữ liệu trả về:** **Pure DTO** hoặc `PagedData<T>` (tuyệt đối không bọc qua `ApiResponse<T>`).
* **Rào chắn Inward-Only:** Chỉ trỏ tới tài nguyên nội bộ của CatalogService (`Catalog.Domain`, `Catalog.Application`, `Catalog.Infrastructure`, `Catalog.Api`). Không tham chiếu tới `docs/api/` ở Root.

---

## 2. Quy trình 3 Bước Tra cứu Source of Truth Trước Khi Lập Doc

### Bước 1: Tra cứu Thực thể Domain (`Catalog.Domain/Entities/`)
- Mở `Catalog.Domain/Entities/` (ví dụ `Category.cs`, `Product.cs`, `ProductVariant.cs`).
- Xác định kiểu C#, thuộc tính nullable, giá cả (`decimal(18, 2)`), và các quan hệ thực thể.

### Bước 2: Tra cứu Bảng mã Enum (`Catalog.Domain/Enums/`)
- Mở `Catalog.Domain/Enums/` (ví dụ `ProductStatus.cs`).
- Ghi nhận số nguyên DB (`0`, `1`, `2`...) và chuỗi PascalCase API (`Draft`, `Active`, `Archived`).
- Điền bảng ánh xạ vào Mục 4.1 của doc.

### Bước 3: Tra cứu Mẫu DTO & Xử lý Lỗi Có Sẵn (`Catalog.Application/`)
- Rà soát các tính năng có sẵn tại `Catalog.Application/Services/` (ví dụ `Categories/`, `Products/`).
- Kế thừa quy ước đặt tên DTO: `{Action}Request`, `{Action}Response`.
- Kế thừa mã lỗi đã có (`PRODUCT_NOT_FOUND`, `CATEGORY_NOT_FOUND`, `VAL_INVALID_INPUT`).

---

## 3. Các Rào Chắn Bất Biến tại CatalogService

1. **Pure DTO:** Controller tại CatalogService trả về DTO thuần, cấm thêm `ResultFilter` hay bọc `ApiResponse<T>`.
2. **Plural Route:** `/api/v1/products`, `/api/v1/categories`, `/api/v1/variants`.
3. **Mã lỗi & Message:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (cấm tiền tố `ERR_`).
4. **Quyền sở hữu người bán (Seller Ownership):** Khi tạo/sửa sản phẩm, `sellerId` luôn đọc từ `HttpContext.GetUserId()`.
5. **Phân trang tại DB:** Toàn bộ danh sách sản phẩm/danh mục phân trang tại DB qua `Skip`/`Take` và `CountAsync`, trả về `PagedData<T>`.

---

## 4. Quy trình Thực hiện (Workflow)

1. Sao chép `docs/api/SERVICE_API_DOC_TEMPLATE.md` vào `docs/api/{Feature}/{HTTP_METHOD}_{ENDPOINT_SLUG}.md`.
2. Điền đầy đủ 5 phần của template dựa trên kết quả tra cứu Source of Truth.
3. Hoàn thiện mục 5.1 (Pre-Code Plan) mô tả các bước xử lý kỹ thuật.
4. Triển khai code C# tuân thủ 100% hợp đồng đã đặc tả trong doc.
5. Chạy `dotnet test Catalog.sln -c Release` (hoặc build `Catalog.sln`) đạt 100% pass, tích checklist và chuyển trạng thái doc sang `✅ VERIFIED`.
