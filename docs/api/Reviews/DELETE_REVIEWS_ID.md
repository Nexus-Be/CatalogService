# CATALOG SERVICE - DELETE /api/v1/reviews/{id}

> **Feature / Use Case:** Khách hàng hoặc Admin xóa bài đánh giá sản phẩm [CAT-64]  
> **Trạng thái:** ⏳ DRAFT (Pre-Code Specification)  
> **Ngày tạo:** 2026-11-10  
> **Tác giả:** Nexus Team & AI Agent  

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `DELETE`
- **Route Endpoint:** `/api/v1/reviews/{id}`
- **Quyền hạn (RBAC Policy):** `[RequireRole("buyer", "admin")]` (Bắt buộc người dùng đã đăng nhập)
- **Header bắt buộc:**
  - `Authorization: Bearer <JWT_Token>`
  - `X-User-Id: <Guid>` (Hoặc trích xuất từ Token claim `sub`/`userId`)
- **Controller:** `Catalog.Api/Controllers/Public/ReviewsController.cs`
- **Application Contract:** `Catalog.Application/Services/Review/IProductReviewService.cs` (`DeleteReviewAsync`)

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)

### 2.1 Route Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `id` | Route | `Guid` | Có | UUIDv4 format | Khóa chính của bài đánh giá cần xóa |

---

## 3. Quy tắc Nghiệp vụ & Rào chắn (Business Rules & Recalculation)

1. **Kiểm tra Quyền Sở hữu (Ownership Guardrail):**
   - Tìm kiếm bài đánh giá theo `Id == id` và `!IsDeleted`. Nếu không tìm thấy $\to$ Trả về `404 REVIEW_NOT_FOUND`.
   - Nếu `review.BuyerId != currentUserId` và người gọi không phải `Admin` $\to$ Trả về `403 FORBIDDEN` ("You can only delete your own reviews.").
2. **Xóa mềm & Giảm Thống kê Xếp hạng (`ProductRatingStats`):**
   - Nếu bài đánh giá đang ở trạng thái `Published`:
     - Giảm điểm trong bảng `ProductRatingStats`:
       ```csharp
       stats.StarSum -= review.Rating;
       stats.ReviewCount -= 1;
       stats.AvgRating = stats.ReviewCount > 0 
           ? Math.Round((decimal)stats.StarSum / stats.ReviewCount, 2) 
           : null;
       ```
   - Đánh dấu xóa mềm bài đánh giá:
     - `review.IsDeleted = true`
     - `review.DeletedAt = DateTimeOffset.UtcNow`
3. **Thực thi lưu trữ:**
   - Thực hiện trong cùng transaction qua `SaveChangesAsync()`.

---

## 4. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)

- **Mã HTTP thành công:** `204 No Content`
- **Response Body:** Không có nội dung trả về (Empty body).

---

## 5. Ma trận Lỗi (RFC 9110 Error Matrix)

| HTTP Status | Error Code | Điều kiện kích hoạt | Error Message chuẩn |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `id` không phải định dạng GUID hợp lệ | `"Invalid review ID format."` |
| `401 Unauthorized` | `UNAUTHORIZED` | Không có JWT Token hoặc Token hết hạn | `"Authentication is required to perform this action."` |
| `403 Forbidden` | `FORBIDDEN` | Người dùng cố tình xóa bài đánh giá của người khác | `"You can only delete your own reviews."` |
| `404 Not Found` | `REVIEW_NOT_FOUND` | Không tìm thấy bài đánh giá với ID tương ứng | `"Product review not found."` |

