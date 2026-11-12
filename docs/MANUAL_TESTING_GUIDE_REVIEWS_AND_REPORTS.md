# KỊCH BẢN KIỂM THỬ THỦ CÔNG (MANUAL TESTING SCENARIOS)
## PHÂN HỆ ĐÁNH GIÁ (REVIEWS) & TỐ CÁO SẢN PHẨM (REPORTS)

> **Dịch vụ:** CatalogService  
> **Cổng truy cập:**  
> - Chạy trực tiếp (`dotnet run`): `http://localhost:5272/swagger`  
> - Chạy qua Docker (`docker compose`): `http://localhost:8002/swagger`  
> **Ngày cập nhật:** 2026-11-12

---

## 🔑 1. THIẾT LẬP DANH TÍNH TRÊN SWAGGER (AUTHENTICATION HEADERS)

Để gọi các API yêu cầu quyền, bạn bấm nút **Authorize (ổ khóa xanh)** ở góc trên bên phải trang Swagger và điền các Header sau:

| Đối tượng | Header `x-user-id` | Header `x-user-roles` | Ghi chú |
| :--- | :--- | :--- | :--- |
| **Buyer (Người mua)** | `dddddddd-4444-4444-4444-444444444444` | `buyer` | Dùng để viết review, gửi tố cáo vi phạm. |
| **Seller (Người bán)** | `cccccccc-3333-3333-3333-333333333333` | `seller` | Chủ sở hữu sản phẩm seed. |
| **Admin / Staff (Quản trị viên)** | `aaaaaaaa-1111-1111-1111-111111111111` | `admin,staff` | Dùng để xử lý đơn tố cáo, cấm sản phẩm, ẩn review. |

---

## ⭐ PHẦN A: KỊCH BẢN TEST ĐÁNH GIÁ SẢN PHẨM (REVIEWS & RATINGS)

**Sản phẩm mục tiêu:** `Smartphone Nexus X Pro 256GB`  
- `productId`: `11000000-0000-0000-0000-000000000001` (Sản phẩm mới chưa có đánh giá nào).

---

### Bước A1: Xem tổng quan Rating ban đầu (Công khai)
- **API:** `GET /api/v1/products/11000000-0000-0000-0000-000000000001/rating-summary`
- **Quyền:** Không cần Header (Public).
- **Kết quả kỳ vọng (`200 OK`):**
  ```json
  {
    "productId": "11000000-0000-0000-0000-000000000001",
    "avgRating": null,
    "reviewCount": 0,
    "starSum": 0,
    "distribution": {
      "star5": 0,
      "star4": 0,
      "star3": 0,
      "star2": 0,
      "star1": 0
    }
  }
  ```

---

### Bước A2: Thử đánh giá khi CHƯA mua hàng (Kiểm tra chống gian lận)
- **API:** `POST /api/v1/products/11000000-0000-0000-0000-000000000001/reviews`
- **Header:** `x-user-id: dddddddd-4444-4444-4444-444444444444`, `x-user-roles: buyer`
- **Request Body:**
  ```json
  {
    "orderId": "99000000-0000-0000-0000-000000000099",
    "rating": 5,
    "comment": "Sản phẩm tuyệt vời nhưng tôi chưa mua!"
  }
  ```
- **Kết quả kỳ vọng (`400 Bad Request`):**
  ```json
  {
    "error": {
      "code": "NOT_PURCHASED",
      "message": "You can only review products that you have purchased and completed."
    }
  }
  ```
  *(Hệ thống từ chối vì chưa tìm thấy bản ghi `PurchaseRecord` của Buyer với đơn hàng này).*

---

### Bước A3: Kích hoạt quyền đánh giá (Mô phỏng CommerceService đồng bộ đơn mua)
- **API:** `POST /api/v1/internal/purchases`
- **Quyền:** Internal API
- **Request Body:**
  ```json
  {
    "buyerId": "dddddddd-4444-4444-4444-444444444444",
    "orderId": "99000000-0000-0000-0000-000000000099",
    "productId": "11000000-0000-0000-0000-000000000001"
  }
  ```
