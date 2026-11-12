# CATALOG SERVICE - PUT /api/v1/reviews/{id}

> **Feature / Use Case:** Khách hàng (Buyer) chỉnh sửa nội dung hoặc điểm số đánh giá của chính mình trong thời hạn 30 ngày [CAT-63]  
> **Trạng thái:** ✅ VERIFIED  
> **Ngày cập nhật:** 2026-11-12  
> **Tác giả:** Nexus Team & AI Agent  

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `PUT`
- **Route Endpoint:** `/api/v1/reviews/{id}`
- **Content-Type:** `multipart/form-data` (Hỗ trợ cập nhật điểm số, nhận xét và upload lại ảnh đánh giá)
- **Quyền hạn (RBAC Policy):** `[RequireRole("buyer", "admin")]` (Bắt buộc người dùng đã đăng nhập)
- **Header bắt buộc:**
  - `Authorization: Bearer <JWT_Token>`
  - `X-User-Id: <Guid>` (Hoặc trích xuất từ Token claim `sub`/`userId`)
- **Controller:** `Catalog.Api/Controllers/Public/ReviewsController.cs`
- **Application Contract:** `Catalog.Application/Services/Review/IProductReviewService.cs` (`UpdateReviewAsync`)

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)

### 2.1 Route Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `id` | Route | `Guid` | Có | UUIDv4 format | Khóa chính của bài đánh giá cần chỉnh sửa |

### 2.2 Form Data Parameters (`multipart/form-data`)
| Trường Form | Kiểu | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :---: | :--- | :--- |
| `rating` | `short` | Có | Từ 1 đến 5 sao | Điểm số đánh giá cập nhật |
| `comment` | `string` | Không | Tối đa 2000 ký tự | Nội dung nhận xét cập nhật |
| `reviewImages` | `List<IFormFile>` | Không (Tùy chọn) | Tối đa 5 file; $\le$ 5MB/file; định dạng `.jpg`, `.jpeg`, `.png`, `.webp` | Các file ảnh chụp mới của sản phẩm nếu muốn thay thế ảnh cũ |

- **Ví dụ gọi bằng cURL (cập nhật kèm ảnh mới):**
```bash
curl -X PUT "http://localhost:8002/api/v1/reviews/488c5c33-0809-43ed-8418-8c9ebe1f66ab" \
  -H "x-user-id: dddddddd-4444-4444-4444-444444444444" \
  -H "x-user-roles: buyer" \
  -F "rating=5" \
  -F "comment=Cập nhật sau 1 tuần dùng: rất hài lòng!" \
  -F "reviewImages=@/path/to/new_photo.png"
```

---

## 3. Quy tắc Nghiệp vụ & Rào chắn (Business Rules & Recalculation)

1. **Kiểm tra Quyền Sở hữu (Ownership Guardrail):**
   - Tìm kiếm bài đánh giá theo `Id == id` và `!IsDeleted`. Nếu không tìm thấy $\to$ Trả về `404 REVIEW_NOT_FOUND`.
   - Nếu `review.BuyerId != currentUserId` và người gọi không phải `Admin` $\to$ Trả về `403 FORBIDDEN` ("You can only edit your own reviews.").
2. **Kiểm tra Thời hạn Chỉnh sửa (30-Day Window Limit):**
   - Khách hàng chỉ được phép chỉnh sửa bài đánh giá trong vòng 30 ngày kể từ ngày tạo bài (`review.CreatedAt`):
     ```csharp
     if ((now - review.CreatedAt) > TimeSpan.FromDays(30))
     {
         throw new BusinessRuleValidationException(
             "REVIEW_UPDATE_EXPIRED",
             "You can only edit your review within 30 days of posting.");
     }
     ```
   - Nếu quá 30 ngày $\to$ Trả về `400 Bad Request` với mã `REVIEW_UPDATE_EXPIRED`.
3. **Kiểm tra Trạng thái Bài đánh giá:**
   - Nếu bài đánh giá đã bị Admin ẩn (`Status == ReviewStatus.Hidden`) $\to$ Trả về `400 REVIEW_HIDDEN` ("Cannot edit a review that has been hidden by moderators.").
