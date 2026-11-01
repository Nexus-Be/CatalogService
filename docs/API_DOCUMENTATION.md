# TÀI LIỆU CHI TIẾT TỪNG API - CATALOG SERVICE

> **Dịch vụ:** Nexus Catalog Service (Quản lý Danh mục, Sản phẩm, Biến thể SKU, Hình ảnh)  
> **Cổng mặc định:** `8002` (Docker / Local Kestrel)  
> **Phiên bản API:** `v1`  
> **Định dạng dữ liệu:** `application/json` (Upload ảnh: `multipart/form-data`)  
> **Cơ sở dữ liệu:** PostgreSQL Neon Cloud (`catalog`)

---

## 📌 PHẦN 1: QUY CHUẨN KỸ THUẬT VÀ XÁC THỰC

### 1. Headers xác thực & Bối cảnh Gateway
Mọi yêu cầu từ Client/Frontend đi qua API Gateway sẽ được đính kèm các Header sau:
* `Authorization`: `Bearer <jwt-token>` (Token đăng nhập của người dùng).
* `x-user-id`: GUID định danh người dùng (Bắt buộc với các endpoint yêu cầu quyền Seller/Admin).
* `x-user-roles`: Quyền của người dùng, phân cách bởi dấu phẩy (Ví dụ: `seller`, `admin`, `staff`, `buyer`).

### 2. Định dạng phản hồi lỗi chuẩn (RFC 9110 Problem Details)
Tất cả các lỗi trả về từ hệ thống đều tuân thủ chuẩn `application/problem+json`:
```json
{
  "code": "VAL_INVALID_INPUT",
  "detail": "Product name cannot exceed 255 characters.",
  "status": 400,
  "title": "Bad Request",
  "instance": "/api/v1/products"
}
```

### 3. Bảng mã lỗi hệ thống (Standard Error Codes)
* `VAL_INVALID_INPUT`: Dữ liệu đầu vào sai định dạng, thiếu trường bắt buộc, hoặc vi phạm điều kiện logic.
* `PERMISSION_DENIED`: Người dùng không có quyền thao tác trên tài nguyên của người khác (HTTP 403).
* `UNAUTHORIZED`: Thiếu thông tin người dùng trong header (HTTP 401).
* `CATEGORY_NOT_FOUND`: Danh mục không tồn tại trong hệ thống (HTTP 404).
* `PRODUCT_NOT_FOUND`: Sản phẩm không tồn tại hoặc đã bị xóa mềm (HTTP 404).
* `VARIANT_NOT_FOUND`: Biến thể SKU không tồn tại (HTTP 404).
* `IMAGE_NOT_FOUND`: Hình ảnh không tồn tại (HTTP 404).
* `DUPLICATE_SKU`: Mã SKU bị trùng lặp trong nội bộ gian hàng của Seller (HTTP 409).
* `MAX_VARIANTS_REACHED`: Vượt quá số lượng tối đa 10 biến thể cho 1 sản phẩm (HTTP 400).
* `VAL_LIMIT_EXCEEDED`: Vượt quá số lượng tối đa 5 hình ảnh cho 1 sản phẩm (HTTP 400).

---

## 📁 PHẦN 2: PHÂN HỆ DANH MỤC (CATEGORY MANAGEMENT)

---

### API 01. `GET /api/v1/categories` — Lấy danh sách danh mục
* **Mã nghiệp vụ:** Không gắn mã (Truy vấn danh mục)
* **Mục đích:** Người mua và người bán tra cứu danh mục hàng hóa trên sàn theo dạng phẳng hoặc cây 3 cấp.
* **Quyền hạn & Xác thực:** `Public` (Không yêu cầu đăng nhập).
* **Quy tắc Validate (Validation Rules):**
  * `parentId`: Nếu truyền, phải đúng định dạng GUID hợp lệ.
  * `tree`: Kiểu Boolean (`true`/`false`).
  * `keyword`: Chuỗi tìm kiếm, tự động cắt khoảng trắng đầu cuối (Trim).
* **Chi tiết Request:**
  * **Headers:** Không yêu cầu.
  * **Query Parameters:**
    * `parentId` (Guid?, Tùy chọn): Lọc các danh mục con trực tiếp thuộc về danh mục cha này.
    * `tree` (Boolean, Mặc định `false`): Nếu `true`, trả về cây phân cấp lồng nhau. Nếu `false`, trả về danh sách phẳng.
    * `keyword` (String?, Tùy chọn): Tìm kiếm danh mục theo tên.
* **Chi tiết Response:**
  * **Thành công (200 OK):**
    ```json
    [
      {
        "categoryId": "10000000-0000-0000-0000-000000000001",
        "parentId": null,
        "name": "Thời Trang",
        "description": "Quần áo thời trang nam nữ",
        "level": 1,
        "sortOrder": 1,
        "children": [
          {
            "categoryId": "10000000-0000-0000-0000-000000000002",
            "parentId": "10000000-0000-0000-0000-000000000001",
            "name": "Thời Trang Nam",
            "description": "Thời trang dành cho phái nam",
            "level": 2,
            "sortOrder": 1,
            "children": []
          }
        ]
      }
    ]
    ```
* **Luồng xử lý (Endpoint Processing Flow):**
  1. Tiếp nhận query parameters từ client.
  2. Truy vấn bảng `Categories` trong database (chỉ lấy các bản ghi `IsDeleted == false` và `IsDisabled == false`).
  3. Nếu `tree == true`: Tự động build cấu trúc phân cấp đệ quy cây 3 cấp từ tập dữ liệu.
  4. Nếu `tree == false`: Trả về danh sách phẳng sắp xếp theo `Level` và `SortOrder`.
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác** (Xử lý hoàn toàn trong nội bộ Catalog Database).

---

### API 02. `GET /api/v1/categories/{id}` — Lấy chi tiết một danh mục
* **Mục đích:** Xem thông tin chi tiết của một danh mục cụ thể theo ID.
* **Quyền hạn & Xác thực:** `Public`.
* **Quy tắc Validate (Validation Rules):**
  * `id`: Bắt buộc, phải là GUID hợp lệ.
