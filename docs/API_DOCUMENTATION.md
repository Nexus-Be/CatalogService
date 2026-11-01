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
* `Authorization` (`String`, Bắt buộc với API phân quyền): `Bearer <jwt-token>` (Token đăng nhập của người dùng).
* `x-user-id` (`Guid`, Bắt buộc với Seller/Admin): GUID định danh người dùng.
* `x-user-roles` (`String`, Bắt buộc với Seller/Admin): Quyền hạn (`seller`, `admin`, `staff`, `buyer`).

### 2. Định dạng phản hồi lỗi chuẩn (RFC 9110 Problem Details)
Tất cả các lỗi trả về từ hệ thống đều tuân thủ chuẩn `application/problem+json`:
* `code` (`String`): Mã lỗi hệ thống dạng SCREAMING_SNAKE_CASE (ví dụ: `VAL_INVALID_INPUT`).
* `detail` (`String`): Thông điệp mô tả chi tiết nguyên nhân lỗi viết bằng tiếng Anh chuẩn.
* `status` (`Integer`): Mã trạng thái HTTP (ví dụ: `400`, `401`, `403`, `404`, `409`).
* `title` (`String`): Tiêu đề ngắn gọn của nhóm lỗi HTTP (ví dụ: `Bad Request`, `Not Found`).
* `instance` (`String`): URL của endpoint vừa phát sinh lỗi (ví dụ: `/api/v1/products`).

```json
{
  "code": "VAL_INVALID_INPUT",
  "detail": "Product name cannot exceed 255 characters.",
  "status": 400,
  "title": "Bad Request",
  "instance": "/api/v1/products"
}
```

---

## 📁 PHẦN 2: PHÂN HỆ DANH MỤC (CATEGORY MANAGEMENT)

---

### API 01. `GET /api/v1/categories` — Lấy danh sách danh mục
* **Mã nghiệp vụ:** Không gắn mã (Truy vấn danh mục)
* **Mục đích:** Người mua và người bán tra cứu danh mục hàng hóa trên sàn theo dạng phẳng hoặc cây 3 cấp.
* **Quyền hạn & Xác thực:** `Public` (Không yêu cầu đăng nhập).
* **Quy tắc Validate (Validation Rules):**
  * `parentId`: Đúng định dạng GUID nếu có truyền.
  * `tree`: Kiểu Boolean (`true`/`false`).
  * `keyword`: Chuỗi tìm kiếm, tự động cắt khoảng trắng đầu cuối (Trim).
* **Chi tiết Request:**
  * **Headers:** Không yêu cầu.
  * **Query Parameters:**
    * `parentId` (`Guid?`, Tùy chọn): Lọc các danh mục con trực tiếp thuộc danh mục cha này.
    * `tree` (`Boolean`, Mặc định `false`): Nếu `true`, trả về cây phân cấp lồng nhau. Nếu `false`, trả về danh sách phẳng.
    * `keyword` (`String?`, Tùy chọn): Tìm kiếm danh mục theo tên.
* **Chi tiết Response (200 OK):**
  * **Kiểu dữ liệu mảng đối tượng `CategoryItemDto`:**
    * `categoryId` (`Guid`): ID định danh danh mục.
    * `parentId` (`Guid?`): ID danh mục cha (null nếu là cấp 1).
    * `name` (`String`): Tên danh mục.
    * `description` (`String?`): Mô tả danh mục.
    * `level` (`Short`): Cấp độ phân cấp (1, 2, hoặc 3).
    * `sortOrder` (`Integer`): Số thứ tự hiển thị ưu tiên.
    * `children` (`Array of CategoryItemDto`): Danh sách danh mục con (chỉ có khi `tree = true`).
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
  * **Path Parameters:**
    * `id` (`Guid`, Bắt buộc): ID định danh danh mục.
* **Chi tiết Response (200 OK):**
  * **Kiểu dữ liệu đối tượng `CategoryDto`:**
    * `id` (`Guid`): ID danh mục.
    * `parentId` (`Guid?`): ID danh mục cha.
    * `name` (`String`): Tên danh mục.
    * `description` (`String?`): Mô tả danh mục.
    * `level` (`Short`): Cấp độ (1, 2, 3).
    * `sortOrder` (`Integer`): Thứ tự sắp xếp.
    * `isDisabled` (`Boolean`): Cờ đánh dấu danh mục bị vô hiệu hóa hay chưa.
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
  * Yêu cầu Header: `x-user-id` (`Guid`), `x-user-roles` (`String`: `admin` hoặc `staff`).
* **Quy tắc Validate (Validation Rules):**
  * `name`: Bắt buộc, không để trống, $\le 255$ ký tự (`VAL_INVALID_INPUT`).
  * `description`: Tùy chọn, tối đa 1000 ký tự.
  * `level`: Bắt buộc, giá trị nguyên từ 1 đến 3 (`VAL_INVALID_INPUT`).
  * `parentId`: Nếu cung cấp, danh mục cha phải tồn tại trong cơ sở dữ liệu và có `Level == request.Level - 1`.
