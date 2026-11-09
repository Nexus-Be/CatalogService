# HỢP ĐỒNG KẾT NỐI LIÊN SERVICE: CATALOG SERVICE <-> AUCTION SERVICE (PUSH-DRIVEN)

> **Mục đích:** Đặc tả giao tiếp HTTP REST nội bộ (Internal Inter-Service Communication) giữa **AuctionService** và **CatalogService** (port `8002` trong Docker / URL nội bộ `http://catalog-service:8002`).
> **Nguyên tắc:** AuctionService là bên chủ động gọi (Push) sang CatalogService khi vòng đời phiên đấu giá thay đổi. Không cần Token JWT người dùng (nội bộ qua Docker network).

---

## 1. Danh sách Endpoints nội bộ

| STT | Endpoint | Method | Thời điểm AuctionService gọi | Ý nghĩa |
| :---: | :--- | :---: | :--- | :--- |
| **1** | `/api/v1/internal/products/{productId}/auction-eligibility` | `GET` | Trước khi cho phép tạo/mở phiên đấu giá | Tiền kiểm tra: Sản phẩm có tồn tại, có đúng loại Đấu giá (`SaleType=Auction`), có đang `Active`, có đủ ảnh không. |
| **2** | `/api/v1/internal/products/{productId}/lock-auction` | `POST` | Ngay khi phiên đấu giá bắt đầu mở | Khóa sản phẩm: Đổi trạng thái sang `InAuction`. Catalog chặn tuyệt đối mọi hành vi sửa, xóa, ngừng bán. |
| **3** | `/api/v1/internal/products/{productId}/unlock-auction` | `POST` | Khi phiên đấu giá bị hủy hoặc hết giờ không ai mua | Mở khóa sản phẩm: Trả trạng thái về `Active` để Seller có thể mở phiên khác hoặc quản lý lại. |
| **4** | `/api/v1/internal/products/{productId}/mark-auctioned` | `POST` | Khi phiên đấu giá kết thúc thành công (có người thắng) | Chốt bán sản phẩm: Chuyển vĩnh viễn sang `Auctioned` (khóa cứng vĩnh viễn). |

---

## 2. Chi tiết từng Endpoint

### 2.1. Tiền kiểm tra điều kiện mở phiên (`auction-eligibility`)
* **URL:** `GET http://catalog-service:8002/api/v1/internal/products/{productId}/auction-eligibility`
* **Response (200 OK):**
```json
{
  "productId": "56b9292a-d83d-484f-9018-6df7ae3727c9",
  "isEligible": true,
  "reason": null,
  "status": "Active",
  "skuCode": "AUC-56B9292A-DEF",
  "weightGrams": 350
}
```
* **Khi không đủ điều kiện (200 OK nhưng `isEligible: false`):**
```json
{
  "productId": "56b9292a-d83d-484f-9018-6df7ae3727c9",
  "isEligible": false,
  "reason": "Product is already locked in an ongoing auction.",
  "status": "InAuction",
  "skuCode": "AUC-56B9292A-DEF",
  "weightGrams": 350
}
```

---

### 2.2. Khóa sản phẩm khi mở phiên (`lock-auction`)
* **URL:** `POST http://catalog-service:8002/api/v1/internal/products/{productId}/lock-auction`
* **Response Thành công (200 OK):**
```json
{
  "id": "56b9292a-d83d-484f-9018-6df7ae3727c9",
  "sellerId": "cccccccc-3333-3333-3333-333333333333",
  "categoryId": "10000000-0000-0000-0000-000000000001",
  "name": "Eligible Seller Auction Watch",
  "status": "InAuction",
  "weightGrams": 350
}
```
* **Lỗi có thể trả về:**
  - `400 Bad Request` (`PRODUCT_ALREADY_IN_AUCTION`): Sản phẩm đã đang nằm trong một phiên đấu giá mở khác.
  - `400 Bad Request` (`PRODUCT_ALREADY_AUCTIONED`): Sản phẩm đã bán xong trước đó.
  - `400 Bad Request` (`VAL_INVALID_INPUT`): Sản phẩm không ở trạng thái Active (ví dụ đang Draft hoặc Inactive).
  - `404 Not Found` (`PRODUCT_NOT_FOUND`): Không tìm thấy sản phẩm.

---

### 2.3. Mở khóa sản phẩm (`unlock-auction`)
* **URL:** `POST http://catalog-service:8002/api/v1/internal/products/{productId}/unlock-auction`
* **Response Thành công (200 OK):**
```json
{
  "id": "56b9292a-d83d-484f-9018-6df7ae3727c9",
  "sellerId": "cccccccc-3333-3333-3333-333333333333",
  "categoryId": "10000000-0000-0000-0000-000000000001",
  "name": "Eligible Seller Auction Watch",
  "status": "Active",
  "weightGrams": 350
}
```
* **Lỗi có thể trả về:**
  - `400 Bad Request` (`PRODUCT_NOT_IN_AUCTION`): Sản phẩm hiện không ở trạng thái `InAuction`.
  - `404 Not Found` (`PRODUCT_NOT_FOUND`): Không tìm thấy sản phẩm.

---

### 2.4. Chốt bán sản phẩm thành công (`mark-auctioned`)
* **URL:** `POST http://catalog-service:8002/api/v1/internal/products/{productId}/mark-auctioned`
* **Response Thành công (200 OK):**
```json
{
  "id": "56b9292a-d83d-484f-9018-6df7ae3727c9",
  "sellerId": "cccccccc-3333-3333-3333-333333333333",
  "categoryId": "10000000-0000-0000-0000-000000000001",
  "name": "Eligible Seller Auction Watch",
  "status": "Auctioned",
  "weightGrams": 350
}
```
* **Tính chất:** Idempotent (nếu đã là `Auctioned`, gọi lại vẫn trả về 200 OK thành công).

---

## 3. Ma trận mã lỗi chuẩn (Error Code Matrix)

| HTTP Status | Error Code (`errorCode`) | Error Message (`detail`) | Nguyên nhân |
| :---: | :--- | :--- | :--- |
| `400` | `PRODUCT_IN_AUCTION` | Product is currently in an active auction and cannot be modified. | Seller cố tình sửa/xóa/ẩn sản phẩm khi phiên đang mở. |
| `400` | `PRODUCT_ALREADY_IN_AUCTION` | Product is already locked in an active auction. | Auction gọi khóa một sản phẩm đã bị khóa trước đó. |
| `400` | `PRODUCT_NOT_IN_AUCTION` | Product is not currently in an active auction. Current status is '{status}'. | Auction gọi mở khóa cho một sản phẩm không ở trạng thái InAuction. |
| `400` | `PRODUCT_ALREADY_AUCTIONED` | Auctioned products cannot be put into auction again. | Sản phẩm đã bán xong vĩnh viễn, không được đấu giá lại. |
| `404` | `PRODUCT_NOT_FOUND` | Product with id '{id}' was not found. | ID sản phẩm không tồn tại trong Catalog. |

