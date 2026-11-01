# TÀI LIỆU CHI TIẾT TỪNG API - CATALOG SERVICE

> **Dịch vụ:** Nexus Catalog Service (Quản lý Danh mục, Sản phẩm, Biến thể SKU, Hình ảnh)  
> **Cổng mặc định:** `8002` (Docker / Local)  
> **Phiên bản API:** `v1`  
> **Định dạng dữ liệu:** `application/json` (Upload ảnh: `multipart/form-data`)  
> **Cơ sở dữ liệu:** PostgreSQL Neon Cloud (`catalog`)

---

## 📌 PHẦN 1: QUY CHUẨN KỸ THUẬT CHUNG

### 1. Headers xác thực & Bối cảnh người dùng
Mọi API yêu cầu phân quyền (`Seller`, `Admin`, `Staff`) nhận thông tin định danh được chuyển tiếp từ API Gateway thông qua các HTTP Headers:
* `Authorization`: `Bearer <jwt-token>` (Token đăng nhập của người dùng).
* `x-user-id`: GUID định danh duy nhất của người dùng (Ví dụ: `e3b0c442-98fc-1c14-9af7-4c2c4d2e931b`).
* `x-user-roles`: Quyền của người dùng, phân tách bằng dấu phẩy (Ví dụ: `seller`, `admin`, `buyer`).

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
* `PRODUCT_NOT_FOUND`: Sản phẩm không tồn tại (HTTP 404).
* `VARIANT_NOT_FOUND`: Biến thể SKU không tồn tại (HTTP 404).
* `IMAGE_NOT_FOUND`: Hình ảnh không tồn tại (HTTP 404).
* `DUPLICATE_SKU`: Mã SKU bị trùng lặp trong nội bộ gian hàng của Seller (HTTP 409).
* `MAX_VARIANTS_REACHED`: Vượt quá số lượng tối đa 10 biến thể cho 1 sản phẩm (HTTP 400).
* `VAL_LIMIT_EXCEEDED`: Vượt quá số lượng tối đa 5 hình ảnh cho 1 sản phẩm (HTTP 400).

---

## 📁 PHẦN 2: PHÂN HỆ DANH MỤC (CATEGORY MANAGEMENT)

### 1. `GET /api/v1/categories` — Lấy danh sách danh mục
* **Mục đích:** Khách hàng hoặc người bán tra cứu danh sách danh mục hàng hóa trên sàn.
* **Quyền hạn:** `Public` (Không yêu cầu đăng nhập).
* **Query Parameters:**
  * `parentId` (Guid, tùy chọn): Lọc các danh mục con trực tiếp thuộc về danh mục cha này.
  * `tree` (Boolean, mặc định `false`): Nếu `true`, trả về cấu trúc cây phân cấp lồng nhau (Parent $\to$ Children). Nếu `false`, trả về danh sách phẳng.
  * `keyword` (String, tùy chọn): Tìm kiếm danh mục theo tên.
* **Phản hồi thành công (200 OK):**
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

---

### 2. `GET /api/v1/categories/{id}` — Xem chi tiết một danh mục
* **Mục đích:** Lấy thông tin chi tiết một danh mục cụ thể theo ID.
* **Quyền hạn:** `Public`.
* **Path Parameters:** `id` (Guid, Bắt buộc).
* **Phản hồi thành công (200 OK):**
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
* **Lỗi (404 Not Found):** `CATEGORY_NOT_FOUND` nếu không tìm thấy ID.

---

### 3. `POST /api/v1/categories` [CAT-15] — Tạo danh mục mới
* **Mục đích:** Quản trị viên/Nhân viên tạo cây danh mục tối đa 3 cấp.
* **Quyền hạn:** `admin`, `staff`.
* **Request Body:**
```json
{
  "name": "Áo Thun Nam",
  "description": "Các loại áo phông, áo thun nam",
  "parentId": "10000000-0000-0000-0000-000000000002",
  "level": 3,
  "sortOrder": 1
}
```
* **Ràng buộc:**
  * `name`: Bắt buộc, tối đa 255 ký tự.
  * `level`: Từ 1 đến 3.
  * Nếu có `parentId`, danh mục cha phải tồn tại.
* **Phản hồi thành công (201 Created):** Trả về thông tin danh mục vừa tạo.

---

