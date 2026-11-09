# CATALOG SERVICE - GET /api/v1/internal/variants/{id}

> **Feature / Use Case:** Truy vấn thông tin chi tiết một biến thể SKU nội bộ liên service [CAT-21] (Dành cho Cart, Checkout, Order và Fulfillment kiểm tra tính khả dụng)
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-06
> **Tác giả:** Nexus Team & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/api/v1/internal/variants/{id}`
- **Alias Endpoint:** `/api/v1/internal/catalog/skus/{id}` (Hỗ trợ tương thích ngược cho các service gọi theo chuẩn danh pháp cũ)
- **Quyền hạn (RBAC Policy):** Inter-Service REST call (Không yêu cầu đăng nhập người dùng cuối).
- **Quy tắc Sở hữu (Resource Ownership):** Dành cho giao tiếp nội bộ giữa các microservices, trả về đầy đủ snapshot của biến thể mục tiêu.
- **Controller:** `Catalog.Api/Controllers/Internal/InternalVariantsController.cs`
- **Application Contract:** `Catalog.Application/Services/Product/IProductService.cs`
- **Tương tác Liên Service:** Cung cấp thông tin biến thể cho Cart Service (tính toán đơn giá, kiểm tra khả dụng mua `isPurchasable`), Order Service (chụp snapshot đơn hàng) và Fulfillment Service.

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters & Query Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `id` | Route | `Guid` | Có | UUIDv4 format | Mã định danh biến thể (`variantId` / `skuId`) |

### 2.2 Request Body Record DTO
- Không có Request Body (HTTP GET).

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** Trả về Pure DTO `VariantDetailDto` với mã HTTP `200 OK`.

- **Vị trí file:** `Catalog.Application/Services/Product/ProductDtos.cs`
- **Tên C# Record:** `VariantDetailDto`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record VariantDetailDto(
    Guid Id,
    Guid ProductId,
    Guid SellerId,
    string ProductName,
    string SkuCode,
    string Name,
    decimal? Price,
    string Currency,
    bool IsActive,
    bool HasOrder,
    string Status,
    string? StatusReason,
    bool IsPurchasable,
    IReadOnlyList<ProductVariantAttributeDto> Attributes,
    string? PrimaryImageUrl = null,
    IReadOnlyList<ProductImageDto>? Images = null);
```
- **JSON Response Body mẫu (camelCase):**
```json
{
  "id": "fafdc926-69b6-4b61-810d-3c98ac1c520c",
  "productId": "2bcb5321-8ef7-43d2-99ed-4f5da606caab",
  "sellerId": "cccccccc-3333-3333-3333-333333333333",
  "productName": "Bàn Phím Cơ Không Dây Nexus RGB V2",
  "skuCode": "KB-NEXUS-01-WHITE",
  "name": "Màu Trắng - Gateron Yellow",
  "price": 1250000.00,
  "currency": "VND",
  "isActive": true,
  "hasOrder": false,
  "status": "Active",
  "statusReason": null,
  "isPurchasable": true,
  "attributes": [
    {
      "attrName": "Màu sắc",
      "attrValue": "Trắng"
    },
    {
      "attrName": "Switch",
      "attrValue": "Yellow"
    }
  ],
  "primaryImageUrl": "https://res.cloudinary.com/u6yxkkop/image/upload/v1793813165/nexus_catalog/products/kb-white.jpg",
  "images": [
    {
      "id": "11111111-2222-3333-4444-555555555555",
      "productId": "2bcb5321-8ef7-43d2-99ed-4f5da606caab",
      "variantId": "fafdc926-69b6-4b61-810d-3c98ac1c520c",
      "url": "https://res.cloudinary.com/u6yxkkop/image/upload/v1793813165/nexus_catalog/products/kb-white.jpg",
      "altText": "Bàn phím cơ màu trắng",
      "isActive": true,
      "sortOrder": 0
    }
  ]
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `404 Not Found` | `VARIANT_NOT_FOUND` | `Product variant not found with the specified identifier.` | `id` không tồn tại hoặc đã bị xóa mềm trong DB |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi không mong muốn trong quá trình đọc cơ sở dữ liệu |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** `InternalVariantsController.GetVariantById` nhận `id` từ route.
2. **Bước 2:** Gọi `_productService.GetVariantByIdAsync(id, ct)`.
3. **Bước 3:** `ProductService` truy vấn `ProductVariants` kèm `Product`, `Attributes`, `Images`, kiểm tra cờ `!IsDeleted`.
4. **Bước 4:** Tính toán trường `IsPurchasable` (`!IsDeleted && Status == Active && Product.Status == Active && Price > 0`).
5. **Bước 5:** Trả về `VariantDetailDto` với HTTP `200 OK`.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đặc tả đầy đủ route chuẩn và alias route tương thích ngược.
- [x] **Application:** `ProductService.GetVariantByIdAsync` trả về `VariantDetailDto` kèm logic tính `isPurchasable`.
- [x] **Api:** `InternalVariantsController` hỗ trợ cả 2 route `/api/v1/internal/variants/{id}` và `/api/v1/internal/catalog/skus/{id}`.
- [x] **Verify:** `dotnet build Catalog.sln -c Release` thành công 100% với 0 lỗi.

