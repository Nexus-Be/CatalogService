# CATALOG SERVICE - POST /api/v1/products/{id}/reviews

> **Feature / Use Case:** Khách hàng (Buyer) gửi đánh giá sản phẩm sau khi mua và nhận hàng thành công [CAT-62]  
> **Trạng thái:** ⏳ DRAFT (Pre-Code Specification)  
> **Ngày tạo:** 2026-11-10  
> **Tác giả:** Nexus Team & AI Agent  

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `POST`
- **Route Endpoint:** `/api/v1/products/{id}/reviews`
- **Quyền hạn (RBAC Policy):** `[RequireRole("buyer", "admin")]` (Bắt buộc người dùng đã đăng nhập)
- **Header bắt buộc:**
  - `Authorization: Bearer <JWT_Token>`
  - `X-User-Id: <Guid>` (Hoặc trích xuất từ Token claim `sub`/`userId`)
- **Controller:** `Catalog.Api/Controllers/Public/ProductReviewsController.cs`
- **Application Contract:** `Catalog.Application/Services/Review/IProductReviewService.cs` (`CreateReviewAsync`)

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)

### 2.1 Route Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `id` | Route | `Guid` | Có | UUIDv4 format | Khóa chính của sản phẩm được đánh giá |

### 2.2 Request Body Record DTO (`CreateProductReviewRequest`)
- **Vị trí file:** `Catalog.Application/Services/Review/ReviewRequests.cs`
- **Khai báo C# Record:**
```csharp
public sealed record CreateProductReviewRequest(
    [Required(ErrorMessage = "Order ID is required.")]
    Guid OrderId,

    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5 stars.")]
    short Rating,

    [MaxLength(2000, ErrorMessage = "Comment cannot exceed 2000 characters.")]
    string? Comment = null
);
```

- **JSON Request Body mẫu:**
```json
{
  "orderId": "99000000-0000-0000-0000-000000000001",
  "rating": 5,
  "comment": "Sản phẩm dùng rất tốt, đóng gói kỹ càng và giao hàng nhanh chóng."
}
```

---

## 3. Quy tắc Nghiệp vụ & Rào chắn (Business Rules & Recalculation)

1. **Kiểm tra Sản phẩm:**
   - Sản phẩm có `Id == id` phải tồn tại trong CSDL và chưa bị xóa mềm. Nếu không tồn tại $\to$ Trả về `404 PRODUCT_NOT_FOUND`.
   - Nếu sản phẩm đã bị cấm (`Status == ProductStatus.Banned`) $\to$ Trả về `400 PRODUCT_BANNED` ("Cannot review a banned product.").
2. **Kiểm tra Xác thực Mua hàng (Verified Purchase):**
   - Hệ thống tìm kiếm bản ghi trong bảng `PurchaseRecords`:
     ```csharp
     var purchase = await _dbContext.PurchaseRecords
         .FirstOrDefaultAsync(p => p.BuyerId == currentUserId 
                                && p.ProductId == id 
                                && p.OrderId == request.OrderId 
                                && !p.IsDeleted, cancellationToken);
     ```
   - Nếu không tìm thấy $\to$ Trả về `403 PURCHASE_NOT_FOUND` ("You can only review products from your completed purchases.").
3. **Quy tắc Chống Đánh giá Trùng lặp (Deduplication):**
   - Mỗi lần mua hàng chỉ được đánh giá 1 lần cho cùng 1 đơn hàng:
     ```csharp
     var alreadyReviewed = await _dbContext.ProductReviews
         .AnyAsync(r => r.PurchaseRecordId == purchase.Id && !r.IsDeleted, cancellationToken);
     ```
   - Nếu đã đánh giá $\to$ Trả về `409 Conflict` với mã lỗi `ALREADY_REVIEWED` ("This purchase has already been reviewed.").
4. **Lưu trữ Review & Cập nhật Xếp hạng (`ProductRatingStats`):**
   - Tạo mới `ProductReview`:
     - `PurchaseRecordId = purchase.Id`
     - `ProductId = id`
     - `BuyerId = currentUserId`
     - `Rating = request.Rating`
     - `Comment = request.Comment?.Trim()`
     - `Status = ReviewStatus.Published`
   - Cập nhật thống kê `ProductRatingStats` tương ứng với `ProductId == id`:
     - Nếu chưa có bản ghi `ProductRatingStats`: Khởi tạo mới với `StarSum = Rating`, `ReviewCount = 1`, `AvgRating = Rating`.
     - Nếu đã có:
       ```csharp
       stats.StarSum += request.Rating;
       stats.ReviewCount += 1;
       stats.AvgRating = Math.Round((decimal)stats.StarSum / stats.ReviewCount, 2);
       ```
   - Thực thi `await _dbContext.SaveChangesAsync(cancellationToken)` trong cùng một transaction.

---

## 4. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)

- **Mã HTTP thành công:** `201 Created`
- **Location Header:** `/api/v1/reviews/{reviewId}`
- **JSON Response mẫu:**
```json
{
  "id": "e4f8a123-5678-490a-bcde-123456789abc",
  "productId": "33000000-0000-0000-0000-000000000003",
  "buyerId": "dddddddd-4444-4444-4444-444444444444",
  "rating": 5,
  "comment": "Sản phẩm dùng rất tốt, đóng gói kỹ càng và giao hàng nhanh chóng.",
  "status": "Published",
  "createdAt": "2026-11-10T06:45:00Z",
  "updatedAt": null
}
```

---

## 5. Ma trận Lỗi (RFC 9110 Error Matrix)

| HTTP Status | Error Code | Điều kiện kích hoạt | Error Message chuẩn |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `rating` không thuộc [1, 5] hoặc `comment` > 2000 ký tự | `"Rating must be between 1 and 5 stars."` |
| `400 Bad Request` | `PRODUCT_BANNED` | Sản phẩm đã bị cấm vĩnh viễn trên sàn | `"Cannot review a banned product."` |
| `401 Unauthorized` | `UNAUTHORIZED` | Không có JWT Token hoặc Token hết hạn | `"Authentication is required to perform this action."` |
| `403 Forbidden` | `PURCHASE_NOT_FOUND` | Người dùng chưa từng mua sản phẩm này trong đơn hàng đã hoàn tất | `"You can only review products from your completed purchases."` |
| `404 Not Found` | `PRODUCT_NOT_FOUND` | Không tìm thấy sản phẩm trong hệ thống | `"Product not found in the system."` |
| `409 Conflict` | `ALREADY_REVIEWED` | Đơn mua này đã được đánh giá trước đó | `"This purchase has already been reviewed."` |