### 4. `PATCH /api/v1/categories/{id}` hoặc `PUT` [CAT-16] — Cập nhật danh mục
* **Mục đích:** Sửa đổi tên, mô tả, thứ tự sắp xếp hoặc ẩn danh mục.
* **Quyền hạn:** `admin`, `staff`.
* **Path Parameters:** `id` (Guid, Bắt buộc).
* **Request Body:**
```json
{
  "name": "Áo Thun Cổ Tròn",
  "description": "Mô tả cập nhật",
  "level": 3,
  "sortOrder": 2,
  "isDisabled": false
}
```
* **Phản hồi thành công (200 OK):** Trả về CategoryDto đã cập nhật.

---

### 5. `DELETE /api/v1/categories/{id}` [CAT-17] — Xóa mềm danh mục
* **Mục đích:** Vô hiệu hóa và xóa mềm danh mục khỏi hệ thống.
* **Quyền hạn:** `admin`, `staff`.
* **Path Parameters:** `id` (Guid, Bắt buộc).
* **Ràng buộc:** Không cho phép xóa danh mục nếu đang có danh mục con bên dưới.
* **Phản hồi thành công:** `204 No Content`.

---

## 📦 PHẦN 3: PHÂN HỆ SẢN PHẨM (PRODUCT MANAGEMENT)

### 6. `GET /api/v1/products` [CAT-05] — Tìm kiếm, lọc sản phẩm công khai
* **Mục đích:** Trang chủ hoặc trang tìm kiếm của người mua hiển thị sản phẩm đang `ACTIVE`.
* **Quyền hạn:** `Public`.
* **Query Parameters:**
  * `keyword` (String): Tìm theo tên sản phẩm, nhãn hiệu hoặc mô tả.
  * `categoryId` (Guid): Lọc theo danh mục.
  * `minPrice` / `maxPrice` (Decimal): Lọc khoảng giá.
  * `saleType` (String): `Regular` hoặc `Auction`.
  * `sortBy` (String): `price_asc`, `price_desc`, `rating_desc`, `newest`.
  * `page` (Int, mặc định `1`), `pageSize` (Int, mặc định `20`).
* **Phản hồi thành công (200 OK):**
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

---