* **Chi tiết Request:**
  * **Headers:**
    * `x-user-id` (`Guid`, Bắt buộc).
    * `x-user-roles` (`String`, Bắt buộc): `admin` hoặc `staff`.
  * **Request Body (`CreateCategoryRequest`):**
    * `name` (`String`, Bắt buộc): Tên danh mục (1 - 255 ký tự).
    * `description` (`String?`, Tùy chọn): Mô tả tóm tắt.
    * `parentId` (`Guid?`, Tùy chọn): ID danh mục cha (để null nếu là danh mục gốc cấp 1).
    * `level` (`Short`, Mặc định `1`): Cấp độ (1, 2, 3).
    * `sortOrder` (`Integer`, Mặc định `0`): Thứ tự ưu tiên hiển thị.
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
  * **Thành công (201 Created):** Trả về đối tượng `CategoryDto`.
    * `id` (`Guid`): ID danh mục vừa sinh.
    * `parentId` (`Guid?`): ID danh mục cha.
    * `name` (`String`): Tên danh mục.
    * `description` (`String?`): Mô tả.
    * `level` (`Short`): Cấp độ.
    * `sortOrder` (`Integer`): Thứ tự hiển thị.
    * `isDisabled` (`Boolean`): Trạng thái vô hiệu hóa (`false`).
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
  * `isDisabled`: Kiểu Boolean.
* **Chi tiết Request:**
  * **Path Parameters:**
    * `id` (`Guid`, Bắt buộc): ID danh mục cần sửa.
  * **Request Body (`UpdateCategoryRequest`):**
    * `name` (`String`, Bắt buộc): Tên danh mục mới.
    * `description` (`String?`, Tùy chọn): Mô tả mới.
    * `level` (`Short`, Mặc định `1`): Cấp độ mới.
    * `sortOrder` (`Integer`, Mặc định `0`): Thứ tự sắp xếp.
    * `isDisabled` (`Boolean`, Mặc định `false`): Trạng thái vô hiệu hóa.
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
* **Quy tắc Validate, Request & Response:** Giống hoàn toàn API 04.
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
  * **Path Parameters:**
    * `id` (`Guid`, Bắt buộc): ID danh mục cần xóa.
* **Chi tiết Response:**
  * **Thành công:** `204 No Content` (Không có body).
  * **Lỗi (400 Bad Request):** Danh mục đang chứa con hoặc sản phẩm (`VAL_INVALID_INPUT`).
  * **Lỗi (404 Not Found):** Danh mục không tồn tại (`CATEGORY_NOT_FOUND`).
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
  * **Query Parameters:**
    * `keyword` (`String?`, Tùy chọn): Từ khóa tìm kiếm trong tên hoặc mô tả.
    * `categoryId` (`Guid?`, Tùy chọn): ID danh mục cần lọc.
    * `minPrice` (`Decimal?`, Tùy chọn): Mức giá sàn.
    * `maxPrice` (`Decimal?`, Tùy chọn): Mức giá trần.
    * `saleType` (`String?`, Tùy chọn): `Regular` hoặc `Auction`.
    * `sortBy` (`String?`, Tùy chọn): Tiêu chí sắp xếp.
    * `page` (`Integer`, Mặc định `1`): Chỉ mục trang hiện tại.
    * `pageSize` (`Integer`, Mặc định `20`): Số lượng phần tử mỗi trang.
* **Chi tiết Response (200 OK):**
  * **Cấu trúc phân trang `PagedData<ProductListItemDto>`:**
    * `items` (`Array of ProductListItemDto`):
      * `productId` (`Guid`): ID sản phẩm.
      * `name` (`String`): Tên sản phẩm.
      * `brand` (`String?`): Thương hiệu.
      * `saleType` (`String`): Loại bán (`Regular` hoặc `Auction`).
      * `status` (`String`): Trạng thái hiển thị (`Active`).
      * `minPrice` (`Decimal?`): Giá thấp nhất trong các biến thể.
      * `maxPrice` (`Decimal?`): Giá cao nhất trong các biến thể.
      * `primaryImageUrl` (`String?`): Đường dẫn URL ảnh đại diện chính.
      * `avgRating` (`Decimal?`): Điểm đánh giá trung bình (ví dụ: `4.8`).
    * `page` (`Integer`): Trang hiện tại.
    * `pageSize` (`Integer`): Số phần tử/trang.
    * `totalItems` (`Integer`): Tổng số sản phẩm thỏa mãn điều kiện.
    * `totalPages` (`Integer`): Tổng số trang.
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
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

### API 08. `GET /api/v1/products/{id}` — Xem chi tiết sản phẩm công khai [CAT-06]
* **Mục đích:** Người mua xem trang chi tiết: hiển thị thông tin sản phẩm, danh sách biến thể đang mở bán, toàn bộ ảnh sản phẩm, danh mục và điểm đánh giá sao.
* **Quyền hạn & Xác thực:** `Public`.
* **Chi tiết Request:**
  * **Path Parameters:**
    * `id` (`Guid`, Bắt buộc): ID định danh sản phẩm.
