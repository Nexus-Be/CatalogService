# CATALOG SERVICE - PATCH /api/v1/admin/reviews/{id}/moderate

> **Feature / Use Case:** Quản trị viên (Admin) kiểm duyệt bài đánh giá (Ẩn đánh giá vi phạm tiêu chuẩn hoặc Khôi phục hiển thị) [CAT-65]  
> **Trạng thái:** ✅ VERIFIED  
> **Ngày cập nhật:** 2026-11-12  
> **Tác giả:** Nexus Team & AI Agent  

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `PATCH`
- **Route Endpoint:** `/api/v1/admin/reviews/{id}/moderate`
- **Quyền hạn (RBAC Policy):** `[RequireRole("admin")]` (Bắt buộc vai trò Quản trị viên)
- **Header bắt buộc:**
  - `Authorization: Bearer <JWT_Token>`
  - `X-User-Id: <Guid>` (ID của Admin thực hiện kiểm duyệt)
  - `X-User-Roles: admin`
- **Controller:** `Catalog.Api/Controllers/Public/AdminReviewsController.cs`
- **Application Contract:** `Catalog.Application/Services/Review/IProductReviewService.cs` (`ModerateReviewAsync`)

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)

### 2.1 Route Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `id` | Route | `Guid` | Có | UUIDv4 format | Khóa chính của bài đánh giá cần kiểm duyệt |

### 2.2 Request Body Record DTO (`ModerateReviewRequest`)
- **Vị trí file:** `Catalog.Application/Services/Review/ReviewRequests.cs`
- **Khai báo C# Record:**
```csharp
public sealed record ModerateReviewRequest(
    [Required(ErrorMessage = "Action is required.")]
    string Action, // "HIDE" hoặc "PUBLISH"

    [MaxLength(500, ErrorMessage = "Moderation reason cannot exceed 500 characters.")]
    string? Reason = null
);
```

- **JSON Request Body mẫu:**
```json
{
  "action": "HIDE",
  "reason": "Bài đánh giá chứa ngôn từ thô tục, xúc phạm và quảng cáo website ngoài."
}
```

---

## 3. Quy tắc Nghiệp vụ & Rào chắn (Business Rules & Recalculation)

1. **Kiểm tra Bài đánh giá:**
   - Tìm bài đánh giá theo `Id == id` và `!IsDeleted`. Nếu không tìm thấy $\to$ Trả về `404 REVIEW_NOT_FOUND`.
2. **Xử lý Hành động:**
   - **Trường hợp `action.ToUpper() == "HIDE"`:**
     - Nếu review đang `Published`: Chuyển sang `Status = ReviewStatus.Hidden`.
     - Giảm trừ điểm khỏi `ProductRatingStats`:
       ```csharp
       stats.StarSum -= review.Rating;
       stats.ReviewCount -= 1;
       stats.AvgRating = stats.ReviewCount > 0 
           ? Math.Round((decimal)stats.StarSum / stats.ReviewCount, 2) 
           : null;
       ```
   - **Trường hợp `action.ToUpper() == "PUBLISH"` (Khôi phục hiển thị):**
     - Nếu review đang `Hidden`: Chuyển sang `Status = ReviewStatus.Published`.
     - Cộng lại điểm vào `ProductRatingStats`:
       ```csharp
       stats.StarSum += review.Rating;
       stats.ReviewCount += 1;
       stats.AvgRating = Math.Round((decimal)stats.StarSum / stats.ReviewCount, 2);
       ```
   - Nếu `action` không hợp lệ $\to$ Trả về `400 VAL_INVALID_ACTION` ("Action must be either 'HIDE' or 'PUBLISH'.").
3. **Ghi nhận Nhật ký Kiểm duyệt (Audit Logging):**
   - Hệ thống tự động ghi bản ghi `AuditLog`:
     - `UserId = adminUserId`
     - `Action = AuditAction.ReviewModerate` (enum giá trị = 6)
     - `EntityType = "ProductReview"`
     - `EntityId = id`
     - `Details = "Review status changed to {Status}. Reason: {Reason}"`
4. **Phát thông báo In-App (Notification Integration):**
   - Khi Admin thực hiện hành động `HIDE`: Hệ thống tự động gửi thông báo In-App tới tác giả đánh giá (`review.BuyerId`).
     - **Người nhận:** `review.BuyerId` (Tác giả bài review)
     - **NotificationType:** `REVIEW_HIDDEN`
     - **Title:** `"Bài đánh giá của bạn đã bị ẩn"`
     - **Body:** `"Bài đánh giá của bạn cho sản phẩm '{product.Name}' đã bị ẩn bởi Quản trị viên do vi phạm quy chuẩn cộng đồng. Ghi chú: {request.Reason}."`
     - **Payload:** `{"reviewId": "...", "productId": "...", "reason": "..."}`
   - Cơ chế phát thông báo là non-blocking: sự cố kết nối tới `NotificationService` không ảnh hưởng đến giao dịch kiểm duyệt.

---

## 4. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)

- **Mã HTTP thành công:** `200 OK`
- **JSON Response mẫu:**
```json
{
  "id": "33500000-0000-0000-0000-000000000001",
  "productId": "33000000-0000-0000-0000-000000000003",
  "buyerId": "dddddddd-4444-4444-4444-444444444444",
  "rating": 5,
  "comment": "Ấm đun nước rất nhanh, giữ nhiệt tốt và điều khiển app tiện lợi.",
  "status": "Hidden",
  "createdAt": "2026-01-01T00:00:00Z",
  "updatedAt": "2026-11-10T06:55:00Z"
}
```

---

## 5. Ma trận Lỗi (RFC 9110 Error Matrix)

| HTTP Status | Error Code | Điều kiện kích hoạt | Error Message chuẩn |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_ACTION` | `action` không phải `"HIDE"` hoặc `"PUBLISH"` | `"Action must be either 'HIDE' or 'PUBLISH'."` |
| `401 Unauthorized` | `UNAUTHORIZED` | Không có JWT Token hoặc Token hết hạn | `"Authentication is required to perform this action."` |
| `403 Forbidden` | `FORBIDDEN` | Người dùng không có vai trò Admin | `"You do not have permission to moderate reviews."` |
| `404 Not Found` | `REVIEW_NOT_FOUND` | Không tìm thấy bài đánh giá với ID tương ứng | `"Product review not found."` |

