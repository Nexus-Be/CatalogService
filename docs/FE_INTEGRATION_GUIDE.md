# HƯỚNG DẪN TÍCH HỢP FRONTEND (FE INTEGRATION GUIDE)
## CÁC PHÂN HỆ: ĐÁNH GIÁ (REVIEW), DUYỆT SẢN PHẨM (APPROVAL/MODERATION), TỐ CÁO (REPORT) & KHÓA SẢN PHẨM (BAN/SUSPEND)

> **Dành cho:** Đội ngũ Frontend (Web / Mobile App)  
> **Dịch vụ phụ trách:** `CatalogService` (Port 8002 / Gateway 8080)  
> **Nguyên tắc vàng:** **CHỈ SỬ DỤNG CÁC ENDPOINT VÀ CONTRACT ĐƯỢC ĐẶC TẢ TẠI TÀI LIỆU NÀY.** Tuyệt đối không dùng các endpoint tự chế hoặc tài liệu nháp cũ không đồng bộ.  
> **Trạng thái:** ✅ ĐÃ KIỂM CHỨNG TRỰC TIẾP TRÊN MÔI TRƯỜNG THỰC TẾ  
> **Cập nhật lần cuối:** 2026-11-12  

---

## 📌 MỤC LỤC NHANH
1. [Nguyên tắc chung & Cấu hình Header](#1-nguyên-tắc-chung--cấu-hình-header)
2. [Bảng tóm tắt các Endpoints](#2-bảng-tóm-tắt-các-endpoints)
3. [Phân hệ 1: Đánh giá & Xếp hạng sản phẩm (Reviews & Ratings)](#3-phân-hệ-1-đánh-giá--xếp-hạng-sản-phẩm-reviews--ratings)
4. [Phân hệ 2: Duyệt & Kiểm duyệt sản phẩm (Product Moderation & Status)](#4-phân-hệ-2-duyệt--kiểm-duyệt-sản-phẩm-product-moderation--status)
5. [Phân hệ 3: Tố cáo sản phẩm vi phạm (Product Violation Reports)](#5-phân-hệ-3-tố-cáo-sản-phẩm-vi-phạm-product-violation-reports)
6. [Cảnh báo: 6 Sai lầm & Vấn đề thường gặp FE cần tránh](#6-cảnh-báo-6-sai-lầm--vấn-đề-thường-gặp-fe-cần-tránh)
7. [Code mẫu Javascript / Typescript (Copy-Paste Ready)](#7-code-mẫu-javascript--typescript-copy-paste-ready)
8. [Hệ thống Thông báo In-App & Rung chuông Realtime](#8-hệ-thống-thông-báo-in-app--rung-chuông-realtime)

---

## 1. Nguyên tắc chung & Cấu hình Header

### 1.1 Base URL & Tình trạng Hỗ trợ của API Gateway (Port 8080 vs 8002)
- **Qua API Gateway (Khuyến nghị cho Staging / Production):** `http://localhost:8080` (hoặc domain gateway chung của sàn).
- **Trực tiếp Catalog Service (Local dev / Trực tiếp khi Gateway chưa mapping đủ route):** `http://localhost:8002`.

> ⚠️ **LƯU Ý CỰC KỲ QUAN TRỌNG VỀ ĐỊNH TUYẾN GATEWAY (PORT 8080):**
> Hiện tại, cấu hình YARP của Gateway (`nexus-gateway:8080`) nạp các route theo tiền tố URL:
> 1. **Các endpoint ĐƯỢC GATEWAY HỖ TRỢ 100%:** Mọi endpoint bắt đầu bằng `/api/v1/products/*`, `/api/v1/categories/*`, `/api/v1/variants/*`, `/api/v1/images/*`, `/api/v1/seller/products/*`.
>    - Ví dụ: `GET/POST /api/v1/products/{id}/reviews`, `POST /api/v1/products/{id}/reports`, `GET /api/v1/products/{id}/rating-summary` đều chạy tốt qua `http://localhost:8080`.
> 2. **Các endpoint CHƯA ĐƯỢC MAP TRONG GATEWAY (Sẽ nhận `404 Not Found` từ Gateway):**
>    - `PUT /api/v1/reviews/{id}` (Sửa đánh giá)
>    - `DELETE /api/v1/reviews/{id}` (Xóa đánh giá)
>    - `GET /api/v1/admin/products` & `PATCH /api/v1/admin/products/{id}/moderate` (Duyệt/Khóa SP)
>    - `GET /api/v1/admin/reports/products` & `PUT /api/v1/admin/reports/products/{reportId}/action` (Xử lý tố cáo)
>    - `PATCH /api/v1/admin/reviews/{id}/moderate` (Ẩn review)
>    👉 **Cách khắc phục cho FE:** Trong lúc chờ Gateway nạp thêm 4 cụm route (`reviews-route`, `admin-products-route`, `admin-reports-route`, `admin-reviews-route`), phía FE nên cấu hình Base URL động: các API trên gọi trực tiếp đến `http://localhost:8002`.

### 1.2 Headers bắt buộc
| Loại API | Header bắt buộc | Giá trị |
| :--- | :--- | :--- |
| **API Công khai (Public)** | `Content-Type` | `application/json` |
| **API Cần đăng nhập (User/Buyer/Seller/Admin)** | `Authorization` | `Bearer <access_token>` |
| **API Upload file ảnh (Reviews / Reports)** | `Content-Type` | **KHÔNG SET MANUAL** (để trình duyệt tự set `multipart/form-data; boundary=...`) |

### 1.3 Quy chuẩn Response & Xử lý Lỗi (RFC 9110)
- **Thành công (200 / 201):** Trả về đối tượng JSON DTO thuần túy.
- **Lỗi (400, 401, 403, 404, 409):** Trả về định dạng ProblemDetails chuẩn:
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errorCode": "VAL_INVALID_INPUT",
  "detail": "Description must be between 10 and 2000 characters.",
  "errors": {
    "Description": ["Description must be between 10 and 2000 characters."]
  }
}
```
> **Lưu ý:** Mã lỗi (`errorCode`) luôn là `SCREAMING_SNAKE_CASE`. FE nên căn cứ vào `errorCode` để hiển thị Toast/Alert song ngữ phù hợp cho người dùng.

---

## 2. Bảng tóm tắt các Endpoints & Trạng thái Gateway

| STT | Phương thức | Endpoint Route | Quyền hạn (Role) | Chức năng FE | Content-Type | Hỗ trợ Gateway (Port 8080) |
| :---: | :---: | :--- | :---: | :--- | :--- | :---: |
| **01** | `GET` | `/api/v1/products/{id}/rating-summary` | Public | Xem thống kê sao & điểm trung bình | `application/json` | ✅ Sẵn sàng |
| **02** | `GET` | `/api/v1/products/{id}/reviews` | Public | Xem danh sách đánh giá của sản phẩm | `application/json` | ✅ Sẵn sàng |
| **03** | `POST` | `/api/v1/products/{id}/reviews` | Buyer | Gửi đánh giá sau khi mua hàng | `multipart/form-data` | ✅ Sẵn sàng |
| **04** | `PUT` | `/api/v1/reviews/{id}` | Buyer | Sửa đánh giá của chính mình (trong 30 ngày) | `multipart/form-data` | ⚠️ Gọi `:8002` (Thiếu route GW) |
| **05** | `DELETE`| `/api/v1/reviews/{id}` | Buyer, Admin | Xóa bài đánh giá | `application/json` | ⚠️ Gọi `:8002` (Thiếu route GW) |
| **06** | `PATCH` | `/api/v1/admin/reviews/{id}/moderate` | Admin, Staff | Ẩn/Hiện bài đánh giá vi phạm | `application/json` | ⚠️ Gọi `:8002` (Thiếu route GW) |
| **07** | `GET` | `/api/v1/admin/products` | Admin, Staff | Danh sách sản phẩm chờ duyệt / toàn sàn | `application/json` | ⚠️ Gọi `:8002` (Thiếu route GW) |
| **08** | `PATCH` | `/api/v1/admin/products/{id}/moderate` | Admin, Staff | Duyệt / Từ chối / Tạm khóa / Mở khóa / Cấm vĩnh viễn | `application/json` | ⚠️ Gọi `:8002` (Thiếu route GW) |
| **09** | `PATCH` | `/api/v1/products/{id}/status` | Seller | Người bán tự Tắt/Bật bán sản phẩm của mình | `application/json` | ✅ Sẵn sàng |
| **10** | `POST` | `/api/v1/products/{id}/reports` | Buyer, Seller | Gửi tố cáo sản phẩm vi phạm (bắt buộc ảnh) | `multipart/form-data` | ✅ Sẵn sàng |
| **11** | `GET` | `/api/v1/admin/reports/products` | Admin, Staff | Danh sách hàng đợi đơn tố cáo | `application/json` | ⚠️ Gọi `:8002` (Thiếu route GW) |
| **12** | `PUT` | `/api/v1/admin/reports/products/{reportId}/action` | Admin, Staff | Phán quyết đơn tố cáo (Cấm sản phẩm / Bác bỏ) | `application/json` | ⚠️ Gọi `:8002` (Thiếu route GW) |

---

## 3. Phân hệ 1: Đánh giá & Xếp hạng sản phẩm (Reviews & Ratings)

### 3.1 Xem tổng quan thống kê sao sản phẩm
- **Endpoint:** `GET /api/v1/products/{id}/rating-summary`
- **Quyền:** Public (Không cần token)
- **Mục đích:** Hiển thị box tổng quan trên trang chi tiết sản phẩm (PDP).
- **Response mẫu (200 OK):**
```json
{
  "avgRating": 4.67,
  "reviewCount": 3,
  "starSum": 14,
  "starDistribution": {
    "fiveStar": 2,
    "fourStar": 1,
    "threeStar": 0,
    "twoStar": 0,
    "oneStar": 0
  }
}
```

---

### 3.2 Xem danh sách đánh giá của sản phẩm
- **Endpoint:** `GET /api/v1/products/{id}/reviews`
- **Quyền:** Public
- **Query Parameters:**
  - `page`: số trang (mặc định: `1`)
  - `pageSize`: số bài/trang (mặc định: `10`, tối đa `50`)
  - `rating`: lọc theo số sao cụ thể (`1`, `2`, `3`, `4`, `5`). Bỏ trống để lấy tất cả.
  - `sortBy`: `newest` (mới nhất), `highest_rating` (sao cao nhất), `lowest_rating` (sao thấp nhất).
- **Response mẫu (200 OK):**
```json
{
  "items": [
    {
      "id": "68427db9-db42-4801-a6d1-cee58d885808",
      "productId": "0fdd3b17-786b-46be-8382-e58c51eced8e",
      "buyerId": "dddddddd-4444-4444-4444-444444444444",
      "rating": 5,
      "comment": "Chất lượng đàn xuất sắc, âm thanh vang và rất sáng!",
      "reviewImages": [
        "https://res.cloudinary.com/u6yxkkop/image/upload/v1794461553/nexus_catalog/reviews/front.png",
        "https://res.cloudinary.com/u6yxkkop/image/upload/v1794461553/nexus_catalog/reviews/back.png"
      ],
      "status": "Published",
      "createdAt": "2026-11-12T07:11:32Z",
      "updatedAt": null
    }
  ],
  "metadata": {
    "page": 1,
    "pageSize": 10,
    "totalItems": 1,
    "totalPages": 1
  }
}
```

---

### 3.3 Người mua gửi đánh giá sản phẩm sau khi nhận hàng
- **Endpoint:** `POST /api/v1/products/{id}/reviews`
- **Quyền:** Bắt buộc đăng nhập với role `buyer`
- **Format:** `multipart/form-data`
- **Form Fields:**
  | Tên trường Form | Kiểu | Bắt buộc | Ràng buộc | Mô tả |
  | :--- | :--- | :---: | :--- | :--- |
  | `orderId` | `string` | **Có** | UUIDv4 | Mã đơn hàng đã hoàn thành |
  | `rating` | `number` | **Có** | Số nguyên từ 1 đến 5 | Số sao đánh giá |
  | `comment` | `string` | Không | Tối đa 2000 ký tự | Nhận xét chi tiết |
  | `reviewImages` | `File` (Binary) | Không (Tùy chọn) | Tối đa 5 file, $\le$ 5MB/file. Định dạng: `.jpg`, `.jpeg`, `.png`, `.webp` | Ảnh chụp thực tế của sản phẩm |

- **Các điều kiện rào chắn (Business Rules):**
  1. Người mua phải thực sự đã mua sản phẩm này trong đơn hàng `orderId` (nếu không $\to$ `403 PURCHASE_NOT_FOUND`).
  2. Thời hạn đánh giá tối đa **30 ngày** kể từ ngày giao hàng thành công (nếu quá $\to$ `400 REVIEW_WINDOW_EXPIRED`).
  3. Mỗi đơn hàng chỉ được đánh giá 1 lần duy nhất (nếu trùng $\to$ `409 ALREADY_REVIEWED`).
  4. Sản phẩm không bị cấm (nếu cấm $\to$ `400 PRODUCT_BANNED`).
- **Tự động hóa Backend:** Khi đánh giá thành công, CatalogService tự động gọi UserService điều chỉnh điểm uy tín của Shop bán hàng. **FE không cần và không được tự gọi API điểm uy tín.**

---

### 3.4 Người mua chỉnh sửa đánh giá
- **Endpoint:** `PUT /api/v1/reviews/{id}`
- **Quyền:** Bắt buộc `buyer` sở hữu bài đánh giá
- **Format:** `multipart/form-data`
- **Form Fields:** `rating` (bắt buộc: 1..5), `comment` (tùy chọn), `reviewImages` (tùy chọn).
- **Rào chắn:** Chỉ được chỉnh sửa trong vòng **30 ngày** kể từ khi đăng đánh giá (`400 REVIEW_UPDATE_EXPIRED`).

---

### 3.5 Admin/Staff kiểm duyệt (Ẩn/Hiện) bài đánh giá
- **Endpoint:** `PATCH /api/v1/admin/reviews/{id}/moderate`
- **Quyền:** `admin` hoặc `staff`
- **Format:** `application/json`
- **Request Body:**
```json
{
  "action": "HIDE", // Hoặc "PUBLISH" để hiện lại
  "note": "Đánh giá chứa từ ngữ xúc phạm hoặc hình ảnh không liên quan"
}
```

---

## 4. Phân hệ 2: Duyệt & Kiểm duyệt sản phẩm (Product Moderation & Status)

### 4.1 Vòng đời trạng thái sản phẩm (State Machine)
- `Draft`: Sản phẩm người bán mới tạo hoặc bị Admin từ chối duyệt (`Rejected`). Chưa bán được.
- `Pending`: Sản phẩm đã nộp đầy đủ thông tin, đang chờ Admin/Staff duyệt.
- `Active`: Sản phẩm đang hiển thị và bán trên sàn.
- `Inactive`: Tạm dừng bán (do người bán tự tắt hoặc Admin tạm khóa `SUSPEND`).
- `Banned`: **Bị cấm vĩnh viễn** do vi phạm pháp luật / hàng giả. Không bao giờ được kích hoạt lại.
- `Auctioned`: Sản phẩm đấu giá đã chốt phiên thành công.

---

### 4.2 Admin/Staff tra cứu danh sách sản phẩm cần duyệt
- **Endpoint:** `GET /api/v1/admin/products`
- **Quyền:** `admin`, `staff`
- **Query Parameters:**
  - `status`: Lọc theo trạng thái (`Pending`, `Active`, `Inactive`, `Draft`, `Banned`). FE làm trang kiểm duyệt hãy truyền `status=Pending`.
  - `keyword`: Tìm theo tên sản phẩm, thương hiệu.
  - `page`, `pageSize`: Phân trang.
  - `sellerId`, `categoryId`, `saleType`: Lọc nâng cao.

---

### 4.3 Admin/Staff thực hiện duyệt, tạm khóa hoặc cấm sản phẩm
- **Endpoint:** `PATCH /api/v1/admin/products/{id}/moderate`
- **Quyền:** `admin`, `staff`
- **Format:** `application/json`
- **Request Body:**
```json
{
  "action": "APPROVE", // APPROVE, REJECT, SUSPEND, UNLOCK, BAN
  "reason": "Sản phẩm đáp ứng đầy đủ tiêu chuẩn kiểm định."
}
```

| Giá trị `action` | Trạng thái mới | Yêu cầu kiểm tra từ Backend | Tác động hệ thống |
| :--- | :--- | :--- | :--- |
| **`APPROVE`** | `Active` | Sản phẩm phải có ít nhất 1 ảnh active, ít nhất 1 biến thể SKU active có giá > 0, và cân nặng vận chuyển `WeightGrams > 0`. | Tự động đồng bộ khởi tạo tồn kho kho vật lý sang `FulfillmentService`. |
| **`REJECT`** | `Draft` | Bắt buộc truyền `reason` để người bán biết lý do sửa lại. | Ghi nhận `StatusReason = Rejected`. |
| **`SUSPEND`** | `Inactive` | Tạm khóa do khiếu nại nhẹ hoặc chờ bổ sung giấy tờ. | Toàn bộ biến thể SKU bị tắt `Inactive`. Sản phẩm ẩn khỏi trang chủ. |
| **`UNLOCK`** | `Active` | Mở khóa lại sản phẩm đã bị `SUSPEND`. | Sản phẩm xuất hiện lại trên sàn. |
| **`BAN`** | `Banned` | Dùng khi vi phạm nghiêm trọng (hàng cấm, hàng giả). | **Cấm vĩnh viễn** không thể mở lại. Tự động gọi UserService **trừ 10 điểm uy tín** của Shop bán hàng. |

---

### 4.4 Người bán (Seller) tự Bật/Tắt bán sản phẩm của mình
- **Endpoint:** `PATCH /api/v1/products/{id}/status`
- **Quyền:** `seller` chính chủ sở hữu sản phẩm
- **Format:** `application/json`
- **Request Body:**
```json
{
  "status": "Active", // Hoặc "Inactive"
  "reason": "Tạm dừng bán để nhập thêm hàng mới"
}
```
> **Lưu ý:** Nếu sản phẩm đang bị Admin khóa (`SUSPEND` hoặc `BAN`), người bán **không thể tự ý bật lại** (Backend sẽ trả lỗi `400 PRODUCT_LOCKED_BY_ADMIN`).

---

## 5. Phân hệ 3: Tố cáo sản phẩm vi phạm (Product Violation Reports)

### 5.1 Người dùng (Buyer/Seller) gửi tố cáo sản phẩm
- **Endpoint:** `POST /api/v1/products/{id}/reports`
- **Quyền:** `buyer` hoặc `seller` đã đăng nhập
- **Format:** `multipart/form-data`
- **Form Fields:**
  | Tên trường Form | Kiểu | Bắt buộc | Ràng buộc | Mô tả |
  | :--- | :--- | :---: | :--- | :--- |
  | `reason` | `string` | **Có** | Không rỗng | Lý do tố cáo (vd: "Hàng giả", "Hàng cấm", "Lừa đảo") |
  | `description` | `string` | **Có** | Từ 10 đến 2000 ký tự | Mô tả chi tiết hành vi vi phạm |
  | `evidenceFiles` | `File` (Binary) | **BẮT BUỘC ÍT NHẤT 1 ẢNH** | Tối đa 5 file, $\le$ 5MB/file. Định dạng: `.jpg`, `.jpeg`, `.png`, `.webp` | Bằng chứng thực tế (ảnh chụp tem giả, đoạn chat, hóa đơn) |

- **Rào chắn chống SPAM:**
  - Nếu không gửi file ảnh nào $\to$ Trả về `400 VAL_INVALID_INPUT` (`"At least one evidence image is required to submit a report."`).
  - Nếu người dùng này đã gửi 1 đơn tố cáo sản phẩm này đang ở trạng thái chờ duyệt (`Pending`) $\to$ Trả về `409 ALREADY_REPORTED` (`"You have already submitted a report for this product that is currently under review."`).

---

### 5.2 Admin/Staff xem danh sách đơn tố cáo
- **Endpoint:** `GET /api/v1/admin/reports/products`
- **Quyền:** `admin`, `staff`
- **Query Parameters:**
  - `status`: `Pending` (chờ xử lý), `Investigating` (đang điều tra), `Resolved` (đã xử lý/cấm), `Dismissed` (bác bỏ).
  - `page`, `pageSize`: Phân trang.
- **Response mẫu (200 OK):**
```json
{
  "items": [
    {
      "id": "a1e0f4fb-9061-41bb-a194-cf8499e97b53",
      "productId": "0fdd3b17-786b-46be-8382-e58c51eced8e",
      "productName": "Dan Guitar Classic Co Dien Ramirez 1A Nam 1974",
      "sellerId": "97b3ae37-9ebc-4d15-b812-35cf6971ea76",
      "reporterId": "6a4f418e-75a3-43d0-ab7e-d0759e93f3f8",
      "reason": "Counterfeit",
      "description": "Sản phẩm làm nhái tem mác chính hãng, có vết nứt lớn nhưng không báo trước.",
      "evidenceImages": [
        "https://res.cloudinary.com/u6yxkkop/image/upload/v1794461553/nexus_catalog/reports/evidence/img1.png"
      ],
      "status": "Pending",
      "createdAt": "2026-11-12T07:11:35Z"
    }
  ],
  "pagination": {
    "pageIndex": 1,
    "pageSize": 20,
    "totalCount": 1,
    "totalPages": 1
  }
}
```

---

### 5.3 Admin/Staff ra phán quyết xử lý đơn tố cáo
- **Endpoint:** `PUT /api/v1/admin/reports/products/{reportId}/action`
- **Quyền:** `admin`, `staff`
- **Format:** `application/json`
- **Request Body:**
```json
{
  "action": "BAN_PRODUCT", // BAN_PRODUCT, DISMISS, INVESTIGATE
  "resolutionNote": "Đã xác minh tem giả mạo. Cấm vĩnh viễn sản phẩm và trừ điểm uy tín người bán."
}
```
| Phán quyết (`action`) | Trạng thái Đơn | Tác động lên Sản phẩm & Người bán |
| :--- | :--- | :--- |
| **`BAN_PRODUCT`** | `Resolved` | 1. Sản phẩm chuyển sang `Banned`.<br>2. Tắt toàn bộ biến thể SKU.<br>3. Tự động đóng tất cả các đơn tố cáo khác của sản phẩm này.<br>4. Backend tự động gọi UserService trừ **10 điểm uy tín** của Shop bán hàng. |
| **`DISMISS`** | `Dismissed` | Bác bỏ tố cáo (do người mua vu khống hoặc thiếu căn cứ). Sản phẩm giữ nguyên trạng thái. |
| **`INVESTIGATE`** | `Investigating` | Đánh dấu đơn đang trong quá trình liên hệ người bán để xác minh thêm. |

---

## 6. Cảnh báo: 6 Sai lầm & Vấn đề thường gặp FE cần tránh

| STT | Sai lầm / Vấn đề phổ biến | Hậu quả | Cách làm đúng chuẩn |
| :---: | :--- | :--- | :--- |
| ❌ **1** | **Tự gọi API cộng/trừ điểm uy tín seller:** Tìm API để FE trừ điểm khi thấy sản phẩm bị cấm. | Lỗi 401/403 vì API trừ điểm là API nội bộ giữa các service (`S2S`). | **Tuyệt đối không gọi.** Backend CatalogService tự động gọi UserService khi có review hoặc khi duyệt cấm sản phẩm. |
| ❌ **2** | **Nhầm lẫn giữa Report sản phẩm và Khiếu nại đơn hàng:** Gọi API của UserService để report hàng giả. | UserService chỉ giải quyết tranh chấp giao hàng GHN/vận chuyển. | - Tố cáo sản phẩm/hàng giả $\to$ Gọi `CatalogService` (`POST /api/v1/products/{id}/reports`).<br>- Khiếu nại vận chuyển đơn hàng $\to$ Gọi `UserService` (`POST /api/v1/users/reports`). |
| ❌ **3** | **Tự set Header `Content-Type: multipart/form-data` thủ công:** Viết `headers: { 'Content-Type': 'multipart/form-data' }`. | Mất chuỗi `boundary` $\to$ Backend không thể parse file $\to$ Lỗi `400 Bad Request`. | **Không khai báo Content-Type** khi gửi `FormData`. Trình duyệt sẽ tự động thêm `multipart/form-data; boundary=...`. |
| ❌ **4** | **Đặt sai tên field upload ảnh:** Dùng `images` hoặc `evidenceImages` thay vì đúng tên controller yêu cầu. | Backend không nhận được file $\to$ Báo lỗi thiếu ảnh. | - Review: Dùng đúng tên `reviewImages`.<br>- Report: Dùng đúng tên `evidenceFiles`. |
| ❌ **5** | **Dùng sai tên field `note` trong Moderate Report:** Gửi body `{ action: "BAN_PRODUCT", note: "..." }`. | Lỗi `400 VAL_INVALID_INPUT` (`Resolution note is required.`). | Phải đặt đúng tên field là `resolutionNote`. |
| ⚠️ **6** | **Gặp lỗi 404 trống khi gọi qua Gateway (`:8080`):** Gọi `PUT /api/v1/reviews/{id}` hoặc các API `admin/*` qua Gateway 8080 bị trả về 404 rỗng. | Cấu hình YARP của Gateway chưa mapping 4 cụm route (`/reviews`, `/admin/products`, `/admin/reports`, `/admin/reviews`) sang `catalog-service`. | Tạm thời trỏ Base URL của các API này trực tiếp sang Catalog Service (`http://localhost:8002`) hoặc thông báo team DevOps/Gateway bổ sung 4 route này vào file `appsettings.json` của Gateway. |

---

## 7. Code mẫu Javascript / Typescript (Copy-Paste Ready)

### 7.1 Gửi đánh giá sản phẩm có đính kèm ảnh (React / Vue / Axios / Fetch)
```typescript
import axios from 'axios';

async function submitProductReview(productId: string, orderId: string, rating: number, comment: string, imageFiles: File[]) {
  const formData = new FormData();
  formData.append('orderId', orderId);
  formData.append('rating', rating.toString());
  if (comment) {
    formData.append('comment', comment);
  }

  // Đính kèm các file ảnh (tối đa 5 file)
  imageFiles.slice(0, 5).forEach((file) => {
    formData.append('reviewImages', file);
  });

  try {
    const response = await axios.post(`/api/v1/products/${productId}/reviews`, formData, {
      headers: {
        Authorization: `Bearer ${localStorage.getItem('token')}`,
        // KHÔNG set 'Content-Type': axios tự động nhận diện FormData
      },
    });
    console.log('Đánh giá thành công:', response.data);
    return response.data;
  } catch (error: any) {
    const errData = error.response?.data;
    alert(`Lỗi (${errData?.errorCode || 'ERROR'}): ${errData?.detail || 'Không thể gửi đánh giá'}`);
  }
}
```

---

### 7.2 Gửi tố cáo sản phẩm vi phạm (Bắt buộc ít nhất 1 ảnh)
```typescript
async function submitProductReport(productId: string, reason: string, description: string, proofFiles: File[]) {
  if (!proofFiles || proofFiles.length === 0) {
    alert('Vui lòng đính kèm ít nhất 1 hình ảnh bằng chứng!');
    return;
  }

  const formData = new FormData();
  formData.append('reason', reason);
  formData.append('description', description);

  proofFiles.slice(0, 5).forEach((file) => {
    formData.append('evidenceFiles', file); // Đúng tên field: evidenceFiles
  });

  const res = await fetch(`/api/v1/products/${productId}/reports`, {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${localStorage.getItem('token')}`,
    },
    body: formData,
  });

  const data = await res.json();
  if (!res.ok) {
    throw new Error(data.detail || 'Gửi tố cáo thất bại');
  }
  return data;
}
```

---

### 7.3 Admin xử lý phán quyết đơn tố cáo (Cấm sản phẩm)
```typescript
async function resolveProductReport(reportId: string, action: 'BAN_PRODUCT' | 'DISMISS' | 'INVESTIGATE', resolutionNote: string) {
  const response = await fetch(`/api/v1/admin/reports/products/${reportId}/action`, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${localStorage.getItem('token')}`,
    },
    body: JSON.stringify({
      action: action,
      resolutionNote: resolutionNote, // Đúng tên field: resolutionNote
    }),
  });

  const result = await response.json();
  if (!response.ok) {
    throw new Error(result.detail || 'Xử lý đơn thất bại');
  }
  return result;
}
```

---

### 7.4 Admin duyệt hoặc tạm khóa sản phẩm
```typescript
async function moderateProduct(productId: string, action: 'APPROVE' | 'SUSPEND' | 'REJECT' | 'UNLOCK' | 'BAN', reason: string) {
  const response = await fetch(`/api/v1/admin/products/${productId}/moderate`, {
    method: 'PATCH',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${localStorage.getItem('token')}`,
    },
    body: JSON.stringify({
      action: action,
      reason: reason,
    }),
  });

  const result = await response.json();
  if (!response.ok) {
    throw new Error(result.detail || 'Thao tác kiểm duyệt thất bại');
  }
  return result;
}
```

---

## 8. Hệ thống Thông báo In-App & Rung chuông Realtime

### 8.1 Cơ chế hoạt động & Trải nghiệm người dùng
- Khi phát sinh các sự kiện nghiệp vụ trong `CatalogService` (duyệt sản phẩm, đánh giá mới, tố cáo vi phạm...), Backend tự động dispatch thông báo sang `NotificationService` (Port 8007).
- `NotificationService` sẽ:
  1. Lưu trữ bản ghi vào Hộp thư thông báo người dùng (`In-App Inbox`).
  2. Phát tín hiệu realtime qua kênh WebSocket / SignalR về ứng dụng Web / Mobile của người nhận.
  3. Biểu tượng **Quả chuông (Bell Icon)** trên thanh điều hướng Header sẽ tự động tăng số đếm chưa đọc (Badge counter) và rung hiệu ứng.
- **Chính sách:** Toàn bộ thông báo liên quan đến Catalog vận hành qua hình thức **100% In-App Notifications** (không gửi qua Email).

### 8.2 Danh mục các loại thông báo từ Catalog & Hướng xử lý trên FE

| NotificationType | Đối tượng nhận | Khi nào kích hoạt? | Dữ liệu `payload` (JSON) | Điều hướng khuyến nghị khi click (Router) |
| :--- | :---: | :--- | :--- | :--- |
| `PRODUCT_APPROVED` | **Seller** | Admin duyệt đưa sản phẩm lên sàn | `{"productId": "...", "status": "Active"}` | `/seller/products/{productId}` |
| `PRODUCT_REJECTED` | **Seller** | Admin từ chối duyệt sản phẩm | `{"productId": "...", "reason": "...", "status": "Draft"}` | `/seller/products/{productId}/edit` (để sửa lý do) |
| `PRODUCT_SUSPENDED` | **Seller** | Admin tạm khóa dừng hiển thị sản phẩm | `{"productId": "...", "reason": "...", "status": "Inactive"}` | `/seller/products/{productId}` |
| `PRODUCT_BANNED` | **Seller** | Admin cấm vĩnh viễn sản phẩm & trừ 10 điểm uy tín | `{"productId": "...", "penaltyPoints": 10, "status": "Banned"}` | `/seller/products/{productId}` & `/seller/reputation` |
| `REPORT_SUBMITTED` | **Buyer** | Người mua gửi đơn tố cáo sản phẩm thành công | `{"reportId": "...", "productId": "..."}` | `/buyer/reports/{reportId}` |
| `PRODUCT_BANNED_REPORT` | **Seller** | Sản phẩm bị cấm do đơn tố cáo vi phạm được xác thực | `{"productId": "...", "reportId": "...", "penaltyPoints": 10}` | `/seller/products/{productId}` & `/seller/reputation` |
| `REPORT_RESOLVED` | **Buyer** | Đơn tố cáo của Buyer được Admin phán quyết cấm sản phẩm | `{"reportId": "...", "productId": "...", "resolution": "Resolved"}` | `/buyer/reports/{reportId}` |
| `REPORT_DISMISSED` | **Buyer** | Đơn tố cáo của Buyer bị Admin bác bỏ | `{"reportId": "...", "productId": "...", "resolution": "Dismissed"}` | `/buyer/reports/{reportId}` |
| `NEW_PRODUCT_REVIEW` | **Seller** | Khách hàng đánh giá sản phẩm (từ 1★ đến 5★) | `{"productId": "...", "reviewId": "...", "rating": 5, "orderId": "..."}` | `/seller/products/{productId}#reviews` |
| `REVIEW_HIDDEN` | **Buyer** | Admin ẩn bài đánh giá của Buyer do vi phạm tiêu chuẩn | `{"reviewId": "...", "productId": "...", "reason": "..."}` | `/buyer/reviews` |

### 8.3 Gợi ý xử lý hiển thị Toast/Popup khi nhận sự kiện Realtime
```typescript
// Ví dụ lắng nghe sự kiện từ Notification WebSocket / SignalR:
notificationHub.on("ReceiveInAppNotification", (notification) => {
  // 1. Cập nhật số lượng thông báo chưa đọc trên quả chuông
  unreadCount.value += 1;

  // 2. Kiểm tra type để hiển thị Toast tương ứng
  if (notification.type === "NEW_PRODUCT_REVIEW") {
    const payload = JSON.parse(notification.payload || "{}");
    if (payload.rating <= 2) {
      toast.warning(`[Cảnh báo Đánh giá] ${notification.title}`, {
        description: notification.body,
        action: {
          label: "Xem ngay",
          onClick: () => router.push(`/seller/products/${payload.productId}#reviews`)
        }
      });
      return;
    }
  }

  // Toast thông thường
  toast.info(notification.title, {
    description: notification.body
  });
});
```