* **Chi tiết Request:**
  * **Path Parameters:** `id` (Guid, Bắt buộc).
* **Chi tiết Response:**
  * **Thành công (200 OK):**
    ```json
    {
      "id": "10000000-0000-0000-0000-000000000001",
      "parentId": null,
      "name": "Thời Trang",
      "description": "Quần áo thời trang nam nữ",
      "level": 1,
      "sortOrder": 1,
      "isDisabled": false
    }
    ```
  * **Lỗi (404 Not Found):** Nếu không tìm thấy danh mục hoặc danh mục đã bị xóa mềm (`CATEGORY_NOT_FOUND`).
* **Luồng xử lý (Endpoint Processing Flow):**
  1. Kiểm tra tồn tại của danh mục trong bảng `Categories`.
  2. Nếu không tìm thấy, ném ngoại lệ `EntityNotFoundException("CATEGORY_NOT_FOUND")`.
  3. Trả về DTO chi tiết.
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

### API 03. `POST /api/v1/categories` — Tạo danh mục mới [CAT-15]
* **Mục đích:** Quản trị viên hoặc nhân viên vận hành tạo mới danh mục (hỗ trợ phân cấp tối đa 3 cấp).
* **Quyền hạn & Xác thực:** `admin`, `staff`.
  * Yêu cầu Header: `x-user-id`, `x-user-roles: admin` hoặc `staff`.
* **Quy tắc Validate (Validation Rules):**
  * `name`: Bắt buộc, không được để trống, độ dài từ 1 đến 255 ký tự (`VAL_INVALID_INPUT`).
  * `description`: Tùy chọn, tối đa 1000 ký tự.
  * `level`: Bắt buộc, giá trị nguyên từ 1 đến 3 (`VAL_INVALID_INPUT`).
  * `parentId`: Nếu cung cấp, danh mục cha phải tồn tại trong cơ sở dữ liệu và có `Level == request.Level - 1`.
* **Chi tiết Request:**
  * **Request Body (JSON):**
    ```json
    {
      "name": "Áo Thun Nam",
      "description": "Các loại áo phông, áo thun nam",
      "parentId": "10000000-0000-0000-0000-000000000002",
      "level": 3,
      "sortOrder": 1
    }
    ```
* **Chi tiết Response:**
  * **Thành công (201 Created):** Trả về `CategoryDto` của bản ghi vừa tạo.
  * **Lỗi (400 Bad Request):** Sai dữ liệu đầu vào hoặc level không hợp lệ (`VAL_INVALID_INPUT`).
  * **Lỗi (404 Not Found):** `parentId` cung cấp không tồn tại (`CATEGORY_NOT_FOUND`).
* **Luồng xử lý (Endpoint Processing Flow):**
  1. Kiểm tra quyền hạn từ header `x-user-roles`.
  2. Validate dữ liệu tên, level và danh mục cha.
  3. Khởi tạo thực thể `Category` mới, gán `CreatedBy = currentUserId`.
  4. Lưu xuống PostgreSQL và ghi Audit Log.
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

### API 04. `PUT /api/v1/categories/{id}` — Cập nhật toàn bộ thông tin danh mục [CAT-16]
* **Mục đích:** Thay thế/cập nhật đầy đủ thông tin danh mục (tên, mô tả, thứ tự sắp xếp, trạng thái ẩn/hiện).
* **Quyền hạn & Xác thực:** `admin`, `staff`.
* **Quy tắc Validate (Validation Rules):**
  * `id`: GUID danh mục phải tồn tại trong database (`CATEGORY_NOT_FOUND`).
  * `name`: Bắt buộc, không được để trống, $\le 255$ ký tự.
  * `level`: Từ 1 đến 3.
  * `isDisabled`: Boolean.
* **Chi tiết Request:**
  * **Path Parameters:** `id` (Guid, Bắt buộc).
  * **Request Body (JSON):**
    ```json
    {
      "name": "Áo Thun Nam Cổ Tròn",
      "description": "Mô tả cập nhật",
      "level": 3,
      "sortOrder": 2,
      "isDisabled": false
    }
    ```
* **Chi tiết Response:**
  * **Thành công (200 OK):** Trả về `CategoryDto` đã cập nhật.
* **Luồng xử lý (Endpoint Processing Flow):**
  1. Tìm danh mục theo `id` (nếu không có $\to$ 404).
  2. Cập nhật các trường thông tin và `UpdatedAt = UtcNow`.
  3. Ghi nhận `AuditLog` với `oldValues` và `newValues`.
  4. Lưu thay đổi xuống database.
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

### API 05. `PATCH /api/v1/categories/{id}` — Cập nhật từng phần danh mục [CAT-16]
* **Mục đích:** Endpoint bí danh (Route alias) chuẩn REST PATCH cho phép sửa đổi danh mục tương đương PUT.
* **Quyền hạn & Xác thực:** `admin`, `staff`.
* **Quy tắc Validate (Validation Rules):** Giống API 04.
* **Chi tiết Request & Response:** Giống API 04.
* **Luồng xử lý:** Giống API 04.
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

### API 06. `DELETE /api/v1/categories/{id}` — Xóa mềm danh mục [CAT-17]
* **Mục đích:** Xóa mềm một danh mục khỏi hệ thống.
* **Quyền hạn & Xác thực:** `admin`, `staff`.
* **Quy tắc Validate (Validation Rules):**
  * Danh mục phải tồn tại (`CATEGORY_NOT_FOUND`).
  * **Ràng buộc toàn vẹn:** Nếu danh mục đang có danh mục con trực tiếp hoặc đang chứa sản phẩm, hệ thống từ chối xóa và trả về `400 Bad Request` (`VAL_INVALID_INPUT`).
* **Chi tiết Request:**
  * **Path Parameters:** `id` (Guid, Bắt buộc).
* **Chi tiết Response:**
  * **Thành công:** `204 No Content`.
* **Luồng xử lý (Endpoint Processing Flow):**
  1. Kiểm tra sự tồn tại của danh mục theo `id`.
  2. Kiểm tra xem có danh mục con nào đang tham chiếu tới `ParentId == id` không.
  3. Đánh dấu `IsDeleted = true`, `DeletedAt = UtcNow`.
  4. Lưu thay đổi.
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

