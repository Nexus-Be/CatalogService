# CATALOG SERVICE - POST /api/v1/internal/catalog/skus/validate-variant-switch

> **Feature / Use Case:** Xác thực đổi phân loại hàng trong giỏ (Variant Switch Validation) phục vụ Cart Service [CAT-47]  
> **Trạng thái:** ✅ VERIFIED  
> **Ngày tạo:** 2026-11-02  
> **Tác giả:** Nexus Team & AI Agent  

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `POST`
- **Route Endpoint chính (Hợp đồng Commerce):** `/api/v1/internal/catalog/skus/validate-variant-switch`
- **Route Endpoint alias (Nội bộ Catalog):** `/api/v1/internal/variants/validate-switch`
- **Quyền hạn (RBAC Policy):** `AllowAnonymous` / `InternalMicroservice` (Endpoint nội bộ bảo vệ bởi API Gateway network boundary hoặc Mutual TLS)
- **Quy tắc Sở hữu (Resource Ownership):** Không yêu cầu (Hệ thống tự động kiểm tra cả hai SKU cùng thuộc một `ProductId` và `SellerId`)
- **Controller:** `Catalog.Api/Controllers/Internal/InternalVariantsController.cs`
- **Application Contract:** `Catalog.Application/Services/Product/IProductService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters & Query Parameters
- Không có tham số Route hoặc Query.

### 2.2 Request Body Record DTO
- **Vị trí file:** `Catalog.Application/Services/Product/ProductRequests.cs`
- **Tên C# Record:** `ValidateVariantSwitchRequest`
- **Khai báo C# Record:**
```csharp
public sealed record ValidateVariantSwitchRequest(
    Guid CurrentSkuId,
    Guid NewSkuId);
```
- **Bảng quy chuẩn Validation:**
| Tham số | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :---: | :--- | :--- |
| `currentSkuId` | `Guid` | Có | UUID v4, khác `Guid.Empty` | SKU hiện tại trong giỏ hàng |
| `newSkuId` | `Guid` | Có | UUID v4, khác `Guid.Empty` | SKU mới khách muốn chuyển sang |

- **JSON Request Body mẫu (camelCase):**
```json
{
  "currentSkuId": "5fe253bc-97ea-4cbc-960a-33f13be6730e",
  "newSkuId": "b187fed0-a82e-4720-b812-ecbc6d4b94c0"
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
- **Vị trí file:** `Catalog.Application/Services/Product/ProductDtos.cs`
- **Tên C# Record:** `ValidateVariantSwitchResponse`
- **Khai báo C# Record:**
```csharp
public sealed record ValidateVariantSwitchResponse(
    bool IsValid,
    Guid ProductId,
    Guid SellerId,
    Guid NewSkuId,
    string NewSkuName,
    decimal NewUnitPrice,
    bool IsPurchasable,
    int WeightGrams = 200);
```

- **Bảng chi tiết trường dữ liệu trả về:**
| Tên Field | Kiểu dữ liệu | Ý nghĩa |
| :--- | :--- | :--- |
| `isValid` | `boolean` | `true` nếu `currentSkuId` và `newSkuId` cùng thuộc 1 sản phẩm cha |
| `productId` | `UUID` | ID sản phẩm cha |
| `sellerId` | `UUID` | ID của Seller sở hữu sản phẩm |
| `newSkuId` | `UUID` | ID của biến thể mới |
| `newSkuName` | `string` | Tên của biến thể mới |
| `newUnitPrice` | `decimal` | Đơn giá niêm yết mới nhất (Fresh Price) |
| `isPurchasable` | `boolean` | `true` nếu biến thể mới đủ điều kiện bán (Active, giá > 0, không bị xóa) |
| `weightGrams` | `int` | Khối lượng phục vụ vận chuyển GHN (mặc định 200g) |

- **JSON Response mẫu thành công (`200 OK`):**
```json
{
  "isValid": true,
  "productId": "1f0ccdfc-a6f4-47e2-bab0-98de9db4c403",
  "sellerId": "cccccccc-3333-3333-3333-333333333333",
  "newSkuId": "b187fed0-a82e-4720-b812-ecbc6d4b94c0",
  "newSkuName": "Màu Trắng - Core i5",
  "newUnitPrice": 15000000.00,
  "isPurchasable": true,
  "weightGrams": 200
}
```

---

## 4. Ma trận Mã lỗi Chuẩn (RFC 9110 Error Matrix)

| HTTP Status | Error Code (`errorCode`) | Error Message (`detail`) | Nguyên nhân kích hoạt |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `"Current SKU ID and new SKU ID must be provided."` | Truyền `currentSkuId` hoặc `newSkuId` bằng `Guid.Empty` |
| `400 Bad Request` | `CATALOG_VARIANT_MISMATCH` | `"New SKU does not belong to the same parent product."` | Hai SKU thuộc 2 sản phẩm khác nhau |
| `404 Not Found` | `CATALOG_SKU_NOT_FOUND` | `"Entity 'ProductVariant' with key '...' was not found."` | `currentSkuId` hoặc `newSkuId` không tồn tại hoặc đã bị xóa mềm |

---

## 5. Quy trình Kiểm chứng (Verification)
Đã kiểm chứng thực tế trên server `http://localhost:8002` vào ngày 2026-11-02:
1. Đổi variant hợp lệ cùng sản phẩm ➔ Trả về `200 OK`, `isValid: true`, `isPurchasable: true`.
2. Đổi variant khác sản phẩm ➔ Trả về `400 Bad Request`, `errorCode: "CATALOG_VARIANT_MISMATCH"`.
3. SKU không tồn tại ➔ Trả về `404 Not Found`, `errorCode: "CATALOG_SKU_NOT_FOUND"`.
4. Đầu vào rỗng ➔ Trả về `400 Bad Request`, `errorCode: "VAL_INVALID_INPUT"`.

