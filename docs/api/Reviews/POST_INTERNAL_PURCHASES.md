# CATALOG SERVICE - POST /api/v1/internal/purchases

> **Feature / Use Case:** Đồng bộ hóa bản ghi đơn hàng đã hoàn tất từ OrderService để kích hoạt quyền đánh giá cho khách mua [CAT-66]  
> **Trạng thái:** ⏳ DRAFT (Pre-Code Specification)  
> **Ngày tạo:** 2026-11-10  
> **Tác giả:** Nexus Team & AI Agent  

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `POST`
- **Route Endpoint:** `/api/v1/internal/purchases`
- **Quyền hạn (RBAC Policy):** `[AllowAnonymous]` (Internal Service Communication / Dev Test)
- **Controller:** `Catalog.Api/Controllers/Internal/InternalPurchasesController.cs`
- **Application Contract:** `Catalog.Application/Services/Review/IProductReviewService.cs` (`SyncPurchaseRecordAsync`)

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)

### 2.1 Request Body Record DTO (`SyncPurchaseRecordRequest`)
- **Vị trí file:** `Catalog.Application/Services/Review/ReviewRequests.cs`
- **Khai báo C# Record:**
```csharp
public sealed record SyncPurchaseRecordRequest(
    [Required(ErrorMessage = "Buyer ID is required.")]
    Guid BuyerId,

    [Required(ErrorMessage = "Order ID is required.")]
    Guid OrderId,

    [Required(ErrorMessage = "Product ID is required.")]
    Guid ProductId,

    DateTimeOffset? CompletedAt = null
);
```

- **JSON Request Body mẫu:**
```json
{
  "buyerId": "dddddddd-4444-4444-4444-444444444444",
  "orderId": "99000000-0000-0000-0000-000000000001",
  "productId": "33000000-0000-0000-0000-000000000003",
  "completedAt": "2026-11-10T07:00:00Z"
}
```

---

## 3. Quy tắc Nghiệp vụ & Rào chắn (Business Rules & Idempotency)

1. **Kiểm tra Sản phẩm:**
   - Sản phẩm có `Id == ProductId` phải tồn tại trong CSDL. Nếu không $\to$ Trả về `404 PRODUCT_NOT_FOUND`.
2. **Tính Bất biến & Chống trùng lặp (Idempotency):**
   - Kiểm tra xem cặp `(BuyerId, OrderId, ProductId)` đã tồn tại trong bảng `PurchaseRecords` chưa:
     ```csharp
     var existing = await _dbContext.PurchaseRecords
         .FirstOrDefaultAsync(p => p.BuyerId == request.BuyerId 
                                && p.OrderId == request.OrderId 
                                && p.ProductId == request.ProductId 
                                && !p.IsDeleted, cancellationToken);
     ```
   - Nếu đã tồn tại: Không tạo trùng lặp, trả về `200 OK` với ID bản ghi đã có.
3. **Tạo mới:**
   - Nếu chưa tồn tại: Tạo mới `PurchaseRecord` với `CompletedAt = request.CompletedAt ?? DateTimeOffset.UtcNow` và trả về `201 Created`.

---

## 4. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)

- **Mã HTTP thành công:** `201 Created` (nếu mới tạo) hoặc `200 OK` (nếu đã tồn tại)
- **JSON Response mẫu:**
```json
{
  "id": "33400000-0000-0000-0000-000000000001",
  "buyerId": "dddddddd-4444-4444-4444-444444444444",
  "orderId": "99000000-0000-0000-0000-000000000001",
  "productId": "33000000-0000-0000-0000-000000000003",
  "completedAt": "2026-11-10T07:00:00Z",
  "createdAt": "2026-11-10T07:00:00Z"
}
```

---

## 5. Ma trận Lỗi (RFC 9110 Error Matrix)

| HTTP Status | Error Code | Điều kiện kích hoạt | Error Message chuẩn |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | Một trong các trường `BuyerId`, `OrderId`, `ProductId` bị thiếu | `"All identification fields are required."` |
| `404 Not Found` | `PRODUCT_NOT_FOUND` | Không tìm thấy sản phẩm với `ProductId` tương ứng | `"Product not found in the system."` |