* **Chi tiết Response (200 OK):**
  * **Cấu trúc chi tiết `ProductDetailDto`:**
    * `productId` (`Guid`): ID sản phẩm.
    * `sellerId` (`Guid`): ID người bán sở hữu.
    * `name` (`String`): Tên sản phẩm.
    * `description` (`String?`): Mô tả chi tiết.
    * `brand` (`String?`): Thương hiệu.
    * `saleType` (`String`): `Regular` hoặc `Auction`.
    * `status` (`String`): `Active`.
    * `publishedAt` (`DateTimeOffset?`): Thời điểm đưa lên sàn (ISO 8601 UTC).
    * `category` (`Object`):
      * `categoryId` (`Guid`): ID danh mục.
      * `parentId` (`Guid?`): ID danh mục cha.
      * `name` (`String`): Tên danh mục.
      * `level` (`Integer`): Cấp độ.
    * `variants` (`Array of Object`):
      * `variantId` (`Guid`): ID biến thể SKU.
      * `skuCode` (`String`): Mã SKU.
      * `name` (`String`): Tên biến thể.
      * `price` (`Decimal?`): Giá bán.
      * `status` (`String`): `Active`.
      * `isActive` (`Boolean`): Đánh dấu biến thể mặc định.
      * `attributes` (`Array of Object`):
        * `attrName` (`String`): Tên thuộc tính (ví dụ: `Color`).
        * `attrValue` (`String`): Giá trị thuộc tính (ví dụ: `Đen`).
    * `images` (`Array of Object`):
      * `imageId` (`Guid`): ID ảnh.
      * `url` (`String`): Link ảnh CDN.
      * `altText` (`String?`): Mô tả ảnh.
      * `isActive` (`Boolean`): Ảnh đại diện chính hay không.
      * `sortOrder` (`Integer`): Thứ tự hiển thị.
      * `variantId` (`Guid?`): ID biến thể liên kết riêng (nếu có).
    * `rating` (`Object`):
      * `avgRating` (`Decimal?`): Điểm trung bình.
      * `reviewCount` (`Integer`): Tổng số đánh giá.
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
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

### API 09. `GET /api/v1/seller/products` — Lấy danh sách sản phẩm của Seller [CAT-44]
* **Mục đích:** Kênh quản lý của Người bán (Seller Dashboard) quản lý tất cả sản phẩm của chính mình (bao gồm cả DRAFT, ACTIVE, INACTIVE).
* **Quyền hạn & Xác thực:** `seller`, `admin`.
* **Chi tiết Request:**
  * **Headers:**
    * `x-user-id` (`Guid`, Bắt buộc): ID người bán.
  * **Query Parameters:**
    * `keyword` (`String?`, Tùy chọn).
    * `status` (`String?`, Tùy chọn): `Draft`, `Active`, `Inactive`.
    * `page` (`Integer`, Mặc định `1`).
    * `pageSize` (`Integer`, Mặc định `20`).
* **Chi tiết Response (200 OK):**
  * **Cấu trúc `PagedData<SellerProductListItemDto>`:**
    * `items` (`Array of Object`):
      * `productId` (`Guid`): ID sản phẩm.
      * `name` (`String`): Tên sản phẩm.
      * `brand` (`String?`): Thương hiệu.
      * `categoryId` (`Guid`): ID danh mục.
      * `categoryName` (`String?`): Tên danh mục.
      * `saleType` (`String`): `Regular` hoặc `Auction`.
      * `status` (`String`): Trạng thái.
      * `statusReason` (`String?`): Lý do trạng thái (nếu bị ẩn).
      * `minPrice` (`Decimal?`): Giá nhỏ nhất.
      * `maxPrice` (`Decimal?`): Giá lớn nhất.
      * `primaryImageUrl` (`String?`): Ảnh đại diện chính.
      * `variantCount` (`Integer`): Tổng số SKU đang có của sản phẩm.
      * `hasOrder` (`Boolean`): Cờ đánh dấu sản phẩm đã từng phát sinh đơn hàng hay chưa.
      * `createdAt` (`DateTimeOffset`): Thời điểm tạo.
      * `publishedAt` (`DateTimeOffset?`): Thời điểm kích hoạt lên sàn.
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

### API 10. `GET /api/v1/seller/products/{id}` — Lấy chi tiết sản phẩm dành cho Seller [CAT-45]
* **Mục đích:** Phục vụ màn hình chỉnh sửa chi tiết sản phẩm của Seller (xem toàn bộ SKU kể cả SKU đã ẩn, lịch sử `hasOrder`, và toàn bộ ảnh).
* **Quyền hạn & Xác thực:** `seller`, `admin`.
* **Chi tiết Request:**
  * **Headers:**
    * `x-user-id` (`Guid`, Bắt buộc).
  * **Path Parameters:**
    * `id` (`Guid`, Bắt buộc): ID sản phẩm.
