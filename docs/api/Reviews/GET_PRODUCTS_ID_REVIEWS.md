# CATALOG SERVICE - GET /api/v1/products/{id}/reviews

> **Feature / Use Case:** Xem danh sách bài đánh giá của sản phẩm có phân trang, lọc theo số sao và sắp xếp [CAT-61]  
> **Trạng thái:** ✅ VERIFIED  
> **Ngày cập nhật:** 2026-11-12  
> **Tác giả:** Nexus Team & AI Agent  

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/api/v1/products/{id}/reviews`
- **Quyền hạn (RBAC Policy):** `[AllowAnonymous]` (Công khai cho toàn bộ khách xem hàng)
- **Controller:** `Catalog.Api/Controllers/Public/ProductReviewsController.cs`
- **Application Contract:** `Catalog.Application/Services/Review/IProductReviewService.cs` (`GetProductReviewsAsync`)

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)

### 2.1 Route Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `id` | Route | `Guid` | Có | UUIDv4 format | Khóa chính của sản phẩm cần lấy danh sách đánh giá |

### 2.2 Query Parameters
| Tham số | Vị trí | Kiểu C# | Mặc định | Giới hạn | Ý nghĩa |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `page` | Query | `int` | `1` | $\ge 1$ | Số trang hiện tại |
| `pageSize` | Query | `int` | `10` | $1 \le pageSize \le 50$ | Số lượng đánh giá mỗi trang |
| `rating` | Query | `short?` | `null` | $1 \le rating \le 5$ | Lọc theo số sao cụ thể (null = xem tất cả) |
| `sortBy` | Query | `string?` | `"newest"` | `newest`, `highest_rating`, `lowest_rating` | Tiêu chí sắp xếp danh sách |

---

## 3. Quy tắc Nghiệp vụ & Rào chắn (Business Rules & Filtering)

1. **Kiểm tra Sản phẩm:**
   - Sản phẩm có `Id == id` phải tồn tại trong CSDL và chưa bị xóa mềm (`!IsDeleted`). Nếu không tồn tại $\to$ Trả về `404 PRODUCT_NOT_FOUND`.
2. **Bộ lọc & Trạng thái bài đánh giá:**
   - **Chỉ lấy bài đã duyệt:** `Status == ReviewStatus.Published && !IsDeleted`.
   - Các bài bị ẩn (`Status == ReviewStatus.Hidden`) hoặc đã xóa mềm tuyệt đối không hiển thị cho người xem public.
   - Nếu có `rating` (từ 1 đến 5): Thêm điều kiện `Rating == rating`. Nếu `rating` ngoài khoảng 1–5 $\to$ Trả về `400 VAL_INVALID_RATING`.
3. **Sắp xếp (Sorting):**
   - `newest`: `OrderByDescending(r => r.CreatedAt)` (Mặc định).
   - `highest_rating`: `OrderByDescending(r => r.Rating).ThenByDescending(r => r.CreatedAt)`.
   - `lowest_rating`: `OrderBy(r => r.Rating).ThenByDescending(r => r.CreatedAt)`.
4. **Phân trang tại Database:**
   - Dùng `.Skip((page - 1) * pageSize).Take(pageSize)` và đếm tổng bằng `.CountAsync()`.
   - Đóng gói kết quả dưới dạng `PagedData<ProductReviewDto>`.

---

## 4. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)

- **Mã HTTP thành công:** `200 OK`
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
    DateTimeOffset? UpdatedAt
);
```

- **JSON Response mẫu:**
```json
{
  "items": [
    {
      "id": "488c5c33-0809-43ed-8418-8c9ebe1f66ab",
      "productId": "f3f47581-6036-4fa1-b9fa-803770308c71",
      "buyerId": "dddddddd-4444-4444-4444-444444444444",
      "rating": 5,
      "comment": "Sản phẩm dùng rất tốt, ảnh chụp thực tế kèm theo đây.",
      "reviewImages": [
        "https://res.cloudinary.com/u6yxkkop/image/upload/v1794461553/nexus_catalog/reviews/ca7jjmsdz3fqviymurhm.png"
      ],
      "status": "Published",
      "createdAt": "2026-11-12T05:32:31Z",
      "updatedAt": null
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalItems": 1,
  "totalPages": 1,
  "hasNextPage": false,
  "hasPreviousPage": false
}
```

---

## 5. Ma trận Lỗi (RFC 9110 Error Matrix)

| HTTP Status | Error Code | Điều kiện kích hoạt | Error Message chuẩn |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `id` không phải định dạng GUID hợp lệ | `"Invalid product ID format."` |
| `400 Bad Request` | `VAL_INVALID_PAGINATION` | `page < 1` hoặc `pageSize < 1` hoặc `pageSize > 50` | `"Page must be at least 1, and page size must be between 1 and 50."` |
| `400 Bad Request` | `VAL_INVALID_RATING` | `rating` truyền vào không nằm trong đoạn 1–5 | `"Rating filter must be between 1 and 5."` |
| `404 Not Found` | `PRODUCT_NOT_FOUND` | Không tìm thấy sản phẩm trong hệ thống | `"Product not found in the system."` |

