# CATALOG SERVICE - POST /api/v1/internal/variants/check-status

> **Feature / Use Case:** Kiểm tra tính hợp lệ và khả năng mua hàng của danh sách biến thể theo lô [CAT-39] (Phục vụ Cart Service và Checkout Service trước khi chốt đơn)
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Nexus Team & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `POST`
- **Route Endpoint:** `/api/v1/internal/variants/check-status`
- **Quyền hạn (RBAC Policy):** `AllowAnonymous` / `InternalMicroservice` (Endpoint nội bộ bảo vệ bởi API Gateway network boundary hoặc Mutual TLS)
- **Quy tắc Sở hữu (Resource Ownership):** Không yêu cầu
- **Controller:** `Catalog.Api/Controllers/Internal/InternalVariantsController.cs`
- **Application Contract:** `Catalog.Application/Services/Product/IProductService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters & Query Parameters
- Không có tham số Route hoặc Query.

### 2.2 Request Body Record DTO
- **Vị trí file:** `Catalog.Application/Services/Product/ProductRequests.cs`
- **Tên C# Record:** `CheckVariantStatusRequest`
- **Khai báo C# Record:**
```csharp
public sealed record CheckVariantStatusRequest(
    List<Guid> VariantIds,
    string Purpose);
```
- **Bảng quy chuẩn Validation:**
| Tham số | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :---: | :--- | :--- |
| `variantIds` | `List<Guid>` | Có | Tối đa 100 UUIDs | Danh sách các biến thể cần kiểm tra trạng thái |
| `purpose` | `string` | Có | `cart`, `checkout`, `order` | Ngữ cảnh kiểm tra (xem giỏ hàng, thanh toán, đặt hàng) |

- **JSON Request Body mẫu (camelCase):**
```json
{
  "variantIds": [
    "33333333-3333-3333-3333-333333333333"
  ],
  "purpose": "checkout"
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
- **Vị trí file:** `Catalog.Application/Services/Product/ProductDtos.cs`
- **Tên C# Record:** `CheckVariantStatusResponse`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record VariantStatusItemDto(
    Guid VariantId,
    Guid ProductId,
    string ProductStatus,
    string? ProductStatusReason,
    string VariantStatus,
    string? VariantStatusReason,
    string SaleType,
    bool IsPurchasable,
    string Reason,
    decimal? Price,
    string Currency);

public sealed record CheckVariantStatusResponse(
    IReadOnlyList<VariantStatusItemDto> Items,
    IReadOnlyList<Guid> NotFoundIds);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "items": [
    {
      "variantId": "33333333-3333-3333-3333-333333333333",
      "productId": "77777777-7777-7777-7777-777777777777",
      "productStatus": "Active",
      "productStatusReason": null,
      "variantStatus": "Active",
      "variantStatusReason": null,
      "saleType": "Regular",
      "isPurchasable": true,
      "reason": "Available for purchase.",
      "price": 1250000,
      "currency": "VND"
    }
  ],
  "notFoundIds": []
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Variant IDs list cannot be null or empty.` | Danh sách ID rỗng |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Purpose is required.` | `purpose` bị rỗng |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi không mong muốn |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** `InternalVariantsController.CheckVariantStatusBatch` nhận `CheckVariantStatusRequest`.
2. **Bước 2:** Gọi `_productService.CheckVariantStatusBatchAsync(request, ct)`.
3. **Bước 3:** Lấy danh sách các biến thể từ cơ sở dữ liệu kèm theo thực thể `Product` cha (`.AsNoTracking()`).
4. **Bước 4:** Đánh giá khả năng mua (`IsPurchasable`):
   - `product.Status == ProductStatus.Active` VÀ `!product.IsDeleted`
   - VÀ `variant.IsActive == true` VÀ `!variant.IsDeleted`
   - VÀ nếu `SaleType == SaleType.Auction`: không cho phép mua trực tiếp qua Cart thông thường (chỉ chốt khi thắng đấu giá).
5. **Bước 5:** Tổng hợp lý do (`Reason`) nếu không thể mua (ví dụ: *"Product is inactive"*, *"Variant is locked"*, *"Auction items cannot be purchased directly"*).
6. **Bước 6:** Trả về Pure DTO `CheckVariantStatusResponse` với mã HTTP `200 OK`.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Hoàn tất đặc tả logic kiểm tra tính khả dụng mua hàng liên service.
- [x] **Application:** Triển khai phương thức `CheckVariantStatusBatchAsync` trong `ProductService.cs`.
- [x] **Api:** `InternalVariantsController.CheckVariantStatusBatch` trả Pure DTO.
- [x] **Verify:** `dotnet build Catalog.sln -c Release` thành công 100% với 0 lỗi.

