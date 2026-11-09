# CATALOG SERVICE - POST /api/v1/internal/variants

> **Feature / Use Case:** Truy vấn thông tin hàng loạt biến thể (Batch Get Variants) phục vụ Cart Service và Order Service [CAT-24]
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Nexus Team & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `POST`
- **Route Endpoint:** `/api/v1/internal/variants`
- **Alias Endpoint:** `/api/v1/internal/catalog/skus:batch-get`
- **Quyền hạn (RBAC Policy):** `AllowAnonymous` / `InternalMicroservice` (Endpoint nội bộ bảo vệ bởi API Gateway network boundary hoặc Mutual TLS)
- **Quy tắc Sở hữu (Resource Ownership):** Không yêu cầu (Cho phép đọc thông tin nhiều biến thể thuộc nhiều người bán khác nhau để tính toán giỏ hàng và tạo đơn)
- **Controller:** `Catalog.Api/Controllers/Internal/InternalVariantsController.cs`
- **Application Contract:** `Catalog.Application/Services/Product/IProductService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters & Query Parameters
- Không có tham số Route hoặc Query.

### 2.2 Request Body Record DTO
- **Vị trí file:** `Catalog.Application/Services/Product/ProductRequests.cs`
- **Tên C# Record:** `BatchGetVariantsRequest`
- **Khai báo C# Record:**
```csharp
public sealed record BatchGetVariantsRequest(List<Guid> VariantIds);
```
- **Bảng quy chuẩn Validation:**
| Tham số | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :---: | :--- | :--- |
| `variantIds` | `List<Guid>` | Có | Tối đa 100 UUIDs | Danh sách mã định danh biến thể cần lấy thông tin |

- **JSON Request Body mẫu (camelCase):**
```json
{
  "variantIds": [
    "33333333-3333-3333-3333-333333333333",
    "55555555-5555-5555-5555-555555555555"
  ]
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
- **Vị trí file:** `Catalog.Application/Services/Product/ProductDtos.cs`
- **Tên C# Record:** `BatchVariantResponse`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record BatchVariantItemDto(
    Guid VariantId,
    Guid ProductId,
    string SkuCode,
    string VariantName,
    string ProductName,
    decimal? Price,
    string Currency,
    string Status,
    string ProductStatus,
    Guid SellerId,
    string SaleType,
    string? PrimaryImageUrl,
    IReadOnlyList<ProductVariantAttributeDto> Attributes);

public sealed record BatchVariantResponse(
    IReadOnlyList<BatchVariantItemDto> Items,
    IReadOnlyList<Guid> NotFoundIds);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "items": [
    {
      "variantId": "33333333-3333-3333-3333-333333333333",
      "productId": "77777777-7777-7777-7777-777777777777",
      "skuCode": "KB-NEXUS-01-WHITE",
      "variantName": "Màu Trắng - Gateron Yellow",
      "productName": "Bàn Phím Cơ Không Dây Nexus RGB",
      "price": 1250000,
      "currency": "VND",
      "status": "Active",
      "productStatus": "Active",
      "sellerId": "11111111-1111-1111-1111-111111111111",
      "saleType": "Regular",
      "primaryImageUrl": "https://res.cloudinary.com/nexus/image/upload/v1/products/kb-primary.webp",
      "attributes": [
        { "attrName": "Color", "attrValue": "White" }
      ]
    }
  ],
  "notFoundIds": [
    "99999999-9999-9999-9999-999999999999"
  ]
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Variant IDs list cannot be null or empty.` | Truyền mảng rỗng `[]` hoặc null |
| `400 Bad Request` | `VAL_LIMIT_EXCEEDED` | `Cannot query more than 100 variants in a single batch.` | Truyền vượt quá 100 ID |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi không mong muốn |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** `InternalVariantsController.GetVariantsBatch` nhận `BatchGetVariantsRequest`.
2. **Bước 2:** Gọi `_productService.GetVariantsBatchAsync(request, ct)`.
3. **Bước 3:** Loại bỏ các ID trùng lặp trong request.
4. **Bước 4:** Truy vấn database `ProductVariants` kèm `Product`, `Attributes` và `Images` theo `VariantIds.Contains(v.Id) && !v.IsDeleted`.
5. **Bước 5:** Tổng hợp danh sách biến thể tìm thấy vào `items`.
6. **Bước 6:** So sánh danh sách ID đầu vào với các ID tìm thấy để tổng hợp `notFoundIds`.
7. **Bước 7:** Trả về Pure DTO `BatchVariantResponse` với mã HTTP `200 OK`.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Hoàn tất hợp đồng DTO truy vấn theo lô liên service.
- [x] **Application:** Triển khai phương thức `GetVariantsBatchAsync` trong `ProductService.cs`.
- [x] **Api:** `InternalVariantsController.GetVariantsBatch` trả Pure DTO.
- [x] **Verify:** `dotnet build Catalog.sln -c Release` thành công 100% với 0 lỗi.