* **Chi tiết Response (200 OK):**
  * **Đối tượng `SellerProductDetailDto`:**
    * `productId` (`Guid`): ID sản phẩm.
    * `sellerId` (`Guid`): ID người bán.
    * `categoryId` (`Guid`): ID danh mục.
    * `categoryName` (`String?`): Tên danh mục.
    * `name` (`String`): Tên sản phẩm.
    * `description` (`String?`): Mô tả.
    * `brand` (`String?`): Thương hiệu.
    * `specifications` (`String?`): Thông số kỹ thuật.
    * `saleType` (`String`): `Regular` hoặc `Auction`.
    * `status` (`String`): `Draft`, `Active`, `Inactive`.
    * `statusReason` (`String?`): Lý do (nếu có).
    * `hasOrder` (`Boolean`): Đã có đơn hàng hay chưa.
    * `createdAt` (`DateTimeOffset`): Ngày tạo.
    * `updatedAt` (`DateTimeOffset?`): Ngày cập nhật cuối.
    * `publishedAt` (`DateTimeOffset?`): Ngày xuất bản.
    * `images` (`Array of ProductImageDto`): Danh sách toàn bộ ảnh.
    * `variants` (`Array of SellerProductVariantDto`): Toàn bộ SKU của sản phẩm (bao gồm cả trạng thái `hasOrder` và ảnh riêng).
  * **Lỗi (403 Forbidden):** `PERMISSION_DENIED` (Người dùng không phải chủ sở hữu).
  * **Lỗi (404 Not Found):** `PRODUCT_NOT_FOUND`.
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

### API 11. `POST /api/v1/products` — Tạo mới sản phẩm thường (REGULAR) [CAT-01]
* **Mục đích:** Tạo mới sản phẩm bán giá cố định. Hỗ trợ **cả 2 luồng:** Luồng 1 bước tạo gộp (Single-step atomic kèm `variants`) hoặc Luồng nhiều bước (Multi-step tạo nháp trước).
* **Quyền hạn & Xác thực:** `seller`, `admin`.
* **Chi tiết Request:**
  * **Headers:**
    * `x-user-id` (`Guid`, Bắt buộc): ID người bán.
  * **Request Body (`CreateProductRequest`):**
    * `name` (`String`, Bắt buộc): Tên sản phẩm (1 - 255 ký tự).
    * `categoryId` (`Guid`, Bắt buộc): ID danh mục lá.
    * `brand` (`String?`, Tùy chọn): Thương hiệu (tối đa 150 ký tự).
    * `description` (`String?`, Tùy chọn): Mô tả (tối đa 4000 ký tự).
    * `saleType` (`String`, Mặc định `Regular`): Loại bán.
    * `specifications` (`String?`, Tùy chọn): Thông số kỹ thuật dạng chuỗi JSON hoặc text.
    * `variants` (`Array of CreateVariantRequest?`, Tùy chọn, tối đa 10 SKUs):
      * `skuCode` (`String`, Bắt buộc): Mã SKU không trùng lặp trong shop.
      * `name` (`String`, Bắt buộc): Tên biến thể.
      * `price` (`Decimal`, Bắt buộc): Giá bán $> 0$.
      * `currency` (`String?`, Mặc định `VND`): Đơn vị tiền tệ.
      * `attributes` (`Array of Object?`, Tùy chọn):
        * `attrName` (`String`, Bắt buộc): Tên thuộc tính.
        * `attrValue` (`String`, Bắt buộc): Giá trị thuộc tính.
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
  * **Thành công (201 Created):** Trả về `ProductDto` trạng thái `Draft`.
    * `id` (`Guid`): ID sản phẩm vừa tạo.
    * `sellerId` (`Guid`): ID người bán.
    * `categoryId` (`Guid`): ID danh mục lá.
    * `name` (`String`): Tên sản phẩm.
    * `description` (`String?`): Mô tả.
    * `brand` (`String?`): Thương hiệu.
    * `status` (`String`): `Draft`.
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
  * **Lỗi (400 Bad Request):** Danh mục cha hoặc giá $\le 0$ (`VAL_INVALID_INPUT`).
  * **Lỗi (409 Conflict):** Trùng mã SKU trong shop (`DUPLICATE_SKU`).
* **Tích hợp liên Service (External Calls):**
  * **Có gọi service khác:**
    * **Service đích:** `FulfillmentService` (`http://localhost:8006`).
    * **Endpoint gọi:** `POST /api/v1/inventories/initialize`.
    * **Payload gửi:**
      * `productId` (`Guid`): ID sản phẩm.
      * `skuId` (`Guid?`): `null`.
      * `initialQuantity` (`Integer`): `0`.
    * **Cơ chế xử lý khi lỗi:** Bọc trong khối `try-catch` (Non-fatal). Nếu `FulfillmentService` mất kết nối hoặc trả về lỗi, hệ thống chỉ ghi nhận log cảnh báo và **vẫn hoàn thành việc tạo Product**.