- **Kết quả kỳ vọng (`201 Created`):**
  ```json
  {
    "id": "...",
    "buyerId": "dddddddd-4444-4444-4444-444444444444",
    "orderId": "99000000-0000-0000-0000-000000000099",
    "productId": "11000000-0000-0000-0000-000000000001",
    "completedAt": "..."
  }
  ```

---

### Bước A4: Buyer gửi Đánh giá 5 sao hợp lệ
- **API:** `POST /api/v1/products/11000000-0000-0000-0000-000000000001/reviews`
- **Header:** `x-user-id: dddddddd-4444-4444-4444-444444444444`, `x-user-roles: buyer`
- **Request Body:**
  ```json
  {
    "orderId": "99000000-0000-0000-0000-000000000099",
    "rating": 5,
    "comment": "Máy dùng rất mượt, màn hình OLED 120Hz siêu đẹp, pin trâu!"
  }
  ```
- **Kết quả kỳ vọng (`201 Created`):**
  Trả về thông tin review kèm mã `id` (lưu lại mã `reviewId` này để test Bước A7 & A8).
  ```json
  {
    "id": "<reviewId>",
    "productId": "11000000-0000-0000-0000-000000000001",
    "buyerId": "dddddddd-4444-4444-4444-444444444444",
    "rating": 5,
    "comment": "Máy dùng rất mượt, màn hình OLED 120Hz siêu đẹp, pin trâu!",
    "status": "Published"
  }
  ```

---

### Bước A5: Thử gửi lại đánh giá trên cùng đơn hàng (Kiểm tra chống spam)
- **API:** `POST /api/v1/products/11000000-0000-0000-0000-000000000001/reviews` (với cùng `orderId` trên).
- **Kết quả kỳ vọng (`409 Conflict`):**
  ```json
  {
    "error": {
      "code": "ALREADY_REVIEWED",
      "message": "You have already reviewed this product for this order."
    }
  }
  ```

---

### Bước A6: Kiểm tra điểm Rating được tự động cộng dồn
- **API:** `GET /api/v1/products/11000000-0000-0000-0000-000000000001/rating-summary`
- **Kết quả kỳ vọng (`200 OK`):**
  ```json
  {
    "avgRating": 5.0,
    "reviewCount": 1,
    "starSum": 5,
    "distribution": {
      "star5": 1,
      "star4": 0,
      "star3": 0,
      "star2": 0,
      "star1": 0
    }
  }
  ```

---

### Bước A7: Buyer chỉnh sửa đánh giá (Từ 5 sao sang 4 sao)
- **API:** `PUT /api/v1/reviews/{reviewId}` (thay `{reviewId}` lấy từ Bước A4).
- **Header:** `x-user-id: dddddddd-4444-4444-4444-444444444444`, `x-user-roles: buyer`
- **Request Body:**
  ```json
  {
    "rating": 4,
    "comment": "Dùng 1 tuần thấy máy hơi ấm khi chơi game nặng, giảm 1 sao còn 4 sao."
  }
  ```
- **Kết quả kỳ vọng (`200 OK`):** Review cập nhật rating = 4.
- **Kiểm tra lại Rating:** Gọi lại `GET rating-summary`:
  ➔ `avgRating`: **4.0**, `starSum`: **4**, `star5`: **0**, `star4`: **1**.

---

### Bước A8: Admin ẩn đánh giá do có nội dung vi phạm
- **API:** `PATCH /api/v1/admin/reviews/{reviewId}/moderate`
- **Header:** `x-user-id: aaaaaaaa-1111-1111-1111-111111111111`, `x-user-roles: admin,staff`
- **Request Body:**
  ```json
  {
    "action": "HIDE"
  }
  ```
