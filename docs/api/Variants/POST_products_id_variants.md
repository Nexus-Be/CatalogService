# CATALOG SERVICE - POST /api/v1/products/{id}/variants

> **Feature / Use Case:** Thêm mới biến thể/SKU vào sản phẩm thương mại [CAT-07] (Tối đa 10 SKU, kiểm tra trùng mã SKU và đồng bộ kho Fulfillment)
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Nexus Team & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `POST`
- **Route Endpoint:** `/api/v1/products/{id}/variants`
- **Quyền hạn (RBAC Policy):** `RequireRole("seller", "admin")`
- **Quy tắc Sở hữu (Resource Ownership):** Bắt buộc sản phẩm mục tiêu phải thuộc quyền sở hữu của người bán (`product.SellerId == currentUserId`). Admin được quyền can thiệp.
- **Controller:** `Catalog.Api/Controllers/Public/ProductsController.cs`
- **Application Contract:** `Catalog.Application/Services/Product/IProductService.cs`
- **Tương tác Liên Service:** Nếu sản phẩm cha đang ở trạng thái `ACTIVE` (đối với hàng `SaleType.Regular`), hệ thống gọi `IFulfillmentClient.InitializeInventoryForSkuAsync` với số lượng mặc định là `0` (`initialQuantity: 0`) trong transaction. Quy trình gồm 2 chặng:
  1. *Chặng 1 (Xác định kho):* Gọi `GET /api/v1/warehouses/seller/{sellerId}` sang Fulfillment Service để lấy `warehouseId` của kho active thuộc về Seller (nếu seller chưa có kho riêng, fallback lấy kho tổng Platform qua `GET /api/v1/warehouses`).
  2. *Chặng 2 (Khởi tạo SKU tại kho):* Gọi `POST /api/v1/inventories/initialize` với `skuId`, `warehouseId` đã xác định và `initialQuantity = 0`.
  Nếu Fulfillment service lỗi hoặc không phản hồi, hệ thống rollback và trả về `502 Bad Gateway` (`FULFILLMENT_UNAVAILABLE`). Nếu sản phẩm cha đang là `DRAFT` hoặc `Auction`, hệ thống **không gọi tạo kho**, chỉ lưu biến thể thuần túy vào Catalog DB.

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters & Query Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `id` | Route | `Guid` | Có | UUIDv4 format | Khóa chính của sản phẩm cần thêm biến thể |

### 2.2 Request Body Record DTO
- **Vị trí file:** `Catalog.Application/Services/Product/ProductRequests.cs`
- **Tên C# Record:** `CreateVariantRequest`
- **Khai báo C# Record:**
```csharp
public sealed record CreateVariantAttributeRequest(string AttrName, string AttrValue);

public sealed record CreateVariantRequest(
    string SkuCode,
    string Name,
    decimal Price,
    string? Currency = "VND",
    List<CreateVariantAttributeRequest>? Attributes = null);
```
- **Bảng quy chuẩn Validation:**
| Tham số | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :---: | :--- | :--- |
| `skuCode` | `string` | Có | Max: 50 ký tự, Unique | Mã SKU quản lý hàng hóa |
| `name` | `string` | Có | Độ dài: 2 - 200 ký tự | Tên đại diện cho phiên bản/biến thể |
| `price` | `decimal` | Có | Min: 0, Max: 1,000,000,000 | Giá bán niêm yết của biến thể |
| `currency` | `string?` | Không | Mặc định: `"VND"` | Đơn vị tiền tệ |
| `attributes` | `List?` | Không | Tối đa 5 cặp thuộc tính | Ví dụ: Màu sắc, Size, Bộ nhớ |