## 📦 PHẦN 3: PHÂN HỆ SẢN PHẨM (PRODUCT MANAGEMENT)

---

### API 07. `GET /api/v1/products` — Tìm kiếm và lọc sản phẩm công khai [CAT-05]
* **Mục đích:** Hiển thị danh sách sản phẩm đang mở bán (`ACTIVE`) cho khách hàng xem trên sàn, có hỗ trợ tìm kiếm, phân trang và sắp xếp.
* **Quyền hạn & Xác thực:** `Public`.
* **Quy tắc Validate (Validation Rules):**
  * `page`: Số nguyên $\ge 1$ (mặc định 1).
  * `pageSize`: Số nguyên từ 1 đến 100 (mặc định 20).
  * `minPrice`, `maxPrice`: Số thực $\ge 0$. Nếu cả hai đều có thì `minPrice <= maxPrice`.
  * `sortBy`: Chỉ chấp nhận các giá trị: `price_asc`, `price_desc`, `rating_desc`, `newest`.
* **Chi tiết Request:**
  * **Query Parameters:** `keyword`, `categoryId`, `minPrice`, `maxPrice`, `saleType`, `sortBy`, `page`, `pageSize`.
* **Chi tiết Response:**
  * **Thành công (200 OK):**
    ```json
    {
      "items": [
        {
          "productId": "88888888-8888-8888-8888-888888888888",
          "name": "Áo Thun Cotton Nexus",
          "brand": "Nexus Fashion",
          "saleType": "Regular",
          "status": "Active",
          "minPrice": 150000,
          "maxPrice": 200000,
          "primaryImageUrl": "https://res.cloudinary.com/.../ao-thun.jpg",
          "avgRating": 4.8
        }
      ],
      "page": 1,
      "pageSize": 20,
      "totalItems": 1,
      "totalPages": 1
    }
    ```
* **Luồng xử lý (Endpoint Processing Flow):**
  1. Truy vấn các sản phẩm thỏa mãn `Status == ProductStatus.Active` và `IsDeleted == false`.
  2. Nạp kèm bảng `Variants` (tính khoảng giá minPrice - maxPrice) và bảng `Images` (lấy ảnh có `IsActive == true`).
  3. Áp dụng điều kiện lọc theo từ khóa, danh mục, giá, và sắp xếp theo `sortBy`.
  4. Phân trang và trả về `PagedData<ProductListItemDto>`.
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác** (Tính toán trực tiếp trong DB Catalog).

---

### API 08. `GET /api/v1/products/{id}` — Xem chi tiết sản phẩm công khai [CAT-06]
* **Mục đích:** Người mua bấm vào xem trang chi tiết: hiển thị thông tin sản phẩm, danh sách biến thể đang mở bán, toàn bộ ảnh sản phẩm, danh mục và điểm đánh giá sao.
* **Quyền hạn & Xác thực:** `Public`.
* **Quy tắc Validate (Validation Rules):**
  * `id`: GUID sản phẩm phải tồn tại và `IsDeleted == false`.
* **Chi tiết Request:**
  * **Path Parameters:** `id` (Guid, Bắt buộc).
* **Chi tiết Response:**
  * **Thành công (200 OK):**
    ```json
    {
      "productId": "88888888-8888-8888-8888-888888888888",
      "sellerId": "99999999-9999-9999-9999-999999999999",
      "name": "Áo Thun Cotton Nexus",
      "description": "Chất liệu 100% cotton thoáng mát",
      "brand": "Nexus Fashion",
      "saleType": "Regular",
      "status": "Active",
      "publishedAt": "2026-11-01T08:00:00Z",
      "category": {
        "categoryId": "10000000-0000-0000-0000-000000000003",
        "parentId": "10000000-0000-0000-0000-000000000002",
        "name": "Áo Thun Nam",
        "level": 3
      },
      "variants": [
        {
          "variantId": "77777777-7777-7777-7777-777777777777",
          "skuCode": "ATN-DEN-M",
          "name": "Màu Đen - Size M",
          "price": 150000,
          "status": "Active",
          "isActive": true,
          "attributes": [
            { "attrName": "Color", "attrValue": "Đen" },
            { "attrName": "Size", "attrValue": "M" }
          ]
        }
      ],
      "images": [
        {
          "imageId": "66666666-6666-6666-6666-666666666666",
          "url": "https://res.cloudinary.com/.../img1.jpg",
          "altText": "Ảnh chính áo thun",
          "isActive": true,
          "sortOrder": 0,
          "variantId": null
        }
      ],
      "rating": {
        "avgRating": 4.8,
        "reviewCount": 12
      }
    }
    ```
  * **Lỗi (404 Not Found):** `PRODUCT_NOT_FOUND`.
* **Luồng xử lý (Endpoint Processing Flow):**
  1. Kiểm tra tồn tại sản phẩm.
  2. Eager loading Category, Variants (chỉ lấy variant `Active`), Images (sắp xếp theo `SortOrder`).
  3. Trả về `ProductDetailDto`.
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

### API 09. `GET /api/v1/seller/products` — Lấy danh sách sản phẩm của Seller [CAT-44]
* **Mục đích:** Kênh quản lý của Người bán (Seller Dashboard) quản lý tất cả sản phẩm của chính mình (bao gồm cả DRAFT, ACTIVE, INACTIVE).
* **Quyền hạn & Xác thực:** `seller`, `admin`.
  * Yêu cầu Header: `x-user-id` (GUID người bán).
* **Quy tắc Validate (Validation Rules):**
  * `status`: Tùy chọn, chỉ chấp nhận `Draft`, `Active`, `Inactive`.
  * `page` $\ge 1$, `pageSize` $\ge 1$.
* **Chi tiết Request:**
  * **Headers:** `x-user-id` (Guid, Bắt buộc).
  * **Query Parameters:** `keyword`, `status`, `page`, `pageSize`.
* **Chi tiết Response:**
  * **Thành công (200 OK):** Trả về `PagedData<SellerProductListItemDto>` kèm các trường `variantCount`, `hasOrder`, `createdAt`.