### 7. `GET /api/v1/products/{id}` [CAT-06] — Chi tiết sản phẩm công khai
* **Mục đích:** Người mua bấm vào xem trang chi tiết sản phẩm: bao gồm toàn bộ biến thể đang active, ảnh, danh mục và đánh giá sao.
* **Quyền hạn:** `Public`.
* **Path Parameters:** `id` (Guid, Bắt buộc).
* **Phản hồi thành công (200 OK):**
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
      "sortOrder": 0
    }
  ],
  "rating": {
    "avgRating": 4.8,
    "reviewCount": 12
  }
}
```

---

### 8. `GET /api/v1/seller/products` [CAT-44] — Danh sách sản phẩm của Seller
* **Mục đích:** Kênh quản lý của Người bán (Seller Dashboard) quản lý tất cả sản phẩm của mình (DRAFT, ACTIVE, INACTIVE).
* **Quyền hạn:** `seller`, `admin`.
* **Headers:** `x-user-id` (Guid).
* **Query Parameters:** `keyword`, `status` (DRAFT, ACTIVE, INACTIVE), `page`, `pageSize`.
* **Phản hồi thành công (200 OK):** Trả về danh sách `SellerProductListItemDto` có thêm trường `variantCount`, `hasOrder`, `createdAt`.

---

### 9. `GET /api/v1/seller/products/{id}` [CAT-45] — Chi tiết sản phẩm dành cho Seller
* **Mục đích:** Phục vụ màn hình chỉnh sửa sản phẩm của Seller (xem cả các SKU bị ẩn, lịch sử đơn hàng `hasOrder`, chi tiết từng ảnh).
* **Quyền hạn:** `seller`, `admin`.
* **Path Parameters:** `id` (Guid, Bắt buộc).
* **Ràng buộc:** Chỉ Seller chủ sở hữu mới xem được (chống xem trộm dữ liệu shop khác - HTTP 403 `PERMISSION_DENIED`).

---

### 10. `POST /api/v1/products` [CAT-01] — Tạo sản phẩm thường (REGULAR)
* **Mục đích:** Tạo mới sản phẩm bán giá cố định. Hỗ trợ **cả 2 luồng:** tạo sản phẩm nháp (Multi-step) hoặc tạo kèm luôn danh sách biến thể (Single-step).
* **Quyền hạn:** `seller`, `admin`.
* **Headers:** `x-user-id` (Guid).
* **Request Body (Ví dụ Single-step kèm biến thể):**
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
* **Ràng buộc nghiệp vụ:**
  * `categoryId`: Bắt buộc phải là **danh mục lá (Leaf Category)**.
  * Tên $\le 255$ ký tự, Thương hiệu $\le 150$ ký tự, Mô tả $\le 4000$ ký tự.
  * Tối đa 10 biến thể. Mã SKU không được trùng lặp trong nội bộ gian hàng.
  * Giá biến thể phải $> 0$.
* **Phản hồi thành công (201 Created):**
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

---

### 11. `POST /api/v1/products/auction` [CAT-43] — Tạo sản phẩm Đấu giá
* **Mục đích:** Tạo sản phẩm phục vụ phiên bán đấu giá (`SaleType = Auction`).
* **Quyền hạn:** `seller`, `admin`.
* **Request Body:**
```json
{
  "name": "Tranh Sơn Dầu Nghệ Thuật Cổ",
  "categoryId": "10000000-0000-0000-0000-000000000009",
  "brand": "Nexus Antique",
  "description": "Tác phẩm độc bản",
  "specifications": "Kích thước 80x120cm"
}
```
* **Đặc thù nghiệp vụ:** Hệ thống tự động tạo sẵn đúng **1 biến thể duy nhất** không nhận giá (`price = null`), giao toàn bộ quyền điều phối giá khởi điểm và bước giá cho `AuctionService`. Không nhận mảng `variants` từ ngoài vào.
* **Phản hồi thành công (201 Created):** Trả về `ProductDto` với trạng thái `Draft`.

---

### 12. `PATCH /api/v1/products/{id}` hoặc `PUT` [CAT-02] — Sửa thông tin sản phẩm
* **Mục đích:** Cập nhật tên, danh mục, thương hiệu, mô tả hoặc thông số kỹ thuật.
* **Quyền hạn:** `seller`, `admin` (Chủ sở hữu).
* **Path Parameters:** `id` (Guid, Bắt buộc).
* **Request Body:**
```json
{
  "name": "Áo Khoác Gió Nam Cao Cấp",
  "categoryId": "10000000-0000-0000-0000-000000000003",
  "brand": "Nexus Premium",
  "description": "Mô tả mới...",
  "specifications": "Thông số..."
}
```
* **Phản hồi thành công:** `200 OK`.

---

### 13. `PATCH /api/v1/products/{id}/status` [CAT-03] — Thay đổi trạng thái sản phẩm
* **Mục đích:** Kích hoạt sản phẩm lên sàn (`ACTIVE`), hoặc tạm dừng bán (`INACTIVE`), hoặc chuyển về bản nháp (`DRAFT`).
* **Quyền hạn:** `seller`, `admin` (Chủ sở hữu).
* **Request Body (Kích hoạt):**
```json
{
  "status": "ACTIVE"
}
```
* **Request Body (Tạm dừng bán kèm lý do):**
```json
{
  "status": "INACTIVE",
  "statusReason": "SellerPaused"
}
```
* **Điều kiện bắt buộc khi chuyển sang `ACTIVE` (Guardrails):**
  1. Phải có ít nhất **1 biến thể đang `ACTIVE`** và có giá bán $> 0$ (sản phẩm thường).
  2. Phải có ít nhất **1 hình ảnh sản phẩm đã upload** và mang cờ đại diện chính `IsActive = true`.
  $\implies$ Vi phạm điều kiện: trả về `400 Bad Request` (`VAL_INVALID_INPUT`).
* **Phản hồi thành công (200 OK):** Trả về ProductDto đã đổi trạng thái.

---

### 14. `DELETE /api/v1/products/{id}` [CAT-04] — Xóa mềm sản phẩm
* **Mục đích:** Đánh dấu xóa mềm sản phẩm và toàn bộ biến thể con đi kèm.
* **Quyền hạn:** `seller`, `admin` (Chủ sở hữu).
* **Path Parameters:** `id` (Guid, Bắt buộc).
* **Phản hồi thành công:** `204 No Content`.

---

## 🏷️ PHẦN 4: PHÂN HỆ BIẾN THỂ / SKU (VARIANT MANAGEMENT)

### 15. `GET /api/v1/variants/{id}` [CAT-42] — Xem chi tiết một biến thể
* **Mục đích:** Lấy thông tin chi tiết một biến thể SKU cụ thể theo ID.
* **Quyền hạn:** `Public` / `Seller`.
* **Path Parameters:** `id` (Guid, Bắt buộc).
* **Phản hồi thành công (200 OK):**
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

---

### 16. `POST /api/v1/products/{id}/variants` [CAT-07] — Thêm biến thể cho sản phẩm
* **Mục đích:** Thêm một biến thể SKU mới vào sản phẩm đã có (Dành cho luồng Multi-step).
* **Quyền hạn:** `seller`, `admin` (Chủ sở hữu).
* **Path Parameters:** `id` (Guid của Product).
* **Request Body:**
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
* **Ràng buộc:**
  * Tối đa 10 SKUs / sản phẩm (`MAX_VARIANTS_REACHED`).
  * `skuCode` không được trùng với bất kỳ SKU nào trong shop của Seller (`409 DUPLICATE_SKU`).
  * Không được thêm biến thể cho sản phẩm Đấu giá (`AUCTION`).
* **Phản hồi thành công (201 Created):** Trả về `VariantDetailDto`.

---

### 17. `PATCH /api/v1/variants/{id}` hoặc `PUT` [CAT-08] — Cập nhật biến thể
* **Mục đích:** Sửa tên, giá bán và thuộc tính biến thể.
* **Quyền hạn:** `seller`, `admin` (Chủ sở hữu).
* **Path Parameters:** `id` (Guid của Variant).
* **Request Body:**
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
* **Phản hồi thành công:** `200 OK`.

---

### 18. `PUT /api/v1/products/{id}/default-variant` [CAT-10] — Chọn biến thể mặc định
* **Mục đích:** Chỉ định SKU nào sẽ làm SKU đại diện hiển thị giá chính khi khách bấm vào sản phẩm.
* **Quyền hạn:** `seller`, `admin`.
* **Path Parameters:** `id` (Guid của Product).
* **Request Body:**
```json
{
  "variantId": "77777777-7777-7777-7777-777777777777"
}
```
* **Xử lý:** Gán `isActive = true` cho biến thể được chọn, và chuyển tất cả các biến thể khác của sản phẩm đó về `isActive = false`.
* **Phản hồi thành công:** `200 OK`.

---

### 19. `PATCH /api/v1/variants/{id}/status` [CAT-09] — Đổi trạng thái biến thể
* **Mục đích:** Ẩn hoặc mở bán một biến thể cụ thể (ví dụ: hết màu đỏ thì ẩn SKU đỏ).
* **Quyền hạn:** `seller`, `admin`.
* **Request Body:**
```json
{
  "status": "INACTIVE",
  "statusReason": "SellerPaused"
}
```
* **Phản hồi thành công:** `200 OK`.

---

### 20. `DELETE /api/v1/variants/{id}` [CAT-11] — Xóa mềm biến thể
* **Mục đích:** Xóa biến thể khỏi sản phẩm.
* **Quyền hạn:** `seller`, `admin`.
* **Ràng buộc:** 
  * Chặn xóa nếu biến thể đã từng phát sinh đơn hàng (`hasOrder = true`), bắt buộc Seller phải chuyển sang `INACTIVE` thay vì xóa.
  * Không thể xóa nếu đây là biến thể duy nhất còn lại của sản phẩm.
* **Phản hồi thành công:** `204 No Content`.

---

## 🖼️ PHẦN 5: PHÂN HỆ HÌNH ẢNH SẢN PHẨM (PRODUCT IMAGES)

### 21. `GET /api/v1/products/{id}/images` [CAT-40] — Lấy danh sách ảnh sản phẩm
* **Mục đích:** Lấy toàn bộ danh sách ảnh của sản phẩm theo thứ tự hiển thị `sortOrder`.
* **Quyền hạn:** `Public` / `Seller`.
* **Path Parameters:** `id` (Guid của Product).
* **Phản hồi thành công (200 OK):**
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

---

### 22. `POST /api/v1/products/{id}/images` [CAT-12] — Tải lên hình ảnh sản phẩm
* **Mục đích:** Upload file hình ảnh lên Cloudinary và lưu thông tin vào sản phẩm.
* **Quyền hạn:** `seller`, `admin` (Chủ sở hữu).
* **Định dạng gửi lên:** `multipart/form-data`.
* **Path Parameters:** `id` (Guid của Product).
* **Form Data Fields:**
  * `file` (File, Bắt buộc): File ảnh (jpg, png, webp, jpeg).
  * `variantId` (Guid, tùy chọn): Gắn ảnh riêng cho biến thể SKU nào.
  * `altText` (String, tùy chọn): Mô tả hình ảnh.
  * `sortOrder` (Short, tùy chọn, mặc định `0`): Thứ tự hiển thị.
* **Ràng buộc:** Tối đa **5 ảnh** cho mỗi sản phẩm. Ảnh đầu tiên tự động thành ảnh đại diện chính (`IsActive = true`).
* **Phản hồi thành công (201 Created):** Trả về `ProductImageDto` kèm URL Cloudinary thật.

---

### 23. `PATCH /api/v1/images/{id}` [CAT-14] — Đặt ảnh đại diện chính & Thứ tự
* **Mục đích:** Chuyển đổi ảnh đại diện chính của sản phẩm, đổi thứ tự hiển thị hoặc cập nhật alt text.
* **Quyền hạn:** `seller`, `admin`.
* **Path Parameters:** `id` (Guid của Image).
* **Request Body:**
```json
{
  "isActive": true,
  "sortOrder": 1,
  "altText": "Ảnh góc nghiêng cập nhật"
}
```
* **Quy tắc:** Khi đặt một ảnh làm đại diện chính (`isActive = true`), hệ thống tự động gỡ cờ đại diện ở các ảnh còn lại của sản phẩm để bảo đảm luôn chỉ có duy nhất 1 ảnh đại diện chính.
* **Phản hồi thành công (200 OK):** Trả về `ProductImageDto`.

---

### 24. `DELETE /api/v1/images/{id}` [CAT-13] — Xóa ảnh sản phẩm (Standalone)
* **Mục đích:** Xóa mềm một ảnh theo ID của ảnh.
* **Quyền hạn:** `seller`, `admin`.
* **Path Parameters:** `id` (Guid của Image).
* **Quy tắc đặc biệt:** Nếu ảnh bị xóa đang là ảnh đại diện chính (`IsActive = true`), hệ thống tự động chuyển cờ đại diện sang ảnh kế tiếp có `sortOrder` nhỏ nhất.
* **Phản hồi thành công:** `204 No Content`.

---

### 25. `DELETE /api/v1/products/{id}/images/{imageId}` — Xóa ảnh (Nested Route)
* **Mục đích:** Cung cấp thêm route xóa ảnh có kèm cả Product ID trong đường dẫn.
* **Quyền hạn:** `seller`, `admin`.
* **Path Parameters:** `id` (Product ID), `imageId` (Image ID).
* **Phản hồi thành công:** `204 No Content`.

---

## 🔗 PHẦN 6: PHÂN HỆ TÍCH HỢP LIÊN SERVICE (INTERNAL APIS)

### 26. `POST /api/v1/internal/variants` — Lấy thông tin hàng loạt biến thể
* **Mục đích:** Được gọi bởi `OrderService`, `CartService`, `AuctionService` để lấy thông tin chi tiết (tên, giá, ảnh, thuộc tính) của nhiều SKU cùng lúc.
* **Quyền hạn:** `Internal`.
* **Request Body:**
```json
{
  "variantIds": [
    "77777777-7777-7777-7777-777777777777",
    "77777777-7777-7777-7777-777777777778"
  ]
}
```
* **Phản hồi thành công (200 OK):**
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

---

### 27. `POST /api/v1/internal/variants/check-status` — Kiểm tra tính hợp lệ của biến thể
* **Mục đích:** `OrderService` gọi trước khi tạo đơn hàng để kiểm tra xem sản phẩm và biến thể có đang mở bán hay không (`isPurchasable`).
* **Quyền hạn:** `Internal`.
* **Request Body:**
```json
{
  "variantIds": [
    "77777777-7777-7777-7777-777777777777"
  ],
  "purpose": "ORDER"
}
```
* **Phản hồi thành công (200 OK):**
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