---

### API 12. `POST /api/v1/products/auction` — Tạo mới sản phẩm Đấu giá [CAT-43]
* **Mục đích:** Tạo sản phẩm tham gia phiên đấu giá trực tuyến (`SaleType = Auction`).
* **Quyền hạn & Xác thực:** `seller`, `admin`.
* **Chi tiết Request:**
  * **Headers:** `x-user-id` (`Guid`, Bắt buộc).
  * **Request Body (`CreateAuctionProductRequest`):**
    * `name` (`String`, Bắt buộc): Tên sản phẩm (1 - 255 ký tự).
    * `categoryId` (`Guid`, Bắt buộc): ID danh mục lá.
    * `brand` (`String?`, Tùy chọn): Thương hiệu.
    * `description` (`String?`, Tùy chọn): Mô tả.
    * `specifications` (`String?`, Tùy chọn): Thông số kỹ thuật.
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
* **Tích hợp liên Service (External Calls):**
  * **Có gọi service khác:**
    * **Service đích:** `FulfillmentService` (`http://localhost:8006`).
    * **Endpoint gọi:** `POST /api/v1/inventories/initialize`.
    * **Payload:** `{ "productId": "...", "skuId": "variantId", "initialQuantity": 0 }`.
    * **Xử lý khi lỗi:** Non-fatal try-catch.

---

### API 13. `PUT /api/v1/products/{id}` — Cập nhật thông tin cơ bản sản phẩm [CAT-02]
* **Mục đích:** Sửa đổi tên, thương hiệu, mô tả, thông số hoặc danh mục của sản phẩm.
* **Quyền hạn & Xác thực:** `seller`, `admin` (Chủ sở hữu).
* **Chi tiết Request:**
  * **Path Parameters:**
    * `id` (`Guid`, Bắt buộc): ID sản phẩm cần cập nhật.
  * **Request Body (`UpdateProductRequest`):**
    * `name` (`String`, Bắt buộc): Tên mới.
    * `categoryId` (`Guid`, Bắt buộc): Danh mục lá mới.
    * `brand` (`String?`, Tùy chọn): Thương hiệu mới.
    * `description` (`String?`, Tùy chọn): Mô tả mới.
    * `specifications` (`String?`, Tùy chọn): Thông số mới.
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
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

### API 14. `PATCH /api/v1/products/{id}` — Cập nhật thông tin sản phẩm (Bí danh PATCH) [CAT-02]
* **Mục đích:** Hỗ trợ gọi cập nhật sản phẩm bằng phương thức `PATCH` chuẩn REST.
* **Quy tắc & Luồng xử lý:** Giống hoàn toàn API 13.
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

### API 15. `PATCH /api/v1/products/{id}/status` — Đổi trạng thái sản phẩm [CAT-03]
* **Mục đích:** Xuất bản sản phẩm lên sàn (`ACTIVE`), tạm dừng bán (`INACTIVE`), hoặc chuyển về bản nháp (`DRAFT`).
* **Quyền hạn & Xác thực:** `seller`, `admin` (Chủ sở hữu).
* **Chi tiết Request:**
  * **Path Parameters:**
    * `id` (`Guid`, Bắt buộc): ID sản phẩm.
  * **Request Body (`ChangeProductStatusRequest`):**
    * `status` (`String`, Bắt buộc): Giá trị: `DRAFT`, `ACTIVE`, hoặc `INACTIVE`.
    * `statusReason` (`String?`, Tùy chọn): Lý do (ví dụ: `SellerPaused`, `Violation`).
  ```json
  {
    "status": "ACTIVE"
  }
  ```
* **Chi tiết Response:**
  * **Thành công (200 OK):** Trả về `ProductDto` kèm `publishedAt = UtcNow`.
  * **Lỗi (400 Bad Request):** Thiếu SKU hợp lệ hoặc thiếu hình ảnh (`VAL_INVALID_INPUT`).
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

### API 16. `DELETE /api/v1/products/{id}` — Xóa mềm sản phẩm [CAT-04]
* **Mục đích:** Đánh dấu xóa mềm sản phẩm và xóa mềm toàn bộ các biến thể thuộc sản phẩm đó.
* **Quyền hạn & Xác thực:** `seller`, `admin` (Chủ sở hữu).
* **Chi tiết Request:**
  * **Path Parameters:**
    * `id` (`Guid`, Bắt buộc): ID sản phẩm cần xóa.
* **Chi tiết Response:**
  * **Thành công:** `204 No Content`.
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

## 🏷️ PHẦN 4: PHÂN HỆ BIẾN THỂ / SKU (VARIANT MANAGEMENT)

---