* **Luồng xử lý (Endpoint Processing Flow):**
  1. Lấy `currentUserId` từ header `x-user-id`.
  2. Lọc sản phẩm có `SellerId == currentUserId` và `IsDeleted == false`.
  3. Đếm số lượng variants và cờ `hasOrder` của từng sản phẩm.
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

### API 10. `GET /api/v1/seller/products/{id}` — Lấy chi tiết sản phẩm dành cho Seller [CAT-45]
* **Mục đích:** Phục vụ màn hình chỉnh sửa chi tiết sản phẩm của Seller (xem toàn bộ SKU kể cả SKU đã ẩn, lịch sử `hasOrder`, và toàn bộ ảnh).
* **Quyền hạn & Xác thực:** `seller`, `admin`.
* **Quy tắc Validate (Validation Rules):**
  * `id`: GUID sản phẩm.
  * **Kiểm tra sở hữu:** Sản phẩm phải thuộc quyền sở hữu của `currentUserId` (`PERMISSION_DENIED` - HTTP 403 nếu cố truy cập sản phẩm shop khác).
* **Chi tiết Request:**
  * **Headers:** `x-user-id` (Guid, Bắt buộc).
  * **Path Parameters:** `id` (Guid, Bắt buộc).
* **Chi tiết Response:**
  * **Thành công (200 OK):** Trả về `SellerProductDetailDto`.
  * **Lỗi (403 Forbidden):** `PERMISSION_DENIED`.
  * **Lỗi (404 Not Found):** `PRODUCT_NOT_FOUND`.
* **Luồng xử lý (Endpoint Processing Flow):**
  1. Kiểm tra tồn tại và so sánh `product.SellerId == currentUserId`.
  2. Nạp toàn bộ danh sách variants (cả active lẫn inactive) và danh sách ảnh.
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

### API 11. `POST /api/v1/products` — Tạo mới sản phẩm thường (REGULAR) [CAT-01]
* **Mục đích:** Tạo mới sản phẩm bán giá cố định. Hỗ trợ **cả 2 luồng:** Luồng 1 bước tạo gộp (Single-step atomic kèm `variants`) hoặc Luồng nhiều bước (Multi-step tạo nháp trước).
* **Quyền hạn & Xác thực:** `seller`, `admin`.
  * Header bắt buộc: `x-user-id` (gán vào `SellerId`).
* **Quy tắc Validate (Validation Rules):**
  * `name`: Bắt buộc, không được để trống, $\le 255$ ký tự (`VAL_INVALID_INPUT`).
  * `brand`: Tùy chọn, $\le 150$ ký tự.
  * `description`: Tùy chọn, $\le 4000$ ký tự.
  * `categoryId`: Bắt buộc, phải tồn tại và **bắt buộc phải là danh mục lá (Leaf Category)** — tức danh mục không có danh mục con bên dưới (`VAL_INVALID_INPUT`).
  * Nếu có mảng `variants`:
    * Tối đa 10 biến thể (`MAX_VARIANTS_REACHED`).
    * Mã `skuCode` không được để trống, không được trùng lặp trong mảng và không được trùng với bất kỳ SKU nào đã có trong shop của Seller (`409 DUPLICATE_SKU`).
    * Giá biến thể `price` phải $> 0$.
* **Chi tiết Request:**
  * **Request Body (JSON Single-step):**
    ```json
    {
      "name": "Áo Khoác Gió Nam Nexus",
      "categoryId": "10000000-0000-0000-0000-000000000003",
      "brand": "Nexus",
      "description": "Áo gió chống nước 2 lớp",
      "saleType": "Regular",
      "variants": [
        {
          "skuCode": "AKG-XANH-L",
          "name": "Xanh Navy - L",
          "price": 320000,
          "currency": "VND",
          "attributes": [
            { "attrName": "Color", "attrValue": "Xanh Navy" },
            { "attrName": "Size", "attrValue": "L" }
          ]
        }
      ]
    }
    ```
* **Chi tiết Response:**
  * **Thành công (201 Created):**
    ```json
    {
      "id": "88888888-8888-8888-8888-888888888888",
      "sellerId": "99999999-9999-9999-9999-999999999999",
      "categoryId": "10000000-0000-0000-0000-000000000003",
      "name": "Áo Khoác Gió Nam Nexus",
      "description": "Áo gió chống nước 2 lớp",
      "brand": "Nexus",
      "status": "Draft"
    }
    ```
* **Luồng xử lý (Endpoint Processing Flow):**
  1. Validate tên, danh mục lá, độ dài các trường.
  2. Tạo bản ghi `Product` với `Status = ProductStatus.Draft`.
  3. Nếu có gửi kèm `variants`: Kiểm tra trùng SKU trong shop, khởi tạo toàn bộ danh sách `ProductVariant` và `Attributes` trong **cùng 1 Database Transaction nguyên tử**. Variant đầu tiên tự động gán `IsActive = true`.
  4. Ghi `AuditLog` tạo sản phẩm.
  5. Commit transaction vào database.
  6. **Gọi bất đồng bộ sang Fulfillment Service** để khởi tạo sổ cái tồn kho.
* **Tích hợp liên Service (External Calls):**
  * **Có gọi service khác:**
    * **Service đích:** `FulfillmentService` (`http://localhost:8006`).
    * **Endpoint gọi:** `POST /api/v1/inventories/initialize`.
    * **Payload:** `{ "productId": "...", "skuId": null, "initialQuantity": 0 }`.
    * **Cơ chế xử lý khi lỗi:** Bọc trong khối `try-catch` (Non-fatal). Nếu `FulfillmentService` mất kết nối hoặc trả về lỗi, hệ thống chỉ ghi nhận log cảnh báo và **vẫn hoàn thành việc tạo Product**, không rollback dữ liệu của Seller.

---

### API 12. `POST /api/v1/products/auction` — Tạo mới sản phẩm Đấu giá [CAT-43]
* **Mục đích:** Tạo sản phẩm tham gia phiên đấu giá trực tuyến (`SaleType = Auction`).
* **Quyền hạn & Xác thực:** `seller`, `admin`.
* **Quy tắc Validate (Validation Rules):**
  * Tên $\le 255$ ký tự, `categoryId` phải là danh mục lá.
  * **Đặc thù đấu giá:** Không nhận mảng `variants` từ phía client. Hệ thống tự động khởi tạo đúng 1 biến thể SKU mặc định với `Price = null`, vì giá khởi điểm và bước giá do `AuctionService` toàn quyền quản lý.
