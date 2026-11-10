# CATALOG SERVICE - PUT /api/v1/reviews/{id}

> **Feature / Use Case:** Khách hàng (Buyer) chỉnh sửa nội dung hoặc điểm số đánh giá của chính mình [CAT-63]  
> **Trạng thái:** ⏳ DRAFT (Pre-Code Specification)  
> **Ngày tạo:** 2026-11-10  
> **Tác giả:** Nexus Team & AI Agent  

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `PUT`
- **Route Endpoint:** `/api/v1/reviews/{id}`
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

### 2.2 Request Body Record DTO (`UpdateProductReviewRequest`)
- **Vị trí file:** `Catalog.Application/Services/Review/ReviewRequests.cs`
- **Khai báo C# Record:**
```csharp
public sealed record UpdateProductReviewRequest(
    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5 stars.")]
    short Rating,

    [MaxLength(2000, ErrorMessage = "Comment cannot exceed 2000 characters.")]
    string? Comment = null
);
```

- **JSON Request Body mẫu:**
```json
{
  "rating": 4,
  "comment": "Cập nhật sau 2 tuần dùng: pin hơi hao hơn dự kiến nhưng các tính năng khác đều ổn."
}
```

---

## 3. Quy tắc Nghiệp vụ & Rào chắn (Business Rules & Recalculation)

1. **Kiểm tra Quyền Sở hữu (Ownership Guardrail):**
   - Tìm kiếm bài đánh giá theo `Id == id` và `!IsDeleted`. Nếu không tìm thấy $\to$ Trả về `404 REVIEW_NOT_FOUND`.
   - Nếu `review.BuyerId != currentUserId` và người gọi không phải `Admin` $\to$ Trả về `403 FORBIDDEN` ("You can only edit your own reviews.").
2. **Kiểm tra Trạng thái Bài đánh giá:**
   - Nếu bài đánh giá đã bị Admin ẩn (`Status == ReviewStatus.Hidden`) $\to$ Trả về `400 REVIEW_HIDDEN` ("Cannot edit a review that has been hidden by moderators.").
3. **Cập nhật Bài đánh giá & Tính lại Xếp hạng:**
   - Ghi nhận `oldRating = review.Rating`.
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

---

## 4. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)

- **Mã HTTP thành công:** `200 OK`
- **JSON Response mẫu:**
```json
{
  "id": "e4f8a123-5678-490a-bcde-123456789abc",
  "productId": "33000000-0000-0000-0000-000000000003",
  "buyerId": "dddddddd-4444-4444-4444-444444444444",
  "rating": 4,
  "comment": "Cập nhật sau 2 tuần dùng: pin hơi hao hơn dự kiến nhưng các tính năng khác đều ổn.",
  "status": "Published",
  "createdAt": "2026-11-10T06:45:00Z",
  "updatedAt": "2026-11-10T06:50:00Z"
}
```

---

## 5. Ma trận Lỗi (RFC 9110 Error Matrix)

| HTTP Status | Error Code | Điều kiện kích hoạt | Error Message chuẩn |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `rating` không thuộc [1, 5] hoặc `comment` > 2000 ký tự | `"Rating must be between 1 and 5 stars."` |
| `400 Bad Request` | `REVIEW_HIDDEN` | Bài đánh giá đang bị khóa/ẩn bởi ban quản trị | `"Cannot edit a review that has been hidden by moderators."` |
| `401 Unauthorized` | `UNAUTHORIZED` | Không có JWT Token hoặc Token hết hạn | `"Authentication is required to perform this action."` |
| `403 Forbidden` | `FORBIDDEN` | Người dùng cố tình sửa bài đánh giá của người khác | `"You can only edit your own reviews."` |
| `404 Not Found` | `REVIEW_NOT_FOUND` | Không tìm thấy bài đánh giá với ID tương ứng | `"Product review not found."` |