### API 17. `GET /api/v1/variants/{id}` — Xem chi tiết một biến thể SKU [CAT-42]
* **Mục đích:** Tra cứu cấu hình giá, mã SKU, bộ thuộc tính dynamic (`Color`, `Size`,...) và trạng thái của 1 biến thể cụ thể.
* **Quyền hạn & Xác thực:** `Public` / `Seller`.
* **Chi tiết Request:**
  * **Path Parameters:**
    * `id` (`Guid`, Bắt buộc): ID của biến thể.
* **Chi tiết Response (200 OK):**
  * **Cấu trúc `VariantDetailDto`:**
    * `id` (`Guid`): ID biến thể.
    * `productId` (`Guid`): ID sản phẩm cha.
    * `skuCode` (`String`): Mã SKU.
    * `name` (`String`): Tên biến thể.
    * `price` (`Decimal?`): Giá bán.
    * `currency` (`String`): Loại tiền tệ (`VND`).
    * `isActive` (`Boolean`): Biến thể mặc định hay không.
    * `hasOrder` (`Boolean`): Cờ đánh dấu đã có đơn hàng chưa.
    * `status` (`String`): `Active` hoặc `Inactive`.
    * `statusReason` (`String?`): Lý do.
    * `attributes` (`Array of ProductVariantAttributeDto`):
      * `attrName` (`String`): Tên thuộc tính.
      * `attrValue` (`String`): Giá trị thuộc tính.
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
* **Chi tiết Request:**
  * **Path Parameters:**
    * `id` (`Guid`, Bắt buộc): ID sản phẩm cha.
  * **Request Body (`CreateVariantRequest`):**
    * `skuCode` (`String`, Bắt buộc): Mã SKU không trùng lặp trong shop.
    * `name` (`String`, Bắt buộc): Tên biến thể.
    * `price` (`Decimal`, Bắt buộc): Giá bán $> 0$.
    * `currency` (`String?`, Mặc định `VND`).
    * `attributes` (`Array of CreateVariantAttributeRequest?`, Tùy chọn):
      * `attrName` (`String`, Bắt buộc).
      * `attrValue` (`String`, Bắt buộc).
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
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

### API 19. `PUT /api/v1/variants/{id}` — Cập nhật thông tin biến thể [CAT-08]
* **Mục đích:** Sửa tên biến thể, điều chỉnh giá bán và cập nhật bộ thuộc tính.
* **Quyền hạn & Xác thực:** `seller`, `admin` (Chủ sở hữu).
* **Chi tiết Request:**
  * **Path Parameters:**
    * `id` (`Guid`, Bắt buộc): ID biến thể cần sửa.
  * **Request Body (`UpdateVariantRequest`):**
    * `name` (`String`, Bắt buộc): Tên mới.
    * `price` (`Decimal`, Bắt buộc): Giá bán mới $> 0$.
    * `currency` (`String?`, Mặc định `VND`).
    * `attributes` (`Array of CreateVariantAttributeRequest?`, Tùy chọn).
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
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

### API 20. `PATCH /api/v1/variants/{id}` — Cập nhật biến thể (Bí danh PATCH) [CAT-08]
* **Mục đích:** Hỗ trợ gọi cập nhật biến thể bằng phương thức `PATCH` chuẩn REST.
* **Quy tắc & Luồng xử lý:** Giống hoàn toàn API 19.
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

### API 21. `PUT /api/v1/products/{id}/default-variant` — Đặt biến thể mặc định [CAT-10]
* **Mục đích:** Chỉ định SKU nào sẽ làm SKU đại diện hiển thị giá chính khi khách bấm vào sản phẩm.
* **Quyền hạn & Xác thực:** `seller`, `admin`.
* **Chi tiết Request:**
  * **Path Parameters:**
    * `id` (`Guid`, Bắt buộc): ID sản phẩm.
  * **Request Body (`SetDefaultVariantRequest`):**
    * `variantId` (`Guid`, Bắt buộc): ID của biến thể được chọn làm mặc định.
  ```json
  {
    "variantId": "77777777-7777-7777-7777-777777777777"
  }
  ```
* **Chi tiết Response:**
  * **Thành công (200 OK):** Trả về `VariantDetailDto` với `isActive = true`.
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

### API 22. `PATCH /api/v1/variants/{id}/status` — Đổi trạng thái biến thể [CAT-09]
* **Mục đích:** Ẩn hoặc mở bán một SKU cụ thể (`ACTIVE` / `INACTIVE`).
* **Quyền hạn & Xác thực:** `seller`, `admin`.
* **Chi tiết Request:**
  * **Path Parameters:**
    * `id` (`Guid`, Bắt buộc): ID biến thể.
  * **Request Body (`ChangeVariantStatusRequest`):**
    * `status` (`String`, Bắt buộc): `ACTIVE` hoặc `INACTIVE`.
    * `statusReason` (`String?`, Tùy chọn): Lý do (ví dụ: `SellerPaused`, `OutOfStock`).
  ```json
  {
    "status": "INACTIVE",
    "statusReason": "SellerPaused"
  }
  ```
