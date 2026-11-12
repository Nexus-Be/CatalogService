# CATALOG SERVICE - POST /api/v1/products/{id}/reviews

> **Feature / Use Case:** Khách hàng (Buyer) gửi đánh giá sản phẩm sau khi mua và nhận hàng thành công [CAT-62]  
> **Trạng thái:** ✅ VERIFIED  
> **Ngày cập nhật:** 2026-11-12  
> **Tác giả:** Nexus Team & AI Agent  

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `POST`
- **Route Endpoint:** `/api/v1/products/{id}/reviews`
- **Content-Type:** `multipart/form-data` (Hỗ trợ upload ảnh thực tế của khách hàng)
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

### 2.2 Form Data Parameters (`multipart/form-data`)
| Trường Form | Kiểu | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :---: | :--- | :--- |
| `orderId` | `Guid` | Có | UUIDv4 format | Mã đơn hàng đã hoàn tất thành công |
| `rating` | `short` | Có | Từ 1 đến 5 sao | Điểm số đánh giá chất lượng sản phẩm |
| `comment` | `string` | Không | Tối đa 2000 ký tự | Nhận xét chi tiết của người mua |
| `reviewImages` | `List<IFormFile>` | Không (Tùy chọn) | Tối đa 5 file; $\le$ 5MB/file; định dạng `.jpg`, `.jpeg`, `.png`, `.webp` | Các file ảnh thực tế của sản phẩm đính kèm bài đánh giá |

- **Ví dụ gọi bằng cURL (có upload ảnh):**
```bash
curl -X POST "http://localhost:8002/api/v1/products/f3f47581-6036-4fa1-b9fa-803770308c71/reviews" \
  -H "x-user-id: dddddddd-4444-4444-4444-444444444444" \
  -H "x-user-roles: buyer" \
  -F "orderId=99000000-0000-0000-0000-000000000001" \
  -F "rating=5" \
  -F "comment=Sản phẩm dùng rất tốt, ảnh chụp thực tế kèm theo đây." \
  -F "reviewImages=@/path/to/front.png" \
  -F "reviewImages=@/path/to/back.png"
```

