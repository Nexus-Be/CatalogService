# CATALOG SERVICE - POST /api/v1/products

> **Feature / Use Case:** Tạo sản phẩm thương mại thông thường ở trạng thái Draft [CAT-01] (Hỗ trợ tạo đơn bước hoặc kèm danh sách biến thể ban đầu)
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Nexus Team & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `POST`
- **Route Endpoint:** `/api/v1/products`
- **Quyền hạn (RBAC Policy):** `RequireRole("seller", "admin")`
- **Quy tắc Sở hữu (Resource Ownership):** Cả Seller lẫn Admin chỉ được phép tạo sản phẩm cho chính mình (`sellerId == currentUserId`). Hệ thống cấm tuyệt đối việc tạo hộ sản phẩm cho người khác; nếu `sellerId` được gửi trong request body mà khác với `currentUserId`, hệ thống sẽ từ chối với mã lỗi `403 Forbidden` (`FORBIDDEN`).
- **Controller:** `Catalog.Api/Controllers/Public/ProductsController.cs`
- **Application Contract:** `Catalog.Application/Services/Product/IProductService.cs`
- **Tương tác Liên Service:** Sản phẩm tạo ở trạng thái `DRAFT` **không gọi tạo kho** sang Fulfillment Service để tránh tạo kho chết. Tồn kho chỉ được khởi tạo khi sản phẩm được chuyển sang trạng thái `ACTIVE` (đối với sản phẩm Regular). Sản phẩm `Auction` không quản lý tồn kho và không gọi Fulfillment.

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters & Query Parameters
- Không có tham số Route hoặc Query.

### 2.2 Request Body Record DTO
- **Vị trí file:** `Catalog.Application/Services/Product/ProductRequests.cs`
- **Tên C# Record:** `CreateProductRequest`
- **Khai báo C# Record:**
```csharp
public sealed record ProductSpecificationDto(string Name, string Value);

public sealed record CreateVariantAttributeRequest(string AttrName, string AttrValue);

public sealed record CreateVariantRequest(
    string SkuCode,
    string Name,
    decimal Price,
    string? Currency = "VND",
    List<CreateVariantAttributeRequest>? Attributes = null);

public sealed record CreateProductRequest(
    Guid SellerId,
    Guid CategoryId,
    string Name,
    string? Description = null,
    string? Brand = null,
    SaleType SaleType = SaleType.Regular,
    List<ProductSpecificationDto>? Specifications = null,
    List<CreateVariantRequest>? Variants = null);
```
- **Bảng quy chuẩn Validation:**
| Trường | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :---: | :--- | :--- |
| `sellerId` | `Guid` | Không | UUIDv4 format | Nếu rỗng, hệ thống tự động gán từ JWT UserId |
| `categoryId` | `Guid` | Có | UUIDv4 format | Danh mục phải tồn tại và đang hoạt động |
| `name` | `string` | Có | Độ dài: 3 - 250 ký tự | Tên hiển thị của sản phẩm |
| `description` | `string?` | Không | Max: 4000 ký tự | Mô tả chi tiết sản phẩm |
| `brand` | `string?` | Không | Max: 100 ký tự | Thương hiệu sản xuất |
| `saleType` | `SaleType` | Không | Mặc định: `Regular` (`0`) | Hàng thương mại cố định giá |
| `specifications` | `List<ProductSpecificationDto>?` | Không | Tối đa 30 mục (Name <= 100, Value <= 500) | Danh sách thông số kỹ thuật dạng cặp { name, value } |
| `variants` | `List<CreateVariantRequest>?` | Không | Tối đa 10 SKUs ban đầu | Tùy chọn tạo kèm biến thể (quy trình Single-Step) |