* **Chi tiết Response:**
  * **Thành công (200 OK):** Trả về `VariantDetailDto` đã đổi trạng thái.
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

### API 23. `DELETE /api/v1/variants/{id}` — Xóa mềm biến thể [CAT-11]
* **Mục đích:** Xóa biến thể khỏi sản phẩm.
* **Quyền hạn & Xác thực:** `seller`, `admin`.
* **Chi tiết Request:**
  * **Path Parameters:**
    * `id` (`Guid`, Bắt buộc): ID biến thể cần xóa.
* **Chi tiết Response:**
  * **Thành công:** `204 No Content`.
  * **Lỗi (400 Bad Request):** Chặn xóa nếu biến thể đã từng phát sinh đơn hàng (`hasOrder == true`) hoặc là biến thể cuối cùng (`VAL_INVALID_INPUT`).
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

## 🖼️ PHẦN 5: PHÂN HỆ HÌNH ẢNH SẢN PHẨM (PRODUCT IMAGES)

---

### API 24. `GET /api/v1/products/{id}/images` — Lấy danh sách ảnh sản phẩm [CAT-40]
* **Mục đích:** Lấy toàn bộ danh sách ảnh của sản phẩm theo thứ tự hiển thị `sortOrder`.
* **Quyền hạn & Xác thực:** `Public` / `Seller`.
* **Chi tiết Request:**
  * **Path Parameters:**
    * `id` (`Guid`, Bắt buộc): ID sản phẩm.
* **Chi tiết Response (200 OK):**
  * **Mảng đối tượng `ProductImageDto`:**
    * `id` (`Guid`): ID ảnh.
    * `productId` (`Guid`): ID sản phẩm.
    * `variantId` (`Guid?`): ID biến thể gắn riêng (null nếu là ảnh chung).
    * `url` (`String`): Đường dẫn ảnh CDN.
    * `altText` (`String?`): Chú thích hình ảnh.
    * `isActive` (`Boolean`): Cờ ảnh đại diện chính.
    * `sortOrder` (`Short`): Thứ tự hiển thị.
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
* **Chi tiết Request (Form Data):**
  * `file` (`IFormFile` / `Binary File`, Bắt buộc): File nhị phân ảnh (jpg, png, webp).
  * `variantId` (`Guid?`, Tùy chọn): ID biến thể liên kết riêng.
  * `altText` (`String?`, Tùy chọn): Mô tả ảnh.
  * `sortOrder` (`Short`, Mặc định `0`): Thứ tự sắp xếp.
* **Chi tiết Response:**
  * **Thành công (201 Created):** Trả về `ProductImageDto`.
    * `id` (`Guid`): ID ảnh vừa lưu.
    * `productId` (`Guid`): ID sản phẩm.
    * `variantId` (`Guid?`): ID biến thể.
    * `url` (`String`): Link ảnh CDN công khai do Cloudinary cấp.
    * `altText` (`String?`): Mô tả.
    * `isActive` (`Boolean`): `true` nếu là ảnh đầu tiên.
    * `sortOrder` (`Short`): Thứ tự.
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
  * **Lỗi (400 Bad Request):** Quá 5 ảnh (`VAL_LIMIT_EXCEEDED`).
* **Tích hợp liên Service (External Calls):**
  * **Có gọi service bên thứ 3:**
    * **Dịch vụ:** **Cloudinary Media API** (qua CloudinaryDotNet SDK).
    * **Mục đích:** Lưu trữ file ảnh vật lý trên mạng phân phối nội dung (CDN).
    * **Xử lý khi lỗi:** Nếu upload thất bại, ném ngoại lệ dừng lại ngay lập tức, không ghi dữ liệu rác vào database.

---

### API 26. `PATCH /api/v1/images/{id}` — Cập nhật ảnh đại diện chính & Thứ tự [CAT-14]
* **Mục đích:** Đổi ảnh đại diện chính (`isActive`), số thứ tự hiển thị (`sortOrder`) hoặc mô tả `altText`.
* **Quyền hạn & Xác thực:** `seller`, `admin`.
* **Chi tiết Request:**
  * **Path Parameters:**
    * `id` (`Guid`, Bắt buộc): ID ảnh.
  * **Request Body (`UpdateProductImageRequest`):**
    * `isActive` (`Boolean?`, Tùy chọn): Đặt làm ảnh đại diện chính.
    * `sortOrder` (`Short?`, Tùy chọn): Số thứ tự ưu tiên.
    * `altText` (`String?`, Tùy chọn): Chú thích mới.
  ```json
  {
    "isActive": true,
    "sortOrder": 1,
    "altText": "Ảnh góc nghiêng cập nhật"
  }
  ```
* **Chi tiết Response:**
  * **Thành công (200 OK):** Trả về `ProductImageDto` đã cập nhật.
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