- **Ví dụ gọi bằng cURL (không kèm ảnh):**
```bash
curl -X POST "http://localhost:8002/api/v1/products/f3f47581-6036-4fa1-b9fa-803770308c71/reviews" \
  -H "x-user-id: dddddddd-4444-4444-4444-444444444444" \
  -H "x-user-roles: buyer" \
  -F "orderId=99000000-0000-0000-0000-000000000001" \
  -F "rating=5" \
  -F "comment=Đóng gói kỹ càng và giao hàng nhanh chóng."
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
3. **Kiểm tra Thời hạn Đánh giá (30-Day Window Limit):**
   - Khách hàng chỉ được phép gửi đánh giá trong vòng 30 ngày kể từ khi đơn hàng hoàn thành (`purchase.CompletedAt`):
     ```csharp
     if (purchase.CompletedAt.HasValue && (now - purchase.CompletedAt.Value) > TimeSpan.FromDays(30))
     {
         throw new BusinessRuleValidationException(
             "REVIEW_WINDOW_EXPIRED",
             "You can only review products within 30 days of completing your purchase.");
     }
     ```
   - Nếu quá 30 ngày $\to$ Trả về `400 Bad Request` với mã lỗi `REVIEW_WINDOW_EXPIRED`.
4. **Quy tắc Chống Đánh giá Trùng lặp (Deduplication):**
   - Mỗi lần mua hàng chỉ được đánh giá 1 lần cho cùng 1 đơn hàng:
     ```csharp
     var alreadyReviewed = await _dbContext.ProductReviews
         .AnyAsync(r => r.PurchaseRecordId == purchase.Id && !r.IsDeleted, cancellationToken);
     ```
   - Nếu đã đánh giá $\to$ Trả về `409 Conflict` với mã lỗi `ALREADY_REVIEWED` ("This purchase has already been reviewed.").
5. **Xử lý Upload Ảnh Đánh giá (Optional Review Images):**
   - Nếu khách hàng đính kèm `reviewImages` (tối đa 5 file, $\le$ 5MB/file, định dạng `.jpg`, `.jpeg`, `.png`, `.webp`):
     - Backend tải từng file lên Cloudinary folder `"reviews"`.
     - Lưu danh sách URL ảnh dạng mảng `jsonb` vào cột `ReviewImages` của bảng `ProductReviews`.
   - Nếu không đính kèm ảnh: Cột `ReviewImages` nhận giá trị `NULL`.
6. **Lưu trữ Review & Cập nhật Xếp hạng (`ProductRatingStats`):**
   - Tạo mới `ProductReview`:
     - `PurchaseRecordId = purchase.Id`
     - `ProductId = id`
     - `BuyerId = currentUserId`
     - `Rating = request.Rating`
     - `Comment = request.Comment?.Trim()`
     - `ReviewImages = imageUrlsJson`
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
6. **Tích hợp liên dịch vụ (S2S UserService - Điều chỉnh Điểm Uy Tín Người Bán):**
   - Sau khi bài đánh giá được lưu thành công vào CSDL, CatalogService tự động gửi yêu cầu cập nhật uy tín người bán sang UserService:
     - **Endpoint gọi:** `POST api/v1/internal/users/reputation-adjust` (Port 8001)
     - **Payload:** `{ "userId": "<sellerId>", "targetRole": "Seller", "ratingStar": <rating>, "orderId": "<orderId>", "referenceId": "<orderId>" }`
     - **Nguyên tắc phân tầng:** CatalogService **không tự tính toán điểm trừ/cộng**, mà chuyển trực tiếp số sao nguyên bản (`ratingStar: 1..5`) và mã đơn hàng (`orderId`) cho UserService xử lý thuật toán uy tín (5★: +1, 4★ & 3★: 0, 2★: -1, 1★: -2, kiểm tra ngưỡng < 20 điểm để đình chỉ shop).
     - **Cơ chế chịu lỗi:** Non-blocking resilient execution (bọc try-catch và log warning). Nếu UserService tạm thời gián đoạn hoặc lỗi DB, phản hồi đánh giá của người mua vẫn trả về thành công `201 Created`.
7. **Tự động gửi thông báo In-App cho Người bán (Seller) qua NotificationService:**
   - CatalogService tự động gửi thông báo `NEW_PRODUCT_REVIEW` đến hộp thư của Seller cho mọi đánh giá từ 1★ đến 5★:
     - **4★ - 5★:** Tiêu đề `"Sản phẩm nhận được đánh giá mới ({rating} sao) ⭐"`, nội dung chúc mừng shop.
     - **3★:** Tiêu đề `"Sản phẩm nhận được đánh giá 3 sao"`.
     - **1★ - 2★:** Tiêu đề `"Cảnh báo: Sản phẩm nhận đánh giá tiêu cực ({rating} sao)"`, kèm lời nhắc: `"Đánh giá này có thể làm giảm điểm uy tín Shop, bạn nên kiểm tra và chủ động hỗ trợ khách hàng."`
   - **Cơ chế chịu lỗi:** Non-blocking resilient execution (bọc try-catch). Nếu NotificationService bận hoặc offline, review vẫn được tạo thành công `201 Created`.

---

## 4. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)

- **Mã HTTP thành công:** `201 Created`
- **Location Header:** `/api/v1/reviews/{reviewId}`
- **Khai báo C# Record:**
```csharp
public sealed record ProductReviewDto(
    Guid Id,
    Guid ProductId,
    Guid BuyerId,
    short Rating,
    string? Comment,
    List<string>? ReviewImages,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt = null
);
```

- **JSON Response mẫu (có upload ảnh):**
```json
{
  "id": "e4f8a123-5678-490a-bcde-123456789abc",
  "productId": "f3f47581-6036-4fa1-b9fa-803770308c71",
  "buyerId": "dddddddd-4444-4444-4444-444444444444",
  "rating": 5,
  "comment": "Sản phẩm dùng rất tốt, ảnh chụp thực tế kèm theo đây.",
  "reviewImages": [
    "https://res.cloudinary.com/u6yxkkop/image/upload/v1794461553/nexus_catalog/reviews/ca7jjmsdz3fqviymurhm.png",
    "https://res.cloudinary.com/u6yxkkop/image/upload/v1794461553/nexus_catalog/reviews/toq38ftdvi1vptrzvnva.png"
  ],
  "status": "Published",
  "createdAt": "2026-11-12T05:32:31Z",
  "updatedAt": null
}
```

- **JSON Response mẫu (không kèm ảnh):**
```json
{
  "id": "b7d6bf72-c887-4100-b5db-bc097d136bc6",
  "productId": "f3f47581-6036-4fa1-b9fa-803770308c71",
  "buyerId": "dddddddd-4444-4444-4444-444444444444",
  "rating": 5,
  "comment": "Đóng gói kỹ càng và giao hàng nhanh chóng.",
  "reviewImages": null,
  "status": "Published",
  "createdAt": "2026-11-12T05:32:31Z",
  "updatedAt": null
}
```

---

## 5. Ma trận Lỗi (RFC 9110 Error Matrix)

| HTTP Status | Error Code | Điều kiện kích hoạt | Error Message chuẩn |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `rating` không thuộc [1, 5] hoặc `comment` > 2000 ký tự | `"Rating must be between 1 and 5 stars."` |
| `400 Bad Request` | `VAL_INVALID_INPUT` | Quá 5 ảnh hoặc file không đúng định dạng (.jpg, .jpeg, .png, .webp) | `"Maximum 5 review images are allowed."` / `"File '{fileName}' has unsupported extension '{ext}'. Only .jpg, .jpeg, .png, .webp are allowed."` |
| `400 Bad Request` | `VAL_INVALID_INPUT` | File ảnh vượt quá 5MB | `"File '{fileName}' exceeds 5 MB limit."` |
| `400 Bad Request` | `REVIEW_WINDOW_EXPIRED` | Đơn hàng đã hoàn thành quá 30 ngày trước | `"The review period for this purchase has expired. Reviews must be submitted within 30 days of completion."` |
| `400 Bad Request` | `PRODUCT_BANNED` | Sản phẩm đã bị cấm vĩnh viễn trên sàn | `"Cannot review a banned product."` |
| `401 Unauthorized` | `UNAUTHORIZED` | Không có JWT Token hoặc Token hết hạn | `"Authentication is required to perform this action."` |
| `403 Forbidden` | `PURCHASE_NOT_FOUND` | Người dùng chưa từng mua sản phẩm này trong đơn hàng đã hoàn tất | `"You can only review products from your completed purchases."` |
| `404 Not Found` | `PRODUCT_NOT_FOUND` | Không tìm thấy sản phẩm trong hệ thống | `"Product not found in the system."` |
| `409 Conflict` | `ALREADY_REVIEWED` | Đơn mua này đã được đánh giá trước đó | `"This purchase has already been reviewed."` |

