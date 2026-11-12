# CATALOG SERVICE - GET /api/v1/products/{id}/rating-summary

> **Feature / Use Case:** Xem tổng quan điểm trung bình và phân phối xếp hạng sao của sản phẩm [CAT-60]  
> **Trạng thái:** ✅ VERIFIED  
> **Ngày cập nhật:** 2026-11-12  
> **Tác giả:** Nexus Team & AI Agent  

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/api/v1/products/{id}/rating-summary`
- **Quyền hạn (RBAC Policy):** `[AllowAnonymous]` (Công khai cho toàn bộ khách xem hàng)
- **Controller:** `Catalog.Api/Controllers/Public/ProductReviewsController.cs`
- **Application Contract:** `Catalog.Application/Services/Review/IProductReviewService.cs` (`GetRatingSummaryAsync`)

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)

### 2.1 Route Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `id` | Route | `Guid` | Có | UUIDv4 format | Khóa chính của sản phẩm cần lấy tổng quan đánh giá |

---

## 3. Quy tắc Nghiệp vụ & Rào chắn (Business Rules & Calculations)

1. **Kiểm tra Sản phẩm:**
   - Sản phẩm có `Id == id` phải tồn tại trong CSDL và chưa bị xóa mềm (`!IsDeleted`). Nếu không tồn tại $\to$ Trả về `404 PRODUCT_NOT_FOUND`.
2. **Truy vấn Thông tin Xếp hạng:**
   - Truy vấn từ bảng `ProductRatingStats` theo `ProductId == id`.
   - Nếu sản phẩm chưa từng có đánh giá nào (hoặc bản ghi stats chưa có):
     - `AvgRating = null`
     - `ReviewCount = 0`
     - `StarSum = 0`
     - `Distribution` = Tất cả các sao đều bằng `0`.
   - Nếu sản phẩm đã có đánh giá:
     - Đọc trực tiếp `AvgRating`, `ReviewCount`, `StarSum` từ `ProductRatingStats`.
     - Thống kê phân phối số lượng từng loại sao (5★, 4★, 3★, 2★, 1★) từ các bài đánh giá có `Status == ReviewStatus.Published && !IsDeleted`.

---

## 4. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)

- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record StarDistributionDto(
    int Star5,
    int Star4,
    int Star3,
    int Star2,
    int Star1
);

public sealed record ProductRatingSummaryDto(
    Guid ProductId,
    decimal? AvgRating,
    int ReviewCount,
    int StarSum,
    StarDistributionDto Distribution
);
```

- **JSON Response mẫu (Đã có đánh giá):**
```json
{
  "productId": "33000000-0000-0000-0000-000000000003",
  "avgRating": 4.8,
  "reviewCount": 10,
  "starSum": 48,
  "distribution": {
    "star5": 8,
    "star4": 2,
    "star3": 0,
    "star2": 0,
    "star1": 0
  }
}
```

- **JSON Response mẫu (Chưa có đánh giá nào):**
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

## 5. Ma trận Lỗi (RFC 9110 Error Matrix)

| HTTP Status | Error Code | Điều kiện kích hoạt | Error Message chuẩn |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `id` không phải định dạng GUID hợp lệ | `"Invalid product ID format."` |
| `404 Not Found` | `PRODUCT_NOT_FOUND` | Không tìm thấy sản phẩm trong hệ thống | `"Product not found in the system."` |
| `500 Internal Error`| `INTERNAL_SERVER_ERROR` | Lỗi kết nối CSDL hoặc lỗi hệ thống chưa xử lý | `"An unexpected error occurred while processing your request."` |