### API 27. `DELETE /api/v1/images/{id}` — Xóa ảnh sản phẩm theo ID ảnh [CAT-13]
* **Mục đích:** Xóa mềm một ảnh theo ID ảnh (Endpoint độc lập tại Images Controller).
* **Quyền hạn & Xác thực:** `seller`, `admin` (Chủ sở hữu).
* **Chi tiết Request:**
  * **Path Parameters:**
    * `id` (`Guid`, Bắt buộc): ID ảnh cần xóa.
* **Chi tiết Response:**
  * **Thành công:** `204 No Content`.
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

### API 28. `DELETE /api/v1/products/{id}/images/{imageId}` — Xóa ảnh theo route lồng
* **Mục đích:** Cung cấp thêm route xóa ảnh có kèm `productId` trong URL (Bổ trợ tương thích).
* **Quy tắc & Luồng xử lý:** Giống hoàn toàn API 27.
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.

---

## 🔗 PHẦN 6: PHÂN HỆ TÍCH HỢP LIÊN SERVICE (INTERNAL APIS)

---

### API 29. `POST /api/v1/internal/variants` — Lấy danh sách biến thể hàng loạt
* **Mục đích:** Cung cấp cho `OrderService`, `CartService`, `AuctionService` truy vấn nhanh thông tin chi tiết (tên sản phẩm, giá bán, ảnh chính, thuộc tính) của nhiều SKU cùng lúc.
* **Quyền hạn & Xác thực:** `Internal` (Gọi giữa các microservices).
* **Chi tiết Request:**
  * **Request Body (`BatchGetVariantsRequest`):**
    * `variantIds` (`Array of Guid`, Bắt buộc): Danh sách ID biến thể cần truy vấn.
  ```json
  {
    "variantIds": [
      "77777777-7777-7777-7777-777777777777",
      "77777777-7777-7777-7777-777777777778"
    ]
  }
  ```
* **Chi tiết Response (200 OK):**
  * **Cấu trúc `BatchVariantResponse`:**
    * `items` (`Array of BatchVariantItemDto`):
      * `variantId` (`Guid`): ID biến thể.
      * `productId` (`Guid`): ID sản phẩm.
      * `skuCode` (`String`): Mã SKU.
      * `variantName` (`String`): Tên biến thể.
      * `productName` (`String`): Tên sản phẩm.
      * `price` (`Decimal?`): Giá bán.
      * `currency` (`String`): Loại tiền tệ.
      * `status` (`String`): Trạng thái SKU.
      * `productStatus` (`String`): Trạng thái sản phẩm.
      * `sellerId` (`Guid`): ID người bán.
      * `saleType` (`String`): `Regular` hoặc `Auction`.
      * `primaryImageUrl` (`String?`): Link ảnh đại diện chính.
      * `attributes` (`Array of ProductVariantAttributeDto`):
        * `attrName` (`String`): Tên thuộc tính.
        * `attrValue` (`String`): Giá trị thuộc tính.
    * `notFoundIds` (`Array of Guid`): Danh sách ID không tìm thấy trong hệ thống.
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
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác** (Đóng vai trò Server phục vụ cho các service khác).

---

### API 30. `POST /api/v1/internal/variants/check-status` — Kiểm tra tính hợp lệ của biến thể
* **Mục đích:** `OrderService` gọi trước khi tạo đơn hàng để kiểm tra xem sản phẩm và biến thể có thỏa mãn điều kiện mua sắm hay không (`isPurchasable`).
* **Quyền hạn & Xác thực:** `Internal`.
* **Chi tiết Request:**
  * **Request Body (`CheckVariantStatusRequest`):**
    * `variantIds` (`Array of Guid`, Bắt buộc): Danh sách biến thể cần kiểm tra.
    * `purpose` (`String`, Bắt buộc): Mục đích kiểm tra (ví dụ: `ORDER`, `AUCTION`).
  ```json
  {
    "variantIds": [
      "77777777-7777-7777-7777-777777777777"
    ],
    "purpose": "ORDER"
  }
  ```
* **Chi tiết Response (200 OK):**
  * **Cấu trúc `CheckVariantStatusResponse`:**
    * `items` (`Array of VariantStatusItemDto`):
      * `variantId` (`Guid`): ID biến thể.
      * `productId` (`Guid`): ID sản phẩm.
      * `productStatus` (`String`): Trạng thái sản phẩm (`Active`).
      * `variantStatus` (`String`): Trạng thái biến thể (`Active`).
      * `saleType` (`String`): `Regular` hoặc `Auction`.
      * `isPurchasable` (`Boolean`): Đủ điều kiện mua sắm hay không (`true`/`false`).
      * `reason` (`String`): Lý do (`OK` nếu hợp lệ, hoặc mô tả lỗi nếu `isPurchasable == false`).
      * `price` (`Decimal?`): Giá bán.
      * `currency` (`String`): Đơn vị tiền tệ.
    * `notFoundIds` (`Array of Guid`): Các ID biến thể không tồn tại.
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
* **Tích hợp liên Service (External Calls):**
  * **Không gọi service khác**.