4. **Cập nhật Bài đánh giá, Upload Ảnh & Tính lại Xếp hạng:**
   - Ghi nhận `oldRating = review.Rating`.
   - Nếu có truyền `reviewImages`:
     - Kiểm tra tối đa 5 file, mỗi file $\le$ 5MB, hỗ trợ `.jpg`, `.jpeg`, `.png`, `.webp`.
     - Tải các ảnh mới lên Cloudinary folder `"reviews"`.
     - Cập nhật chuỗi JSON vào cột `ReviewImages`.
   - Cập nhật thông tin mới:
     - `review.Rating = request.Rating`
     - `review.Comment = request.Comment?.Trim()`
     - `review.UpdatedAt = DateTimeOffset.UtcNow`
   - Nếu `oldRating != request.Rating`:
     - Tìm bản ghi `ProductRatingStats` của sản phẩm:
       ```csharp
       stats.StarSum = stats.StarSum - oldRating + request.Rating;
       stats.AvgRating = Math.Round((decimal)stats.StarSum / stats.ReviewCount, 2);
       ```
   - Lưu thay đổi vào CSDL trong cùng transaction.
5. **Tích hợp liên dịch vụ (S2S UserService - Cập nhật lại Uy Tín khi Đổi Sao):**
   - Nếu `oldRating != request.Rating`, CatalogService tự động gửi yêu cầu cập nhật lại điểm uy tín người bán sang UserService:
     - **Endpoint gọi:** `POST api/v1/internal/users/reputation-adjust` (Port 8001)
     - **Payload:** `{ "userId": "<sellerId>", "targetRole": "Seller", "ratingStar": <newRating>, "orderId": "<orderId>", "referenceId": "<orderId>" }`
     - **Cơ chế chịu lỗi:** Non-blocking resilient execution (bọc try-catch và log warning). Không làm gián đoạn response 200 OK của người dùng.

---

## 4. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)

- **Mã HTTP thành công:** `200 OK`
- **JSON Response mẫu:**
```json
{
  "id": "488c5c33-0809-43ed-8418-8c9ebe1f66ab",
  "productId": "f3f47581-6036-4fa1-b9fa-803770308c71",
  "buyerId": "dddddddd-4444-4444-4444-444444444444",
  "rating": 5,
  "comment": "Cập nhật sau 1 tuần dùng: rất hài lòng!",
  "reviewImages": [
    "https://res.cloudinary.com/u6yxkkop/image/upload/v1794461554/nexus_catalog/reviews/gqsecf5aek6x8zv01xbj.png"
  ],
  "status": "Published",
  "createdAt": "2026-11-12T05:32:31Z",
  "updatedAt": "2026-11-12T05:32:34Z"
}
```

---

## 5. Ma trận Lỗi (RFC 9110 Error Matrix)

| HTTP Status | Error Code | Điều kiện kích hoạt | Error Message chuẩn |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `rating` không thuộc [1, 5] hoặc `comment` > 2000 ký tự | `"Rating must be between 1 and 5 stars."` |
| `400 Bad Request` | `VAL_INVALID_INPUT` | Quá 5 ảnh hoặc file không đúng định dạng (.jpg, .jpeg, .png, .webp) | `"Maximum 5 review images are allowed."` / `"File '{fileName}' has unsupported extension '{ext}'. Only .jpg, .jpeg, .png, .webp are allowed."` |
| `400 Bad Request` | `VAL_INVALID_INPUT` | File ảnh vượt quá 5MB | `"File '{fileName}' exceeds 5 MB limit."` |
| `400 Bad Request` | `REVIEW_UPDATE_EXPIRED` | Đã quá 30 ngày kể từ thời điểm gửi bài đánh giá | `"Reviews can only be updated within 30 days of submission."` |
| `400 Bad Request` | `REVIEW_HIDDEN` | Bài đánh giá đang bị khóa/ẩn bởi ban quản trị | `"Cannot edit a review that has been hidden by moderators."` |
| `401 Unauthorized` | `UNAUTHORIZED` | Không có JWT Token hoặc Token hết hạn | `"Authentication is required to perform this action."` |
| `403 Forbidden` | `FORBIDDEN` | Người dùng cố tình sửa bài đánh giá của người khác | `"You can only edit your own reviews."` |
| `404 Not Found` | `REVIEW_NOT_FOUND` | Không tìm thấy bài đánh giá với ID tương ứng | `"Product review not found."` |