- **Kết quả kỳ vọng (`200 OK`):** `status: "Hidden"`.
- **Kiểm chứng hiệu ứng:**
  1. `GET /api/v1/products/11000000-0000-0000-0000-000000000001/reviews`: Danh sách trả về rỗng (review đã biến mất khỏi mắt người dùng công khai).
  2. `GET /api/v1/products/11000000-0000-0000-0000-000000000001/rating-summary`: `reviewCount: 0`, `avgRating: null`, `starSum: 0` (review ẩn không được tính điểm sao).

---

### Bước A9: Admin khôi phục lại đánh giá
- **API:** `PATCH /api/v1/admin/reviews/{reviewId}/moderate`
- **Request Body:**
  ```json
  {
    "action": "PUBLISH"
  }
  ```
- **Kết quả kỳ vọng (`200 OK`):** `status: "Published"`. Rating tự động khôi phục lại 4.0 sao!

---

### Bước A10: Buyer tự xóa đánh giá của mình
- **API:** `DELETE /api/v1/reviews/{reviewId}`
- **Header:** `x-user-id: dddddddd-4444-4444-4444-444444444444`, `x-user-roles: buyer`
- **Kết quả kỳ vọng (`204 No Content`):** Review bị xóa mềm, điểm sao của sản phẩm trừ hoàn toàn về 0.

---

## 🛡️ PHẦN B: KỊCH BẢN TEST TỐ CÁO & CẤM SẢN PHẨM (REPORTS & BANNING)

**Sản phẩm mục tiêu:** `Áo Khoác Bomber Unisex Nexus`  
- `productId`: `22000000-0000-0000-0000-000000000002` (Sản phẩm đang Active).

---

### Bước B1: Người dùng gửi đơn tố cáo sản phẩm vi phạm
- **API:** `POST /api/v1/products/22000000-0000-0000-0000-000000000002/reports`
- **Header:** `x-user-id: dddddddd-4444-4444-4444-444444444444`, `x-user-roles: buyer`
- **Request Body:**
  ```json
  {
    "reason": "Counterfeit",
    "description": "Sản phẩm áo khoác nhái logo thương hiệu khác, chất lượng vải kém không đúng với ảnh cam kết.",
    "evidenceImages": [
      "https://res.cloudinary.com/demo/image/upload/sample_fake_tag.jpg"
    ]
  }
  ```
- **Kết quả kỳ vọng (`201 Created`):**
  Trả về `ProductReportDto` kèm `reportId` (lưu lại `reportId` để test các bước tiếp theo). Trạng thái đơn: `Pending`.

---

### Bước B2: Thử gửi tiếp đơn thứ 2 khi đơn cũ đang chờ (Kiểm tra chống spam)
- **API:** `POST /api/v1/products/22000000-0000-0000-0000-000000000002/reports` (gửi lại cùng body từ cùng user).
- **Kết quả kỳ vọng (`409 Conflict`):**
  ```json
  {
    "error": {
      "code": "ALREADY_REPORTED",
      "message": "You have already submitted a pending report for this product. Please wait for moderation."
    }
  }
  ```

---

### Bước B3: Staff/Admin kiểm tra hàng đợi duyệt đơn tố cáo
- **API:** `GET /api/v1/admin/reports/products?status=Pending&page=1&pageSize=10`
- **Header:** `x-user-id: aaaaaaaa-1111-1111-1111-111111111111`, `x-user-roles: admin,staff`
- **Kết quả kỳ vọng (`200 OK`):** Danh sách chứa đơn tố cáo vừa tạo, hiển thị đầy đủ lý do `Counterfeit` và link ảnh bằng chứng.

---

### Bước B4: Staff chuyển đơn sang trạng thái Điều tra (INVESTIGATE)
- **API:** `PUT /api/v1/admin/reports/products/{reportId}/action` (thay `{reportId}` từ Bước B1).
- **Header:** `x-user-id: aaaaaaaa-1111-1111-1111-111111111111`, `x-user-roles: admin,staff`
- **Request Body:**
  ```json
  {
    "action": "INVESTIGATE",
    "resolutionNote": "Đã tiếp nhận bằng chứng hình ảnh, chuyển bộ phận pháp chế đối soát với nhà bán hàng."
  }
  ```