- **JSON Request Body mẫu (Pure DTO - camelCase):**
```json
{
  "categoryId": "22222222-2222-2222-2222-222222222222",
  "name": "Bàn Phím Cơ Không Dây Nexus RGB",
  "description": "Bàn phím cơ Bluetooth 5.1 hotswap 3 modes kết nối độ trễ siêu thấp.",
  "brand": "Nexus Tech",
  "saleType": "Regular",
  "specifications": [
    { "name": "switch", "value": "Gateron Yellow" },
    { "name": "battery", "value": "4000mAh" }
  ],
  "variants": [
    {
      "skuCode": "KB-NEXUS-01-WHITE",
      "name": "Màu Trắng - Gateron Yellow",
      "price": 1250000,
      "currency": "VND",
      "attributes": [
        { "attrName": "Color", "attrValue": "White" }
      ]
    }
  ]
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** Trả về Pure DTO trực tiếp với mã HTTP `201 Created`.

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
  "id": "77777777-7777-7777-7777-777777777777",
  "sellerId": "11111111-1111-1111-1111-111111111111",
  "categoryId": "22222222-2222-2222-2222-222222222222",
  "name": "Bàn Phím Cơ Không Dây Nexus RGB",
  "description": "Bàn phím cơ Bluetooth 5.1 hotswap 3 modes kết nối độ trễ siêu thấp.",
  "brand": "Nexus Tech",
  "status": "Draft"
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Product name is required.` | Tên sản phẩm để trống hoặc dưới 3 ký tự |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Category ID is required.` | `categoryId` không hợp lệ hoặc để trống |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Specification name cannot be empty.` | Tên thông số kỹ thuật bị rỗng hoặc trắng |
| `400 Bad Request` | `VAL_LIMIT_EXCEEDED` | `A product can have a maximum of 30 specifications.` | Danh sách `specifications` vượt quá 30 mục |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required to access this resource.` | Thiếu Access Token hoặc token hết hạn |
| `403 Forbidden` | `FORBIDDEN` | `You cannot create products on behalf of another seller.` | Người dùng (Seller hoặc Admin) truyền `sellerId` khác với User ID của mình |
| `404 Not Found` | `CATEGORY_NOT_FOUND` | `Category not found or is currently inactive.` | `categoryId` không tồn tại trong hệ thống |
| `409 Conflict` | `DUPLICATE_SKU` | `One or more SKU codes already exist in the catalog.` | Mã SKU trong danh sách `variants` bị trùng lặp |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi không mong muốn trong quá trình lưu dữ liệu |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `ProductStatus.Draft` | `0` | `"Draft"` | Sản phẩm mới tạo luôn khởi tạo ở trạng thái bản nháp |
| `SaleType.Regular` | `0` | `"Regular"` | Hàng thương mại cố định giá |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** `ProductsController.Create` trích xuất `currentUserId` từ claims JWT qua `TryGetUserId`.
2. **Bước 2:** Xác thực quyền sở hữu: nếu không phải Admin, `effectiveSellerId` bắt buộc phải là `currentUserId`.
3. **Bước 3:** Gọi `_productService.CreateAsync(reqWithSeller, ct)`.
4. **Bước 4:** Kiểm tra danh mục (`CategoryId`) tồn tại trong database PostgreSQL.
5. **Bước 5:** Khởi tạo entity `Product` với `Status = ProductStatus.Draft` và lưu vào `AppDbContext`.
6. **Bước 6 (Lưu trữ biến thể không khởi tạo kho):** Nếu có mảng `Variants`, kiểm tra tính duy nhất của `SkuCode`, thêm các entity `ProductVariant` vào `AppDbContext`. Tuyệt đối **không gọi `IFulfillmentClient`** khi tạo sản phẩm ở trạng thái `DRAFT` để tránh tạo rác tồn kho. Tồn kho thực tế sẽ chỉ được khởi tạo với số lượng mặc định là `0` khi sản phẩm được kích hoạt sang `ACTIVE`.
7. **Bước 7:** Lưu thay đổi vào cơ sở dữ liệu (`SaveChangesAsync`) và trả về `ProductDto` với HTTP status `201 Created`.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đặc tả đầy đủ hợp đồng request DTO và ma trận lỗi.
- [x] **Application:** Đã cài đặt trong `ProductService.cs` hỗ trợ cả quy trình tạo đơn lẻ lẫn tạo kèm variants.
- [x] **Api:** `ProductsController.Create` áp dụng attribute `[RequireRole("seller", "admin")]`.
- [x] **Verify:** `dotnet build Catalog.sln -c Release` thành công 100% với 0 lỗi.

