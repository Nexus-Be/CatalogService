# CATALOG SERVICE - POST /api/v1/products/auction

> **Feature / Use Case:** Tạo sản phẩm phục vụ phiên đấu giá ở trạng thái Draft [CAT-43] (Tự động sinh biến thể duy nhất không kèm giá cố định - Unpriced SKU)
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Nexus Team & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `POST`
- **Route Endpoint:** `/api/v1/products/auction`
- **Quyền hạn (RBAC Policy):** `RequireRole("seller", "admin")`
- **Quy tắc Sở hữu (Resource Ownership):** Sản phẩm đấu giá luôn được gán cho chính người bán đang đăng nhập (`sellerId == currentUserId`). Admin có thể hỗ trợ tạo.
- **Controller:** `Catalog.Api/Controllers/Public/ProductsController.cs`
- **Application Contract:** `Catalog.Application/Services/Product/IProductService.cs`
- **Tương tác Liên Service:** Gọi `IFulfillmentClient` để đồng bộ mã SKU đấu giá sang Fulfillment Service. Giá sản phẩm không cố định mà do Auction Service quyết định khi gõ búa.

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters & Query Parameters
- Không có tham số Route hoặc Query.

### 2.2 Request Body Record DTO
- **Vị trí file:** `Catalog.Application/Services/Product/ProductRequests.cs`
- **Tên C# Record:** `CreateAuctionProductRequest`
- **Khai báo C# Record:**
```csharp
public sealed record CreateAuctionProductRequest(
    Guid CategoryId,
    string Name,
    string? Description = null,
    string? Brand = null,
    string? SkuCode = null,
    string? Specifications = null,
    Guid? SellerId = null);
```
- **Bảng quy chuẩn Validation:**
| Tham số | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :---: | :--- | :--- |
| `categoryId` | `Guid` | Có | UUIDv4 format | Danh mục phải tồn tại trong hệ thống |
| `name` | `string` | Có | Độ dài: 3 - 250 ký tự | Tên hiển thị vật phẩm đấu giá |
| `description` | `string?` | Không | Max: 4000 ký tự | Mô tả chi tiết tình trạng vật phẩm |
| `brand` | `string?` | Không | Max: 100 ký tự | Thương hiệu hoặc xuất xứ |
| `skuCode` | `string?` | Không | Tối đa 50 ký tự | Mã SKU. Nếu để trống hệ thống tự sinh `AUC-{UUID}` |
| `specifications` | `string?` | Không | Chuỗi JSON hợp lệ | Thuộc tính giám định, thông số kỹ thuật |
| `sellerId` | `Guid?` | Không | UUIDv4 format | Chỉ Admin mới được chỉ định `sellerId` khác |

- **JSON Request Body mẫu (camelCase):**
```json
{
  "categoryId": "22222222-2222-2222-2222-222222222222",
  "name": "Bình Hoa Gốm Cổ Chu Đậu Thế Kỷ 15",
  "description": "Hiện vật gốm sứ hoa lam men rạn nguyên bản có chứng thư giám định cổ vật.",
  "brand": "Chu Đậu",
  "skuCode": "AUC-CERAMIC-15TH-001",
  "specifications": "{\"height\": \"32cm\", \"origin\": \"Hai Duong\", \"certificate\": \"CERT-88992\"}"
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** Trả về Pure DTO `ProductDto` với mã HTTP `201 Created`.

- **Vị trí file:** `Catalog.Application/Services/Product/ProductDtos.cs`
- **Tên C# Record:** `ProductDto`
- **Mã HTTP thành công:** `201 Created`
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
  "id": "99999999-9999-9999-9999-999999999999",
  "sellerId": "11111111-1111-1111-1111-111111111111",
  "categoryId": "22222222-2222-2222-2222-222222222222",
  "name": "Bình Hoa Gốm Cổ Chu Đậu Thế Kỷ 15",
  "description": "Hiện vật gốm sứ hoa lam men rạn nguyên bản có chứng thư giám định cổ vật.",
  "brand": "Chu Đậu",
  "status": "Draft"
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Product name is required.` | `name` bị rỗng hoặc dưới 3 ký tự |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Category ID is required.` | `categoryId` không hợp lệ hoặc để trống |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required to access this resource.` | Không có Bearer token trong header |
| `403 Forbidden` | `FORBIDDEN` | `You cannot create products on behalf of another seller.` | Seller thông thường chỉ định `sellerId` của người khác |
| `404 Not Found` | `CATEGORY_NOT_FOUND` | `Category not found or is currently inactive.` | Không tìm thấy danh mục liên kết |
| `409 Conflict` | `DUPLICATE_SKU` | `SKU code already exists in the catalog.` | `skuCode` cung cấp đã được dùng cho sản phẩm khác |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi hệ thống trong quá trình lưu dữ liệu |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `ProductStatus.Draft` | `0` | `"Draft"` | Khởi tạo ở trạng thái bản nháp trước khi phê duyệt lên sàn |
| `SaleType.Auction` | `1` | `"Auction"` | Đánh dấu là sản phẩm đấu giá (duy nhất 1 biến thể không giá) |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** `ProductsController.CreateAuction` nhận `CreateAuctionProductRequest`.
2. **Bước 2:** Lấy `currentUserId` từ JWT qua `TryGetUserId`.
3. **Bước 3:** Gọi `_productService.CreateAuctionProductAsync(request, currentUserId, ct)`.
4. **Bước 4:** Kiểm tra danh mục hợp lệ.
5. **Bước 5:** Tạo `Product` với `SaleType = SaleType.Auction` và `Status = ProductStatus.Draft`.
6. **Bước 6:** Tự động tạo 1 `ProductVariant` duy nhất với `Price = null`, `Currency = "VND"`, và mã SKU chỉ định (hoặc sinh tự động).
7. **Bước 7:** Gọi `IFulfillmentClient.CreateProductAsync` để đăng ký SKU với kho Fulfillment.
8. **Bước 8:** Lưu vào database và trả về `ProductDto` với mã HTTP `201 Created`.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Hoàn tất tài liệu đặc tả endpoint `POST /api/v1/products/auction`.
- [x] **Application:** Đã hiện thực phương thức `CreateAuctionProductAsync` trong `ProductService.cs`.
- [x] **Api:** `ProductsController.CreateAuction` đăng ký route và response 201 Created.
- [x] **Verify:** `dotnet build Catalog.sln -c Release` thành công 100% với 0 lỗi.