- **Kết quả kỳ vọng (`200 OK`):** Đơn chuyển sang `status: "Investigating"`.

---

### Bước B5: Staff ra phán quyết Cấm vĩnh viễn sản phẩm (BAN_PRODUCT)
- **API:** `PUT /api/v1/admin/reports/products/{reportId}/action`
- **Header:** `x-user-id: aaaaaaaa-1111-1111-1111-111111111111`, `x-user-roles: admin,staff`
- **Request Body:**
  ```json
  {
    "action": "BAN_PRODUCT",
    "resolutionNote": "Xác nhận hàng giả vi phạm bản quyền thương hiệu nghiêm trọng. Cấm vĩnh viễn khỏi sàn và khóa toàn bộ biến thể."
  }
  ```
- **Kết quả kỳ vọng (`200 OK`):** Đơn chuyển sang `status: "Resolved"`.

---

### Bước B6: Kiểm chứng hiệu ứng dây chuyền & Rào chắn bảo vệ khi sản phẩm bị Ban
1. **Kiểm tra trạng thái sản phẩm:**
   - `GET /api/v1/products/22000000-0000-0000-0000-000000000002`
   - Kỳ vọng: `status` đổi thành **`Banned`**.
2. **Seller thử chỉnh sửa sản phẩm đã bị Ban:**
   - `PUT /api/v1/products/22000000-0000-0000-0000-000000000002`
   - Header: `x-user-id: cccccccc-3333-3333-3333-333333333333`, `x-user-roles: seller`
   - Body: `{"name": "Áo Khoác Bomber Sửa Tên", "categoryId": "20000000-0000-0000-0000-000000000002"}`
   - Kỳ vọng: **`400 Bad Request`** (`PRODUCT_BANNED: Banned product cannot be modified.`).
3. **Seller thử tự kích hoạt lại sản phẩm:**
   - `PATCH /api/v1/products/22000000-0000-0000-0000-000000000002/status`
   - Body: `{"status": "Active"}`
   - Kỳ vọng: **`400 Bad Request`** (`PRODUCT_BANNED: Banned product status cannot be changed.`).
4. **Seller thử xóa sản phẩm:**
   - `DELETE /api/v1/products/22000000-0000-0000-0000-000000000002`
   - Kỳ vọng: **`400 Bad Request`** (`PRODUCT_BANNED: Banned product cannot be deleted.`).
5. **Admin thử mở khóa lại bằng APPROVE thường:**
   - `PATCH /api/v1/admin/products/22000000-0000-0000-0000-000000000002/moderate`
   - Body: `{"action": "APPROVE"}`
   - Kỳ vọng: **`400 Bad Request`** (`PRODUCT_ALREADY_BANNED: Product has been permanently banned and cannot be approved or unlocked.`).

---

### Bước B7: Thử nghiệm nhánh Bác bỏ tố cáo sai sự thật (DISMISS)
1. Gửi tố cáo trên sản phẩm 3:
   - `POST /api/v1/products/33000000-0000-0000-0000-000000000003/reports`
   - Body: `{"reason": "Other", "description": "Tố cáo thử nghiệm sai sự thật để kiểm tra luồng Dismiss."}`
   - Nhận được `fakeReportId`.
2. Staff bác bỏ đơn:
   - `PUT /api/v1/admin/reports/products/{fakeReportId}/action`
   - Body: `{"action": "DISMISS", "resolutionNote": "Bằng chứng không hợp lệ, sản phẩm tuân thủ đúng quy định sàn."}`
   - Kỳ vọng: `200 OK`, `status: "Dismissed"`. Sản phẩm 3 vẫn `Active` bình thường 100%!