* **Chi tiết Request:**
  * **Request Body (JSON):**
    ```json
    {
      "name": "Tranh Sơn Dầu Nghệ Thuật Cổ",
      "categoryId": "10000000-0000-0000-0000-000000000009",
      "brand": "Nexus Antique",
      "description": "Tác phẩm độc bản",
      "specifications": "Kích thước 80x120cm"
    }
    ```
* **Chi tiết Response:**
  * **Thành công (201 Created):** Trả về `ProductDto` với trạng thái `Draft`.
* **Luồng xử lý (Endpoint Processing Flow):**
  1. Kiểm tra tính hợp lệ của danh mục lá.
  2. Tạo `Product` (`SaleType = Auction`, `Status = Draft`).
  3. Tự động sinh 1 `ProductVariant` duy nhất (`SkuCode = AUC-...`, `Price = null`, `IsActive = true`).
  4. Lưu xuống PostgreSQL.
  5. Gọi sang Fulfillment Service để init tồn kho cho SKU đấu giá.
* **Tích hợp liên Service (External Calls):**
  * **Có gọi service khác:**
    * **Service đích:** `FulfillmentService` (`http://localhost:8006`).
    * **Endpoint gọi:** `POST /api/v1/inventories/initialize`.
    * **Payload:** `{ "productId": "...", "skuId": "variantId", "initialQuantity": 0 }`.
    * **Xử lý khi lỗi:** Non-fatal try-catch (log warning, không gián đoạn luồng).

---

### API 13. `PUT /api/v1/products/{id}` — Cập nhật thông tin cơ bản sản phẩm [CAT-02]
* **Mục đích:** Sửa đổi tên, thương hiệu, mô tả, thông số hoặc danh mục của sản phẩm.
* **Quyền hạn & Xác thực:** `seller`, `admin` (Chủ sở hữu).
* **Quy tắc Validate (Validation Rules):**
  * Sản phẩm phải thuộc về `currentUserId` (`PERMISSION_DENIED`).
  * `categoryId`: Nếu thay đổi, danh mục mới phải là danh mục lá.
  * Tên không để trống, $\le 255$ ký tự.
* **Chi tiết Request:**
  * **Path Parameters:** `id` (Guid, Bắt buộc).
  * **Request Body (JSON):**
    ```json
    {
      "name": "Áo Khoác Gió Nam Cao Cấp Nexus",
      "categoryId": "10000000-0000-0000-0000-000000000003",
      "brand": "Nexus Premium",
      "description": "Mô tả đã được cập nhật...",
      "specifications": "Vải trượt nước"
    }
    ```
* **Chi tiết Response:**
  * **Thành công (200 OK):** Trả về `ProductDto` cập nhật.
* **Luồng xử lý (Endpoint Processing Flow):**
  1. Kiểm tra tồn tại và quyền sở hữu.
  2. Validate danh mục mới (nếu có).
  3. Ghi nhận audit log thay đổi và lưu database.
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

### API 14. `PATCH /api/v1/products/{id}` — Cập nhật thông tin sản phẩm (Bí danh PATCH) [CAT-02]
* **Mục đích:** Hỗ trợ gọi cập nhật sản phẩm bằng phương thức `PATCH` chuẩn REST.
* **Quy tắc & Luồng xử lý:** Giống API 13.
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

### API 15. `PATCH /api/v1/products/{id}/status` — Đổi trạng thái sản phẩm [CAT-03]
* **Mục đích:** Xuất bản sản phẩm lên sàn (`ACTIVE`), tạm dừng bán (`INACTIVE`), hoặc chuyển về bản nháp (`DRAFT`).
* **Quyền hạn & Xác thực:** `seller`, `admin` (Chủ sở hữu).
* **Quy tắc Validate & Guardrails Nghiêm ngặt:**
  * `status`: Bắt buộc, chỉ nhận `DRAFT`, `ACTIVE`, `INACTIVE`.
  * **Điều kiện bắt buộc để chuyển sang `ACTIVE`:**
    1. Sản phẩm phải có ít nhất **1 biến thể đang `ACTIVE`** và có giá bán $> 0$ (với sản phẩm thường).
    2. Sản phẩm phải có ít nhất **1 hình ảnh đã tải lên** và mang cờ đại diện chính `IsActive = true`.
    $\implies$ Nếu vi phạm: Từ chối và trả về lỗi `400 Bad Request` (`VAL_INVALID_INPUT`).
* **Chi tiết Request:**
  * **Path Parameters:** `id` (Guid, Bắt buộc).
  * **Request Body (JSON):**
    ```json
    {
      "status": "ACTIVE"
    }
    ```
* **Chi tiết Response:**
  * **Thành công (200 OK):** Trả về `ProductDto` kèm thời gian `PublishedAt = UtcNow`.
  * **Lỗi (400 Bad Request):** Thiếu SKU hợp lệ hoặc thiếu hình ảnh (`VAL_INVALID_INPUT`).
* **Luồng xử lý (Endpoint Processing Flow):**
  1. Kiểm tra quyền sở hữu sản phẩm.
  2. Nếu `newStatus == Active`: Kiểm tra danh sách variants và images.
  3. Nếu đủ điều kiện: Cập nhật `Status = Active`, gán `PublishedAt`, ghi Audit Log.
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

### API 16. `DELETE /api/v1/products/{id}` — Xóa mềm sản phẩm [CAT-04]
* **Mục đích:** Đánh dấu xóa mềm sản phẩm và xóa mềm toàn bộ các biến thể thuộc sản phẩm đó.
* **Quyền hạn & Xác thực:** `seller`, `admin` (Chủ sở hữu).
* **Quy tắc Validate (Validation Rules):**
  * Phải là Seller chủ sở hữu (`PERMISSION_DENIED`).
* **Chi tiết Request:**
  * **Path Parameters:** `id` (Guid, Bắt buộc).