- **JSON Request Body mẫu (camelCase):**
```json
{
  "skuCode": "KB-NEXUS-01-BLACK",
  "name": "Màu Đen - Gateron Red",
  "price": 1290000,
  "currency": "VND",
  "attributes": [
    { "attrName": "Color", "attrValue": "Black" },
    { "attrName": "Switch", "attrValue": "Gateron Red" }
  ]
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
- **Vị trí file:** `Catalog.Application/Services/Product/ProductDtos.cs`
- **Tên C# Record:** `VariantDetailDto`
- **Mã HTTP thành công:** `201 Created`
- **JSON Response Body mẫu (camelCase):**
```json
{
  "id": "55555555-5555-5555-5555-555555555555",
  "productId": "77777777-7777-7777-7777-777777777777",
  "skuCode": "KB-NEXUS-01-BLACK",
  "name": "Màu Đen - Gateron Red",
  "price": 1290000,
  "currency": "VND",
  "isActive": true,
  "hasOrder": false,
  "status": "Active",
  "statusReason": null,
  "attributes": [
    { "attrName": "Color", "attrValue": "Black" },
    { "attrName": "Switch", "attrValue": "Gateron Red" }
  ]
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `SKU code is required.` | Mã `skuCode` bị rỗng hoặc trắng |
| `400 Bad Request` | `VAL_INVALID_PRICE` | `Variant price must be non-negative.` | Giá tiền âm |
| `400 Bad Request` | `VAL_LIMIT_EXCEEDED` | `A product can have a maximum of 10 variants.` | Sản phẩm đã đạt đủ 10 SKU |
| `400 Bad Request` | `VAL_AUCTION_VARIANT_NOT_ALLOWED` | `Auction products cannot have additional variants.` | Cố tình thêm variant vào sản phẩm đấu giá |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required to access this resource.` | Thiếu Bearer token |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to modify this product.` | Không sở hữu sản phẩm này |
| `404 Not Found` | `PRODUCT_NOT_FOUND` | `Product not found with the specified identifier.` | Không tìm thấy sản phẩm cha |
| `409 Conflict` | `DUPLICATE_SKU` | `SKU code already exists in the catalog.` | `skuCode` bị trùng trong Catalog DB |
| `502 Bad Gateway` | `FULFILLMENT_UNAVAILABLE` | `Cannot initialize inventory for SKU '{skuCode}'. Fulfillment service is unavailable or returned an error.` | Sản phẩm cha đang ACTIVE nhưng gọi sang FulfillmentService thất bại |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi không mong muốn |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** `ProductsController.AddVariant` nhận `id` và `CreateVariantRequest`.
2. **Bước 2:** Lấy `currentUserId` từ JWT, gọi `_productService.AddVariantAsync(id, currentUserId, request, ct)`.
3. **Bước 3:** Nạp entity `Product` kèm danh sách `Variants` hiện có.
4. **Bước 4:** Kiểm tra quyền sở hữu của người bán.
5. **Bước 5 (Rào chắn Bất biến):** Kiểm tra `product.SaleType != SaleType.Auction` và tổng số lượng biến thể `< 10`.
6. **Bước 6:** Kiểm tra `skuCode` duy nhất trên toàn bảng `ProductVariants`.
7. **Bước 7:** Khởi tạo entity `ProductVariant` và các `ProductVariantAttribute`.
8. **Bước 8 (Khởi tạo kho có điều kiện):** Nếu sản phẩm cha đang ở trạng thái `ACTIVE` (và là Regular), gọi `_fulfillmentClient.InitializeInventoryForSkuAsync(variant.Id, product.SellerId, initialQuantity: 0)`. Nếu Fulfillment lỗi/unavailable, rollback và ném lỗi `502 FULFILLMENT_UNAVAILABLE`. Nếu sản phẩm cha đang là `DRAFT`, bỏ qua bước gọi Fulfillment và chỉ lưu biến thể vào Catalog DB.
9. **Bước 9:** Lưu DB và trả về `VariantDetailDto` với mã HTTP `201 Created`.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đặc tả ràng buộc tối đa 10 SKU và rào chắn Auction.
- [x] **Application:** Đã hiện thực phương thức `AddVariantAsync` trong `ProductService.cs`.
- [x] **Api:** `ProductsController.AddVariant` trả `201 Created`.
- [x] **Verify:** `dotnet build Catalog.sln -c Release` thành công 100% với 0 lỗi.

