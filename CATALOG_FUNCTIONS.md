# TÀI LIỆU CHI TIẾT CÁC CHỨC NĂNG (CAT FUNCTIONS) - CATALOG SERVICE

> **Ngày cập nhật:** 28/10/2026  
> **Trạng thái triển khai:** ✅ Đã hoàn thiện mã nguồn và kiểm chứng build Release thành công 100%.  
> **Chuẩn kiến trúc:** RESTful Web API over HTTP, Clean Architecture, đóng gói toàn bộ phản hồi qua `IApiResponseFactory` (`ApiResponse<T>`).

---

## 📌 MỤC LỤC
1. [Quy Chuẩn Chung (Headers, Envelope, Mã Lỗi)](#quy-chuẩn-chung)
2. [Phân Hệ 1: Quản Lý Sản Phẩm Cho Seller (CAT-01, CAT-02, CAT-03, CAT-04, CAT-44, CAT-45)](#phân-hệ-1-quản-lý-sản-phẩm-cho-seller)
3. [Phân Hệ 2: Hiển Thị & Tìm Kiếm Cho Buyer (CAT-05, CAT-06, CAT-20, CAT-21)](#phân-hệ-2-hiển-thị--tìm-kiếm-cho-buyer)
4. [Phân Hệ 3: Quản Lý Biến Thể / SKU (CAT-07, CAT-08, CAT-09, CAT-10, CAT-11, CAT-42)](#phân-hệ-3-quản-lý-biến-thể--sku)
5. [Phân Hệ 4: Quản Lý Hình Ảnh Sản Phẩm (CAT-12, CAT-13)](#phân-hệ-4-quản-lý-hình-ảnh-sản-phẩm)
6. [Phân Hệ 5: Quản Lý Danh Mục (CAT-15, CAT-16, CAT-17, CAT-18, CAT-19)](#phân-hệ-5-quản-lý-danh-mục)
7. [Phân Hệ 6: API Nội Bộ Cho Các Service Khác (CAT-24, CAT-39)](#phân-hệ-6-api-nội-bộ-cho-các-service-khác)

---

## 🌐 QUY CHUẨN CHUNG

### 1. Định Tuyến & Hỗ Trợ Alias (Routing)
Mọi API đều hỗ trợ đồng thời cả 2 đường dẫn để tương thích hoàn toàn với API Gateway:
- **Đường dẫn chuẩn Gateway**: `/api/v1/...`
- **Đường dẫn có tiền tố service**: `/api/v1/catalog/...`

### 2. Định Danh Seller / Người Dùng (Authentication Headers)
Đối với các API yêu cầu quyền Seller hoặc Admin:
- Header bắt buộc: `X-Seller-Id: <uuid>` hoặc `X-User-Id: <uuid>` (hoặc trích xuất từ JWT Claim `seller_id` / `sub`).
- Nếu thiếu hoặc ID không hợp lệ: Trả về HTTP `401 Unauthorized` hoặc `400 VAL_INVALID_INPUT`.

### 3. Cấu Trúc Envelope Phản Hồi (Response Envelope)
Mọi response thành công đều tuân thủ cấu trúc chuẩn:
```json
{
  "success": true,
  "statusCode": 200,
  "message": "Sentence case success message.",
  "data": { ... },
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "timestamp": "2026-10-28T20:00:00Z"
}
```
Với dữ liệu phân trang (`paged`):
```json
{
  "success": true,
  "statusCode": 200,
  "message": "Items retrieved successfully.",
  "data": {
    "items": [ ... ],
    "pagination": {
      "pageIndex": 1,
      "pageSize": 20,
      "totalCount": 100,
      "totalPages": 5,
      "hasPreviousPage": false,
      "hasNextPage": true
    }
  },
  "traceId": "...",
  "timestamp": "..."
}
```

### 4. Quy Cách Thông Báo Lỗi
- **Ngôn ngữ**: 100% tiếng Anh.
- **Quy cách chữ**: Sentence case (Chữ cái đầu viết hoa, còn lại viết thường).
- **Mã lỗi**: `SCREAMING_SNAKE_CASE` (ví dụ: `VAL_INVALID_INPUT`, `UNAUTHORIZED`, `FORBIDDEN`, `PRODUCT_NOT_FOUND`, `DUPLICATE_SKU`).

---

## 🏪 PHÂN HỆ 1: QUẢN LÝ SẢN PHẨM CHO SELLER

### 1. CAT-01: Tạo sản phẩm bán thường
- **Mục đích**: Seller tạo một sản phẩm bán thường mới trên sàn.
- **Method & Endpoint**: `POST /api/v1/products` (alias: `/api/v1/catalog/products`)
- **Role**: `Seller`, `Admin`
- **Headers**: `X-Seller-Id: <guid>`
- **Request Body**:
  ```json
  {
    "name": "Bàn phím cơ không dây Nexus Pro",
    "description": "Bàn phím cơ 3 modes kết nối, switch Gateron Pro",
    "brand": "Nexus Tech",
    "categoryId": "cccccccc-cccc-cccc-cccc-cccccccccccc",
    "saleType": "Regular",
    "specifications": "{\"layout\": \"75%\", \"battery\": \"4000mAh\"}"
  }
  ```
- **Response**: `201 Created`
  ```json
  {
    "success": true,
    "statusCode": 201,
    "message": "Product created successfully.",
    "data": {
      "id": "22222222-2222-2222-2222-000000000001",
      "sellerId": "11111111-1111-1111-1111-000000000004",
      "categoryId": "cccccccc-cccc-cccc-cccc-cccccccccccc",
      "name": "Bàn phím cơ không dây Nexus Pro",
      "description": "Bàn phím cơ 3 modes kết nối, switch Gateron Pro",
      "brand": "Nexus Tech",
      "status": "Draft"
    }
  }
  ```
- **Ràng buộc nghiệp vụ (Business Rules)**:
  - Tên sản phẩm không được để trống (`VAL_INVALID_INPUT`).
  - Danh mục `categoryId` phải tồn tại trong hệ thống và chưa bị xóa mềm.
  - **Trạng thái ban đầu bắt buộc là `Draft`** (chưa hiển thị cho người mua).
  - Tự động gán quyền sở hữu `SellerId` từ identity người gửi.

---

### 2. CAT-02: Cập nhật thông tin sản phẩm
- **Mục đích**: Seller cập nhật thông tin chung của sản phẩm (tên, mô tả, brand, danh mục).
- **Method & Endpoint**: `PATCH /api/v1/products/{id}` hoặc `PUT /api/v1/products/{id}`
- **Role**: `Seller (chủ sở hữu)`, `Admin`
- **Headers**: `X-Seller-Id: <guid>`
- **Request Body**:
  ```json
  {
    "name": "Bàn phím cơ Nexus Pro V2",
    "description": "Mô tả mới nâng cấp pin 5000mAh",
    "brand": "Nexus Tech",
    "categoryId": "cccccccc-cccc-cccc-cccc-cccccccccccc",
    "specifications": "{\"layout\": \"75%\", \"battery\": \"5000mAh\"}"
  }
  ```
- **Response**: `200 OK` (Trả về `ProductDto` sau khi cập nhật).
- **Ràng buộc nghiệp vụ**:
  - Chỉ seller sở hữu sản phẩm mới được chỉnh sửa (`403 FORBIDDEN`).
  - Không cho phép cập nhật trạng thái qua API này (phải dùng CAT-03).

---

### 3. CAT-03: Đổi trạng thái sản phẩm (Đăng sàn / Tạm dừng bán)
- **Mục đích**: Chuyển trạng thái sản phẩm giữa `Draft`, `Active` (đăng sàn), `Inactive` (tạm dừng bán).
- **Method & Endpoint**: `PATCH /api/v1/products/{id}/status`
- **Role**: `Seller (chủ sở hữu)`, `Admin`
- **Headers**: `X-Seller-Id: <guid>`
- **Request Body**:
  ```json
  {
    "status": "ACTIVE",
    "statusReason": "SellerPaused"
  }
  ```
- **Response**: `200 OK`
  ```json
  {
    "success": true,
    "statusCode": 200,
    "message": "Product status updated successfully.",
    "data": {
      "id": "22222222-2222-2222-2222-000000000001",
      "sellerId": "11111111-1111-1111-1111-000000000004",
      "categoryId": "cccccccc-cccc-cccc-cccc-cccccccccccc",
      "name": "Bàn phím cơ không dây Nexus Pro",
      "status": "Active"
    }
  }
  ```
- **Ràng buộc nghiệp vụ & Validation (Quan trọng)**:
  - **Điều kiện kích hoạt lên sàn (`ACTIVE`)**:
    1. Sản phẩm bắt buộc phải có **ít nhất 1 biến thể (variant) đang ACTIVE có giá hợp lệ (> 0)**. Nếu không thỏa $\rightarrow$ trả `400 VAL_INVALID_INPUT` (`"Cannot activate product without at least one active variant with a valid price."`).
    2. Sản phẩm bắt buộc phải có **ít nhất 1 ảnh sản phẩm**. Nếu không thỏa $\rightarrow$ trả `400 VAL_INVALID_INPUT` (`"Cannot activate product without at least one active product image."`).
  - **Thời điểm lên sàn**: Khi chuyển từ `Draft` $\rightarrow$ `Active`, hệ thống tự động lưu mốc thời gian `PublishedAt = DateTimeOffset.UtcNow`.
  - Khi chuyển sang `Inactive`, có thể truyền `statusReason` (ví dụ `SellerPaused`, `Discontinued`).

---

### 4. CAT-04: Xóa mềm sản phẩm
- **Mục đích**: Seller gỡ bỏ sản phẩm khỏi shop.
- **Method & Endpoint**: `DELETE /api/v1/products/{id}`
- **Role**: `Seller (chủ sở hữu)`, `Admin`
- **Headers**: `X-Seller-Id: <guid>`
- **Response**: `200 OK` (`{ "deleted": true, "id": "..." }`)
- **Ràng buộc nghiệp vụ**:
  - Không xóa cứng trong database mà đánh dấu xóa mềm `IsDeleted = true`, `DeletedAt = UtcNow`.
  - Chỉ seller sở hữu mới được xóa (`403 FORBIDDEN`).

---

### 5. CAT-44: Danh sách & Tìm kiếm sản phẩm trong shop Seller
- **Mục đích**: Màn hình quản lý kho/shop của Seller, xem và tìm kiếm toàn bộ sản phẩm của mình (thấy cả Draft, Active, Inactive).
- **Method & Endpoint**: `GET /api/v1/seller/products` (alias: `/api/v1/catalog/seller/products`)
- **Role**: `Seller`
- **Headers**: `X-Seller-Id: <guid>` (bắt buộc)
- **Query Parameters**:
  - `page`: Trang hiện tại (mặc định 1).
  - `pageSize`: Số lượng / trang (mặc định 20, min 1, max 100).
  - `keyword`: Tìm theo tên sản phẩm hoặc thương hiệu.
  - `status`: Lọc theo trạng thái cụ thể (`Draft`, `Active`, `Inactive`). Bỏ trống = xem tất cả.
  - `categoryId`: Lọc theo danh mục.
  - `saleType`: `Regular` hoặc `Auction`.
  - `sortBy`: `createdAt_desc`, `createdAt_asc`, `name_asc`, `name_desc`, `price_asc`, `price_desc`.
- **Response**: `200 OK` (Paged Data)
  ```json
  {
    "success": true,
    "statusCode": 200,
    "data": {
      "items": [
        {
          "productId": "22222222-2222-2222-2222-000000000001",
          "name": "Bàn phím cơ không dây Nexus Pro",
          "brand": "Nexus Tech",
          "categoryId": "cccccccc-cccc-cccc-cccc-cccccccccccc",
          "categoryName": "Bàn phím cơ",
          "saleType": "Regular",
          "status": "Draft",
          "statusReason": null,
          "minPrice": 1250000.00,
          "maxPrice": 1550000.00,
          "primaryImageUrl": "https://res.cloudinary.com/.../keyboard.png",
          "variantCount": 3,
          "hasOrder": false,
          "createdAt": "2026-10-28T10:00:00Z",
          "publishedAt": null
        }
      ],
      "pagination": { "pageIndex": 1, "pageSize": 20, "totalCount": 1, "totalPages": 1, ... }
    }
  }
  ```
- **Ràng buộc nghiệp vụ**:
  - **Tách biệt với Buyer**: Seller xem được mọi trạng thái hàng của shop mình; Seller không thể xem danh sách của shop khác.
  - Cờ `hasOrder`: Giúp giao diện hiển thị cảnh báo sản phẩm đã phát sinh đơn hàng hay chưa.

---

### 6. CAT-45: Chi tiết sản phẩm dành cho Seller (để chỉnh sửa)
- **Mục đích**: Trả về dữ liệu chi tiết toàn diện của sản phẩm và mọi biến thể để điền vào form chỉnh sửa của Seller.
- **Method & Endpoint**: `GET /api/v1/seller/products/{id}`
- **Role**: `Seller (chủ sở hữu)`, `Admin`
- **Headers**: `X-Seller-Id: <guid>`
- **Response**: `200 OK`
  ```json
  {
    "success": true,
    "statusCode": 200,
    "data": {
      "productId": "22222222-2222-2222-2222-000000000001",
      "sellerId": "11111111-1111-1111-1111-000000000004",
      "categoryId": "cccccccc-cccc-cccc-cccc-cccccccccccc",
      "categoryName": "Bàn phím cơ",
      "name": "Bàn phím cơ không dây Nexus Pro",
      "description": "...",
      "brand": "Nexus Tech",
      "specifications": "...",
      "saleType": "Regular",
      "status": "Active",
      "hasOrder": true,
      "createdAt": "2026-10-28T10:00:00Z",
      "updatedAt": "2026-10-28T11:00:00Z",
      "publishedAt": "2026-10-28T10:30:00Z",
      "images": [
        { "id": "...", "url": "...", "isActive": true, "sortOrder": 0, "variantId": null }
      ],
      "variants": [
        {
          "variantId": "...",
          "skuCode": "KB-PRO-RED",
          "name": "Red Switch / Màu Đen",
          "price": 1250000.00,
          "currency": "VND",
          "status": "Active",
          "statusReason": null,
          "isActive": true,
          "hasOrder": true,
          "attributes": [
            { "attrName": "Switch", "attrValue": "Red" },
            { "attrName": "Color", "attrValue": "Black" }
          ],
          "images": [ ... ]
        }
      ]
    }
  }
  ```
- **Ràng buộc nghiệp vụ**:
  - Trả về toàn bộ biến thể kể cả biến thể đã bị ẩn (`Inactive`) để seller quản lý.
  - Chặn seller khác truy cập bằng `403 FORBIDDEN`.

---

## 🛒 PHÂN HỆ 2: HIỂN THỊ & TÌM KIẾM CHO BUYER (NGƯỜI MUA)

### 1. CAT-05: Xem chi tiết sản phẩm (Buyer)
- **Method & Endpoint**: `GET /api/v1/products/{id}`
- **Role**: `Public (Khách vãng lai, Người mua)`
- **Response**: `200 OK` (Trả về `ProductDetailDto`).
- **Ràng buộc nghiệp vụ**:
  - **Chỉ hiển thị khi `Status == Active` và `!IsDeleted`**. Nếu sản phẩm đang ở `Draft` hoặc `Inactive` $\rightarrow$ Trả về `404 PRODUCT_NOT_FOUND`.
  - Giấu toàn bộ thông tin nhạy cảm của hệ thống.

---

### 2. CAT-06: Danh sách sản phẩm trên sàn (Buyer)
- **Method & Endpoint**: `GET /api/v1/products`
- **Role**: `Public`
- **Query Parameters**: `page`, `pageSize`, `keyword`, `categoryId`, `sellerId`, `saleType`, `sortBy`.
- **Ràng buộc nghiệp vụ**:
  - **Lọc cứng**: Chỉ lấy các sản phẩm `Status == Active` và chưa bị xóa. Khách hàng không bao giờ nhìn thấy sản phẩm nháp hay sản phẩm tạm khóa.

---

### 3. CAT-20 + CAT-21: Tìm kiếm & Lọc sản phẩm nâng cao
- **Method & Endpoint**: `GET /api/v1/products/search`
- **Query Parameters**:
  - `keyword`: Từ khóa tìm kiếm.
  - `minPrice`, `maxPrice`: Khoảng giá (validate: `minPrice <= maxPrice`, nếu sai trả `VAL_INVALID_PRICE_RANGE`).
  - `brand`: Thương hiệu.
  - `minRating`: Số sao đánh giá tối thiểu (0.0 - 5.0).
  - `sortBy`: `priceAsc`, `priceDesc`, `rating`, `newest`.

---

## 🏷️ PHÂN HỆ 3: QUẢN LÝ BIẾN THỂ (PRODUCT VARIANT / SKU)

### 1. CAT-07: Thêm biến thể mới (Add Variant)
- **Method & Endpoint**: `POST /api/v1/products/{id}/variants`
- **Role**: `Seller (chủ sở hữu)`
- **Headers**: `X-Seller-Id: <guid>`
- **Request Body**:
  ```json
  {
    "skuCode": "KB-PRO-BLUE",
    "name": "Blue Switch / Màu Trắng",
    "price": 1350000.00,
    "currency": "VND",
    "attributes": [
      { "attrName": "Switch", "attrValue": "Blue" },
      { "attrName": "Color", "attrValue": "White" }
    ]
  }
  ```
- **Ràng buộc nghiệp vụ quan trọng**:
  - **Giới hạn số lượng**: Mỗi sản phẩm **không được vượt quá 10 biến thể** (`currentVariantCount >= 10` trả về lỗi: `"A product cannot exceed 10 variants."`).
  - **Mã SkuCode**: Seller được phép tự do nhập mã.
  - **Tính duy nhất của SKU**: Mã SkuCode có thể trùng giữa các seller khác nhau, nhưng **bắt buộc phải duy nhất trong nội bộ shop của cùng 1 seller** (`v.Product.SellerId == currentSellerId && v.SkuCode == sku` $\rightarrow$ `409 DUPLICATE_SKU`).
  - Giá `price` phải lớn hơn 0.

---

### 2. CAT-08: Sửa thông tin biến thể
- **Method & Endpoint**: `PATCH /api/v1/variants/{id}` hoặc `PUT /api/v1/variants/{id}`
- **Role**: `Seller (chủ sở hữu)`
- **Request Body**: Cập nhật `name`, `price`, `currency`, danh sách thuộc tính `attributes`.
- **Ràng buộc nghiệp vụ**:
  - Chỉ seller sở hữu sản phẩm chứa biến thể đó mới được phép chỉnh sửa (`403 FORBIDDEN`).
  - Giá mới cập nhật không được nhỏ hơn hoặc bằng 0.

---

### 3. CAT-09: Đổi trạng thái biến thể
- **Method & Endpoint**: `PATCH /api/v1/variants/{id}/status`
- **Request Body**:
  ```json
  {
    "status": "INACTIVE",
    "statusReason": "OutOfStock"
  }
  ```
- **Ràng buộc**: Chuyển trạng thái giữa `ACTIVE` và `INACTIVE`.

---

### 4. CAT-10: Đặt biến thể mặc định
- **Method & Endpoint**: `PUT /api/v1/products/{id}/default-variant`
- **Request Body**: `{ "variantId": "<guid>" }`
- **Ràng buộc**:
  - Biến thể được chọn làm mặc định phải thuộc về sản phẩm này và đang ở trạng thái `ACTIVE`.
  - Hệ thống tự động gán cờ `IsActive = true` cho biến thể được chọn và bỏ mặc định của các biến thể còn lại.

---

### 5. CAT-11: Xóa biến thể
- **Method & Endpoint**: `DELETE /api/v1/variants/{id}`
- **Ràng buộc nghiệp vụ bảo vệ đơn hàng**:
  - **Chặn xóa khi đã có đơn hàng**: Nếu biến thể đã phát sinh đơn hàng (`HasOrder == true`), **tuyệt đối không cho xóa** $\rightarrow$ Trả về `409 CONFLICT` (`"Cannot delete a variant that has existing orders. You can set its status to INACTIVE instead."`).
  - **Chặn xóa biến thể cuối cùng**: Một sản phẩm phải có ít nhất 1 biến thể. Không được xóa nếu sản phẩm chỉ còn duy nhất 1 biến thể (`remainingCount <= 1` trả về `409 CONFLICT`).
  - Tự động chuyển cờ mặc định sang một biến thể còn lại nếu biến thể bị xóa đang là mặc định.

---

### 6. CAT-42: Xem chi tiết biến thể
- **Method & Endpoint**: `GET /api/v1/variants/{id}`
- **Role**: `Seller`, `Admin`
- **Mục đích**: Lấy thông tin chi tiết đầy đủ của 1 variant theo ID phục vụ màn hình chỉnh sửa nhanh.

---

## 🖼️ PHÂN HỆ 4: QUẢN LÝ HÌNH ẢNH SẢN PHẨM

### 1. CAT-12: Tải ảnh sản phẩm lên
- **Method & Endpoint**: `POST /api/v1/products/{id}/images`
- **Content-Type**: `multipart/form-data`
- **Form Data**:
  - `file`: Tệp hình ảnh (`.jpg`, `.jpeg`, `.png`, `.webp`, tối đa 5MB).
  - `variantId`: (Tùy chọn) Gắn ảnh đại diện riêng cho 1 biến thể cụ thể.
  - `altText`: Mô tả ảnh.
  - `sortOrder`: Thứ tự hiển thị.
- **Ràng buộc**:
  - Tự động upload ảnh thật lên cloud storage (Cloudflare Images / Cloudinary).
  - Validate định dạng ảnh và dung lượng file hợp lệ.

---

### 2. CAT-13: Xóa ảnh sản phẩm
- **Method & Endpoint**: `DELETE /api/v1/products/{id}/images/{imageId}`
- **Ràng buộc**: Xóa mềm bản ghi ảnh trong hệ thống; chỉ chủ sở hữu sản phẩm mới được xóa.

---

## 📂 PHÂN HỆ 5: QUẢN LÝ DANH MỤC (CATEGORY)

### 1. CAT-15: Tạo danh mục mới
- **Method & Endpoint**: `POST /api/v1/categories` (alias: `/api/v1/catalog/categories`)
- **Role**: `Admin`
- **Request Body**:
  ```json
  {
    "name": "Bàn phím cơ",
    "description": "Các loại bàn phím cơ máy tính",
    "parentId": null,
    "sortOrder": 1
  }
  ```
- **Ràng buộc nghiệp vụ quan trọng**:
  - **Tên Category là duy nhất trên toàn hệ thống**: Không được phép trùng tên với bất kỳ category nào khác (so sánh không phân biệt hoa/thường, tự động loại bỏ khoảng trắng thừa). Nếu trùng $\rightarrow$ Trả `400 VAL_INVALID_INPUT` (`"Category with name '...' already exists."`).
  - Cây danh mục tối đa 3 cấp (`Level <= 3`).

---

### 2. CAT-16: Sửa danh mục
- **Method & Endpoint**: `PUT /api/v1/categories/{id}` hoặc `PATCH /api/v1/categories/{id}`
- **Ràng buộc nghiệp vụ**:
  - Tên cập nhật mới không được trùng với các danh mục hiện có khác trong hệ thống.
  - Không cho phép chọn chính nó hoặc các danh mục con cháu làm danh mục cha (`CATEGORY_CYCLE`).

---

### 3. CAT-17: Xóa mềm danh mục
- **Method & Endpoint**: `DELETE /api/v1/categories/{id}`
- **Ràng buộc**: Đánh dấu `IsDeleted = true`.

---

### 4. CAT-18: Xem chi tiết danh mục
- **Method & Endpoint**: `GET /api/v1/categories/{id}`
- **Response**: Trả về thông tin danh mục, danh mục cha (`parent`) và danh sách các danh mục con trực tiếp (`children`).

---

### 5. CAT-19: Danh sách & Cây danh mục
- **Method & Endpoint**: `GET /api/v1/categories`
- **Query Parameters**:
  - `tree`: `true` (trả về cây danh mục đệ quy cha-con) hoặc `false` (danh sách phẳng).
  - `keyword`: Tìm kiếm danh mục theo tên.
  - `parentId`: Lọc danh mục theo cha.

---

## ⚡ PHÂN HỆ 6: API NỘI BỘ LIÊN SERVICE (INTERNAL APIS)

### 1. CAT-24: Lấy thông tin nhiều biến thể cùng lúc (Batch Get Variants)
- **Mục đích**: Phục vụ **Cart Service** và **Order Service** lấy thông tin giá, tên, ảnh, trạng thái của danh sách ID sản phẩm có trong giỏ hàng.
- **Method & Endpoint**: `POST /api/v1/internal/variants/batch` (alias: `/api/v1/catalog/internal/variants/batch`)
- **Request Body**:
  ```json
  {
    "variantIds": [
      "10000000-0000-0000-0000-000000000001",
      "10000000-0000-0000-0000-000000000002"
    ]
  }
  ```
- **Response**: Trả về danh sách `items` chứa đầy đủ `skuCode`, `price`, `currency`, `status`, `productStatus`, `primaryImageUrl`, `attributes` và danh sách các `notFoundIds` nếu có ID không tồn tại.

---

### 2. CAT-39: Kiểm tra trạng thái mua được của biến thể (Check Purchasable Status)
- **Mục đích**: Phục vụ **Cart / Checkout / Auction Service** kiểm tra điều kiện hàng có được phép mua hay không trước khi tiến hành thanh toán đơn hàng.
- **Method & Endpoint**: `POST /api/v1/internal/variants/check-status`
- **Request Body**:
  ```json
  {
    "variantIds": [ "10000000-0000-0000-0000-000000000001" ],
    "purpose": "CHECKOUT"
  }
  ```
- **Response**:
  ```json
  {
    "success": true,
    "statusCode": 200,
    "data": {
      "items": [
        {
          "variantId": "10000000-0000-0000-0000-000000000001",
          "productId": "...",
          "productStatus": "Active",
          "variantStatus": "Active",
          "saleType": "Regular",
          "isPurchasable": true,
          "reason": "OK",
          "price": 1250000.00,
          "currency": "VND"
        }
      ],
      "notFoundIds": []
    }
  }
  ```
- **Quy tắc kiểm tra (`isPurchasable`)**:
  - Hàng chỉ mua được khi: Sản phẩm `Status == Active` VÀ Biến thể `Status == Active` VÀ chưa bị xóa mềm.
  - Nếu sản phẩm bị ẩn hoặc hết hàng, `isPurchasable = false` kèm lý do chi tiết trong trường `reason`.

---

## 📋 BẢNG TỔNG HỢP MÃ LỖI CHUẨN TOÀN HỆ THỐNG (ERROR CODES)

| Mã lỗi HTTP | Mã lỗi SCREAMING_SNAKE_CASE | Ý nghĩa |
| :---: | :--- | :--- |
| **400** | `VAL_INVALID_INPUT` | Dữ liệu đầu vào không hợp lệ (tên rỗng, ID sai định dạng, danh mục trùng tên...). |
| **400** | `VAL_INVALID_PAGINATION` | Phân trang sai (`page < 1` hoặc `pageSize` ngoài khoảng 1..100). |
| **400** | `VAL_INVALID_PRICE_RANGE` | Khoảng giá không hợp lệ (`minPrice > maxPrice`). |
| **401** | `UNAUTHORIZED` | Thiếu danh tính xác thực (thiếu header `X-Seller-Id` hoặc token không hợp lệ). |
| **403** | `FORBIDDEN` / `PERMISSION_DENIED` | Không có quyền thao tác trên sản phẩm của người khác. |
| **404** | `PRODUCT_NOT_FOUND` | Sản phẩm không tồn tại hoặc đã bị xóa mềm. |
| **404** | `CATEGORY_NOT_FOUND` | Danh mục không tồn tại. |
| **404** | `VARIANT_NOT_FOUND` | Biến thể không tồn tại. |
| **409** | `DUPLICATE_SKU` / `SKU_DUPLICATED` | Trùng mã SKU trong nội bộ shop của Seller. |
| **409** | `CONFLICT` (`HAS_ORDER`) | Chặn xóa biến thể vì đã từng có đơn hàng liên kết. |
| **409** | `CONFLICT` (`LAST_VARIANT`) | Chặn xóa vì đây là biến thể duy nhất còn lại của sản phẩm. |