* **Chi tiết Response:**
  * **Thành công:** `204 No Content`.
* **Luồng xử lý (Endpoint Processing Flow):**
  1. Tìm sản phẩm kèm danh sách biến thể.
  2. Gán `product.IsDeleted = true`, `DeletedAt = UtcNow`.
  3. Lặp qua tất cả `Variants` gán `IsDeleted = true`.
  4. Ghi Audit Log xóa sản phẩm.
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

## 🏷️ PHẦN 4: PHÂN HỆ BIẾN THỂ / SKU (VARIANT MANAGEMENT)

---

### API 17. `GET /api/v1/variants/{id}` — Xem chi tiết một biến thể SKU [CAT-42]
* **Mục đích:** Tra cứu cấu hình giá, mã SKU, bộ thuộc tính dynamic (`Color`, `Size`,...) và trạng thái của 1 biến thể cụ thể.
* **Quyền hạn & Xác thực:** `Public` / `Seller`.
* **Chi tiết Request:**
  * **Path Parameters:** `id` (Guid của Variant).
* **Chi tiết Response:**
  * **Thành công (200 OK):**
    ```json
    {
      "id": "77777777-7777-7777-7777-777777777777",
      "productId": "88888888-8888-8888-8888-888888888888",
      "skuCode": "AKG-XANH-L",
      "name": "Xanh Navy - L",
      "price": 320000,
      "currency": "VND",
      "isActive": true,
      "hasOrder": false,
      "status": "Active",
      "statusReason": null,
      "attributes": [
        { "attrName": "Color", "attrValue": "Xanh Navy" },
        { "attrName": "Size", "attrValue": "L" }
      ]
    }
    ```
  * **Lỗi (404 Not Found):** `VARIANT_NOT_FOUND`.
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

### API 18. `POST /api/v1/products/{id}/variants` — Thêm biến thể cho sản phẩm [CAT-07]
* **Mục đích:** Thêm biến thể SKU mới vào sản phẩm đã có (phục vụ luồng tạo sản phẩm từng bước - Multi-step).
* **Quyền hạn & Xác thực:** `seller`, `admin` (Chủ sở hữu).
* **Quy tắc Validate (Validation Rules):**
  * Không cho phép thêm biến thể vào sản phẩm Đấu giá (`AUCTION`) (`VAL_INVALID_INPUT`).
  * **Giới hạn số lượng:** Mỗi sản phẩm chỉ được có tối đa **10 biến thể SKU** (`MAX_VARIANTS_REACHED`).
  * `skuCode`: Bắt buộc, không được để trống, không được trùng với bất kỳ SKU nào trong gian hàng của Seller (`409 DUPLICATE_SKU`).
  * `price`: Bắt buộc $> 0$.
* **Chi tiết Request:**
  * **Path Parameters:** `id` (Guid của Product).
  * **Request Body (JSON):**
    ```json
    {
      "skuCode": "AKG-DO-XL",
      "name": "Màu Đỏ - Size XL",
      "price": 340000,
      "currency": "VND",
      "attributes": [
        { "attrName": "Color", "attrValue": "Đỏ" },
        { "attrName": "Size", "attrValue": "XL" }
      ]
    }
    ```
* **Chi tiết Response:**
  * **Thành công (201 Created):** Trả về `VariantDetailDto`.
  * **Lỗi (409 Conflict):** `DUPLICATE_SKU`.
  * **Lỗi (400 Bad Request):** Quá 10 variants (`MAX_VARIANTS_REACHED`).
* **Luồng xử lý (Endpoint Processing Flow):**
  1. Kiểm tra quyền sở hữu sản phẩm.
  2. Đếm số variant hiện tại $< 10$.
  3. Kiểm tra trùng `SkuCode` trong phạm vi gian hàng của Seller đó.
  4. Tạo `ProductVariant` và lưu thuộc tính `Attributes`.
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

### API 19. `PUT /api/v1/variants/{id}` — Cập nhật thông tin biến thể [CAT-08]
* **Mục đích:** Sửa tên biến thể, điều chỉnh giá bán và cập nhật bộ thuộc tính.
* **Quyền hạn & Xác thực:** `seller`, `admin` (Chủ sở hữu).
* **Quy tắc Validate (Validation Rules):**
  * `price`: Bắt buộc $> 0$.
  * Tên biến thể không được để trống.
* **Chi tiết Request:**
  * **Path Parameters:** `id` (Guid của Variant).
  * **Request Body (JSON):**
    ```json
    {
      "name": "Màu Đỏ Đô - Size XL",
      "price": 350000,
      "currency": "VND",
      "attributes": [
        { "attrName": "Color", "attrValue": "Đỏ Đô" },
        { "attrName": "Size", "attrValue": "XL" }
      ]
    }
    ```
* **Chi tiết Response:**
  * **Thành công (200 OK):** Trả về `VariantDetailDto` đã cập nhật.
* **Luồng xử lý (Endpoint Processing Flow):**
  1. Kiểm tra tồn tại và quyền sở hữu qua `variant.Product.SellerId`.
  2. Cập nhật giá và tên. Thay thế danh sách `Attributes`.
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

### API 20. `PATCH /api/v1/variants/{id}` — Cập nhật biến thể (Bí danh PATCH) [CAT-08]
* **Mục đích:** Hỗ trợ gọi cập nhật biến thể bằng phương thức `PATCH` chuẩn REST.
* **Quy tắc & Luồng xử lý:** Giống API 19.
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

### API 21. `PUT /api/v1/products/{id}/default-variant` — Đặt biến thể mặc định [CAT-10]
* **Mục đích:** Chỉ định SKU nào sẽ làm SKU đại diện hiển thị giá chính khi khách bấm vào sản phẩm.
* **Quyền hạn & Xác thực:** `seller`, `admin`.
* **Chi tiết Request:**
  * **Path Parameters:** `id` (Guid của Product).
  * **Request Body (JSON):**
    ```json
    {
      "variantId": "77777777-7777-7777-7777-777777777777"
    }
    ```
* **Chi tiết Response:**
  * **Thành công (200 OK):** Trả về `VariantDetailDto` của biến thể vừa được chọn làm mặc định.
