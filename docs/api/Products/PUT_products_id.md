# CATALOG SERVICE - PUT /api/v1/products/{id}

> **Feature / Use Case:** Cập nhật thông tin chung của sản phẩm [CAT-02] (Hỗ trợ cả 2 phương thức HTTP: PUT và PATCH)
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Nexus Team & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `PUT` (Hỗ trợ alias: `PATCH`)
- **Route Endpoint:** `/api/v1/products/{id}`
- **Quyền hạn (RBAC Policy):** `RequireRole("seller", "admin")`
- **Quy tắc Sở hữu (Resource Ownership):** Bắt buộc sản phẩm phải thuộc quyền sở hữu của người bán đang thao tác (`product.SellerId == currentUserId`). Admin được quyền can thiệp.
- **Controller:** `Catalog.Api/Controllers/Public/ProductsController.cs`
- **Application Contract:** `Catalog.Application/Services/Product/IProductService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters & Query Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `id` | Route | `Guid` | Có | UUIDv4 format | Khóa chính của sản phẩm cần cập nhật |

### 2.2 Request Body Record DTO
- **Vị trí file:** `Catalog.Application/Services/Product/ProductRequests.cs`
- **Tên C# Record:** `UpdateProductRequest`
- **Khai báo C# Record:**
```csharp
public sealed record UpdateProductRequest(
    string Name,
    string? Description = null,
    string? Brand = null,
    Guid? CategoryId = null,
    SaleType? SaleType = null,
    string? Status = null,
    List<ProductSpecificationDto>? Specifications = null);
```
- **Bảng quy chuẩn Validation:**
| Tham số | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :---: | :--- | :--- |
| `name` | `string` | Có | Độ dài: 3 - 250 ký tự | Tên hiển thị mới của sản phẩm |
| `description` | `string?` | Không | Max: 4000 ký tự | Nội dung mô tả chi tiết |
| `brand` | `string?` | Không | Max: 100 ký tự | Thương hiệu sản phẩm |
| `categoryId` | `Guid?` | Không | UUIDv4 format | Chuyển sản phẩm sang danh mục mới (nếu có) |
| `saleType` | `SaleType?` | Không | `Regular`, `Auction` | Hình thức kinh doanh |
| `specifications` | `List<ProductSpecificationDto>?` | Không | Tối đa 30 mục (Name <= 100, Value <= 500) | Cấu hình kỹ thuật dạng danh sách cặp { name, value } |

- **JSON Request Body mẫu (camelCase):**
```json
{
  "name": "Bàn Phím Cơ Không Dây Nexus RGB V2",
  "description": "Bàn phím cơ Bluetooth 5.1 hotswap 3 modes đã nâng cấp switch và foam tiêu âm.",
  "brand": "Nexus Tech",
  "categoryId": "22222222-2222-2222-2222-222222222222",
  "specifications": [
    { "name": "switch", "value": "Gateron Yellow Pro" },
    { "name": "battery", "value": "5000mAh" }
  ]
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
- **Vị trí file:** `Catalog.Application/Services/Product/ProductDtos.cs`
- **Tên C# Record:** `ProductDto`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record ProductDto(
    Guid Id,
    Guid SellerId,
    Guid CategoryId,
    string Name,
    string? Description,
    string? Brand,
    string Status);
```
- **JSON Response Body mẫu (camelCase):**
```json
{
  "id": "77777777-7777-7777-7777-777777777777",
  "sellerId": "11111111-1111-1111-1111-111111111111",
  "categoryId": "22222222-2222-2222-2222-222222222222",
  "name": "Bàn Phím Cơ Không Dây Nexus RGB V2",
  "description": "Bàn phím cơ Bluetooth 5.1 hotswap 3 modes đã nâng cấp switch và foam tiêu âm.",
  "brand": "Nexus Tech",
  "status": "Draft"
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `PRODUCT_IN_AUCTION` | `Product is currently in an active auction and cannot be modified.` | Sản phẩm đang trong phiên đấu giá mở (`InAuction`) |
| `400 Bad Request` | `PRODUCT_ALREADY_AUCTIONED` | `Auctioned products cannot be modified.` | Sản phẩm đấu giá đã bán xong (`Auctioned`) |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Product name is required.` | Tên sản phẩm bị rỗng hoặc nhỏ hơn 3 ký tự |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Specification name cannot be empty.` | Tên thông số kỹ thuật bị rỗng hoặc trắng |
| `400 Bad Request` | `VAL_LIMIT_EXCEEDED` | `A product can have a maximum of 30 specifications.` | Danh sách `specifications` vượt quá 30 mục |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required to access this resource.` | Không có Bearer token hợp lệ |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to modify this product.` | `currentUserId` không trùng với `sellerId` của sản phẩm |
| `404 Not Found` | `PRODUCT_NOT_FOUND` | `Product not found with the specified identifier.` | Không tồn tại sản phẩm hoặc sản phẩm đã bị xóa mềm |
| `404 Not Found` | `CATEGORY_NOT_FOUND` | `Category not found or is currently inactive.` | Cung cấp `categoryId` mới nhưng không tồn tại |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi không mong muốn trong quá trình lưu dữ liệu |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `ProductStatus.Draft` | `0` | `"Draft"` | Bản nháp |
| `ProductStatus.Active` | `1` | `"Active"` | Đang mở bán |
| `ProductStatus.Inactive` | `2` | `"Inactive"` | Tạm ngưng |
| `ProductStatus.Auctioned` | `3` | `"Auctioned"` | Đã bán xong trong phiên đấu giá |
| `ProductStatus.InAuction` | `4` | `"InAuction"` | Đang trong phiên đấu giá mở (Khóa sửa/xóa) |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** `ProductsController.Update` nhận `id` và `UpdateProductRequest`.
2. **Bước 2:** Lấy `currentUserId` và gọi `_productService.UpdateAsync(id, request, currentUserId, ct)`.
3. **Bước 3:** Tìm sản phẩm theo `id` từ `AppDbContext.Products`. Nếu không tìm thấy hoặc `IsDeleted == true` ném `EntityNotFoundException`.
4. **Bước 4:** Kiểm tra quyền sở hữu (`product.SellerId != currentUserId` và không phải Admin thì ném `UnauthorizedAccessException`).
5. **Bước 5:** Nếu cập nhật `CategoryId`, kiểm tra danh mục tồn tại.
6. **Bước 6:** Cập nhật các trường thông tin: `Name`, `Description`, `Brand`, `Specifications`, `UpdatedAt`.
7. **Bước 7:** Lưu thay đổi vào DB và trả về `ProductDto` với mã HTTP `200 OK`.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã rà soát hợp đồng cập nhật sản phẩm `PUT` & `PATCH`.
- [x] **Application:** Triển khai trong `ProductService.cs`.
- [x] **Api:** `ProductsController.Update` hỗ trợ cả 2 verb `[HttpPut("{id:guid}")]` và `[HttpPatch("{id:guid}")]`.
- [x] **Verify:** `dotnet build Catalog.sln -c Release` thành công 100% với 0 lỗi.

