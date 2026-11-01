# CATALOG SERVICE - GET /api/v1/variants/{id}

> **Feature / Use Case:** Lấy thông tin chi tiết một biến thể (SKU) theo ID [CAT-42]
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Nexus Team & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/api/v1/variants/{id}`
- **Quyền hạn (RBAC Policy):** `AllowAnonymous` (Khách vãng lai và các client nội bộ đều có thể truy xuất)
- **Quy tắc Sở hữu (Resource Ownership):** Không yêu cầu
- **Controller:** `Catalog.Api/Controllers/Public/VariantsController.cs`
- **Application Contract:** `Catalog.Application/Services/Product/IProductService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters & Query Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `id` | Route | `Guid` | Có | UUIDv4 format | Định danh duy nhất của biến thể (Variant/SKU) |

### 2.2 Request Body Record DTO
- Endpoint `GET` không sử dụng Request Body.

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** Trả về DTO thuần `VariantDetailDto` với mã HTTP `200 OK`.

- **Vị trí file:** `Catalog.Application/Services/Product/ProductDtos.cs`
- **Tên C# Record:** `VariantDetailDto`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record ProductVariantAttributeDto(string AttrName, string AttrValue);

public sealed record VariantDetailDto(
    Guid Id,
    Guid ProductId,
    string SkuCode,
    string Name,
    decimal? Price,
    string Currency,
    bool IsActive,
    bool HasOrder,
    string Status,
    string? StatusReason,
    IReadOnlyList<ProductVariantAttributeDto> Attributes);
```
- **JSON Response Body mẫu (camelCase):**
```json
{
  "id": "33333333-3333-3333-3333-333333333333",
  "productId": "77777777-7777-7777-7777-777777777777",
  "skuCode": "KB-NEXUS-01-WHITE",
  "name": "Màu Trắng - Gateron Yellow",
  "price": 1250000,
  "currency": "VND",
  "isActive": true,
  "hasOrder": false,
  "status": "Active",
  "statusReason": null,
  "attributes": [
    {
      "attrName": "Color",
      "attrValue": "White"
    },
    {
      "attrName": "Switch",
      "attrValue": "Gateron Yellow"
    }
  ]
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Invalid variant identifier format.` | Chuỗi `id` không đúng định dạng UUIDv4 |
| `404 Not Found` | `VARIANT_NOT_FOUND` | `Variant not found with the specified identifier.` | Không tồn tại biến thể hoặc biến thể đã bị xóa mềm |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi không mong muốn trong quá trình truy vấn hệ thống |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `VariantStatus.Active` | `0` | `"Active"` | Biến thể đang hoạt động có thể đặt mua |
| `VariantStatus.Inactive` | `1` | `"Inactive"` | Biến thể tạm khóa hoặc hết hàng |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** `VariantsController.GetVariantById` tiếp nhận tham số `[FromRoute] Guid id`.
2. **Bước 2:** Gọi `_productService.GetVariantByIdAsync(id, ct)`.
3. **Bước 3:** Truy vấn entity `ProductVariant` từ database với `.AsNoTracking()`, nạp kèm danh sách `Attributes`.
4. **Bước 4:** Nếu không tìm thấy hoặc `IsDeleted == true`, ném `EntityNotFoundException`.
5. **Bước 5:** Map sang `VariantDetailDto` và trả về `Ok(variant)`.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Hoàn thiện đặc tả DTO chi tiết biến thể.
- [x] **Application:** Đã cài đặt `GetVariantByIdAsync` trong `ProductService.cs`.
- [x] **Api:** `VariantsController.GetVariantById` trả Pure DTO với XML documentation 1 dòng.
- [x] **Verify:** `dotnet build Catalog.sln -c Release` thành công 100% với 0 lỗi.