* **Luồng xử lý (Endpoint Processing Flow):**
  1. Kiểm tra variant có thuộc về sản phẩm này không.
  2. Gán `isActive = true` cho variant được chọn.
  3. Gán `isActive = false` cho toàn bộ các variants còn lại của sản phẩm.
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

### API 22. `PATCH /api/v1/variants/{id}/status` — Đổi trạng thái biến thể [CAT-09]
* **Mục đích:** Ẩn hoặc mở bán một SKU cụ thể (ví dụ: SKU hết hàng thì tạm thời chuyển sang `INACTIVE`).
* **Quyền hạn & Xác thực:** `seller`, `admin`.
* **Request Body (JSON):**
  ```json
  {
    "status": "INACTIVE",
    "statusReason": "SellerPaused"
  }
  ```
* **Chi tiết Response:**
  * **Thành công (200 OK):** Trả về `VariantDetailDto`.
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

### API 23. `DELETE /api/v1/variants/{id}` — Xóa mềm biến thể [CAT-11]
* **Mục đích:** Xóa biến thể khỏi sản phẩm.
* **Quyền hạn & Xác thực:** `seller`, `admin`.
* **Ràng buộc nghiệp vụ bảo vệ:**
  * **Chặn xóa khi đã có đơn hàng:** Nếu biến thể đã từng phát sinh đơn hàng (`hasOrder == true`), hệ thống từ chối xóa và bắt buộc chuyển sang `INACTIVE` (`VAL_INVALID_INPUT`).
  * **Chặn xóa biến thể cuối cùng:** Nếu sản phẩm chỉ còn duy nhất 1 biến thể thì không được xóa.
* **Chi tiết Response:**
  * **Thành công:** `204 No Content`.
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

## 🖼️ PHẦN 5: PHÂN HỆ HÌNH ẢNH SẢN PHẨM (PRODUCT IMAGES)

---

### API 24. `GET /api/v1/products/{id}/images` — Lấy danh sách ảnh sản phẩm [CAT-40]
* **Mục đích:** Lấy toàn bộ danh sách ảnh của sản phẩm theo thứ tự hiển thị `sortOrder`.
* **Quyền hạn & Xác thực:** `Public` / `Seller`.
* **Path Parameters:** `id` (Guid của Product).
* **Chi tiết Response:**
  * **Thành công (200 OK):**
    ```json
    [
      {
        "id": "66666666-6666-6666-6666-666666666666",
        "productId": "88888888-8888-8888-8888-888888888888",
        "variantId": null,
        "url": "https://res.cloudinary.com/.../img1.jpg",
        "altText": "Ảnh mặt trước áo khoác",
        "isActive": true,
        "sortOrder": 0
      }
    ]
    ```
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

### API 25. `POST /api/v1/products/{id}/images` — Tải lên hình ảnh sản phẩm [CAT-12]
* **Mục đích:** Tải file hình ảnh lên dịch vụ Cloudinary CDN và lưu thông tin ảnh vào sản phẩm.
* **Quyền hạn & Xác thực:** `seller`, `admin` (Chủ sở hữu sản phẩm).
* **Định dạng Request:** `multipart/form-data`.
* **Quy tắc Validate (Validation Rules):**
  * `file`: Bắt buộc, không được để trống, kích thước $> 0$ byte.
  * **Giới hạn số lượng ảnh:** Mỗi sản phẩm chỉ được có tối đa **5 hình ảnh** (`VAL_LIMIT_EXCEEDED`).
  * `variantId`: Nếu có truyền, phải là biến thể thuộc về sản phẩm này.
* **Chi tiết Request (Form Data):**
  * `file`: (Binary File, Bắt buộc).
  * `variantId`: (Guid, Tùy chọn).
  * `altText`: (String, Tùy chọn).
  * `sortOrder`: (Short, Mặc định `0`).
* **Chi tiết Response:**
  * **Thành công (201 Created):**
    ```json
    {
      "id": "66666666-6666-6666-6666-666666666666",
      "productId": "88888888-8888-8888-8888-888888888888",
      "variantId": null,
      "url": "https://res.cloudinary.com/u6yxkkop/image/upload/v1/products/xyz.jpg",
      "altText": "Ảnh mặt trước áo khoác",
      "isActive": true,
      "sortOrder": 0
    }
    ```
* **Luồng xử lý (Endpoint Processing Flow):**
  1. Kiểm tra quyền sở hữu sản phẩm và số lượng ảnh hiện tại $< 5$.
  2. Đọc file stream và gọi `_imageStorageService.UploadAsync` đẩy lên Cloudinary.
  3. Nhận về URL CDN công khai.
  4. Nếu là ảnh đầu tiên của sản phẩm: tự động đánh dấu `IsActive = true` (ảnh đại diện chính).
  5. Lưu bản ghi `ProductImage` vào database.
* **Tích hợp liên Service (External Calls):**
  * **Có gọi service bên thứ 3:**
    * **Dịch vụ:** **Cloudinary Media API** (qua CloudinaryDotNet SDK).
    * **Mục đích:** Lưu trữ file ảnh vật lý trên mạng phân phối nội dung (CDN), tối ưu hóa dung lượng và phân giải.
    * **Xử lý khi lỗi:** Nếu quá trình upload lên Cloudinary gặp lỗi mạng hoặc lỗi API Key, hệ thống sẽ ném ngoại lệ dừng lại ngay lập tức, không lưu bản ghi ảnh rỗng vào cơ sở dữ liệu.

---

### API 26. `PATCH /api/v1/images/{id}` — Cập nhật ảnh đại diện chính & Thứ tự [CAT-14]
* **Mục đích:** Đổi ảnh đại diện chính (`isActive`), số thứ tự hiển thị (`sortOrder`) hoặc mô tả `altText`.
* **Quyền hạn & Xác thực:** `seller`, `admin`.
* **Quy tắc nghiệp vụ cốt lõi:**
  * Khi đặt `isActive = true` cho ảnh này, hệ thống sẽ **tự động gỡ cờ `isActive = false` ở toàn bộ các ảnh khác** của sản phẩm đó, đảm bảo nguyên tắc: **Mỗi sản phẩm luôn chỉ có DUY NHẤT 1 ảnh đại diện chính**.
* **Chi tiết Request:**
  * **Path Parameters:** `id` (Guid của Image).
  * **Request Body (JSON):**
    ```json
    {
      "isActive": true,
      "sortOrder": 1,
      "altText": "Ảnh góc nghiêng cập nhật"
    }
    ```
* **Chi tiết Response:**
  * **Thành công (200 OK):** Trả về `ProductImageDto`.
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

### API 27. `DELETE /api/v1/images/{id}` — Xóa ảnh sản phẩm theo ID ảnh [CAT-13]
* **Mục đích:** Xóa mềm một ảnh theo ID ảnh (Endpoint độc lập tại Images Controller).
* **Quyền hạn & Xác thực:** `seller`, `admin` (Chủ sở hữu).
* **Quy tắc chuyển giao đại diện tự động:**
  * Nếu xóa đúng ảnh đang là đại diện chính (`IsActive == true`), hệ thống sẽ **tự động tìm ảnh kế tiếp có `SortOrder` nhỏ nhất** của sản phẩm đó để chuyển cờ `IsActive = true`, tránh tình trạng sản phẩm bị mất ảnh đại diện.
* **Chi tiết Request:**
  * **Path Parameters:** `id` (Guid của Image).
* **Chi tiết Response:**
  * **Thành công:** `204 No Content`.
* **Luồng xử lý (Endpoint Processing Flow):**
  1. Tìm ảnh theo `id`, kiểm tra quyền sở hữu của người bán.
  2. Gán `image.IsDeleted = true`.
  3. Nếu `image.IsActive == true`: Tìm ảnh kế tiếp chưa xóa và cập nhật `nextImage.IsActive = true`.
  4. Lưu thay đổi xuống database.
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

### API 28. `DELETE /api/v1/products/{id}/images/{imageId}` — Xóa ảnh theo route lồng
* **Mục đích:** Cung cấp thêm route xóa ảnh có kèm `productId` trong URL (Bổ trợ tương thích).
* **Quy tắc & Luồng xử lý:** Giống API 27 (Tự động chuyển cờ đại diện cho ảnh kế tiếp nếu xóa ảnh chính).
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

## 🔗 PHẦN 6: PHÂN HỆ TÍCH HỢP LIÊN SERVICE (INTERNAL APIS)

---

### API 29. `POST /api/v1/internal/variants` — Lấy danh sách biến thể hàng loạt
* **Mục đích:** Cung cấp cho `OrderService`, `CartService`, `AuctionService` truy vấn nhanh thông tin chi tiết (tên sản phẩm, giá bán, ảnh chính, thuộc tính) của nhiều SKU cùng lúc.
* **Quyền hạn & Xác thực:** `Internal` (Gọi giữa các microservices).
* **Chi tiết Request:**
  * **Request Body (JSON):**
    ```json
    {
      "variantIds": [
        "77777777-7777-7777-7777-777777777777",
        "77777777-7777-7777-7777-777777777778"
      ]
    }
    ```
* **Chi tiết Response:**
  * **Thành công (200 OK):**
    ```json
    {
      "items": [
        {
          "variantId": "77777777-7777-7777-7777-777777777777",
          "productId": "88888888-8888-8888-8888-888888888888",
          "skuCode": "AKG-XANH-L",
          "variantName": "Xanh Navy - L",
          "productName": "Áo Khoác Gió Nam Nexus",
          "price": 320000,
          "currency": "VND",
          "status": "Active",
          "productStatus": "Active",
          "sellerId": "99999999-9999-9999-9999-999999999999",
          "saleType": "Regular",
          "primaryImageUrl": "https://res.cloudinary.com/.../img1.jpg",
          "attributes": [
            { "attrName": "Color", "attrValue": "Xanh Navy" }
          ]
        }
      ],
      "notFoundIds": []
    }
    ```
* **Luồng xử lý (Endpoint Processing Flow):**
  1. Tiếp nhận danh sách `variantIds`.
  2. Truy vấn hàng loạt từ bảng `ProductVariants`, kết hợp nạp thông tin `Product`, `Attributes` và ảnh đại diện chính của sản phẩm.
  3. Phân loại các ID tìm thấy và các ID không tồn tại vào `notFoundIds`.
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác** (Đây là endpoint đóng vai trò Server phục vụ cho các service khác).

---

### API 30. `POST /api/v1/internal/variants/check-status` — Kiểm tra tính hợp lệ của biến thể
* **Mục đích:** `OrderService` gọi trước khi tạo đơn hàng để kiểm tra xem sản phẩm và biến thể có thỏa mãn điều kiện mua sắm hay không (`isPurchasable`).
* **Quyền hạn & Xác thực:** `Internal`.
* **Chi tiết Request:**
  * **Request Body (JSON):**
    ```json
    {
      "variantIds": [
        "77777777-7777-7777-7777-777777777777"
      ],
      "purpose": "ORDER"
    }
    ```
* **Chi tiết Response:**
  * **Thành công (200 OK):**
    ```json
    {
      "items": [
        {
          "variantId": "77777777-7777-7777-7777-777777777777",
          "productId": "88888888-8888-8888-8888-888888888888",
          "productStatus": "Active",
          "variantStatus": "Active",
          "saleType": "Regular",
          "isPurchasable": true,
          "reason": "OK",
          "price": 320000,
          "currency": "VND"
        }
      ],
      "notFoundIds": []
    }
    ```
* **Quy tắc xác định `isPurchasable`:**
  * Biến thể được xem là hợp lệ để mua sắm (`isPurchasable = true`) khi và chỉ khi:
    1. Sản phẩm tồn tại, chưa bị xóa và có `Status == Active`.
    2. Biến thể tồn tại, chưa bị xóa và có `Status == Active`.
    3. Đối với sản phẩm thường (`Regular`): Giá bán `price` phải $> 0$.
  * Nếu một trong các điều kiện trên không thỏa mãn: `isPurchasable = false` kèm lý do cụ thể trong trường `reason` (ví dụ: `Product is not active`, `Variant is inactive`, `Price is invalid`).
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.
