# CATALOG SERVICE - GET /api/v1/admin/reports/products

> **Feature / Use Case:** Quản trị viên và Nhân viên xem danh sách hàng đợi các đơn tố cáo vi phạm sản phẩm [CAT-58]  
> **Trạng thái:** ⏳ DRAFT (Pre-Code Specification)  
> **Ngày tạo:** 2026-11-10  
> **Tác giả:** Nexus Team & AI Agent  

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/api/v1/admin/reports/products` (Canonical route duy nhất)
- **Quyền hạn (RBAC Policy):** `[RequireRole("admin", "staff")]`
- **Header bắt buộc:**
  - `X-User-Id: <Guid>` (UUID của Quản trị viên hoặc Nhân viên thực hiện tra cứu)
  - `X-User-Roles: admin` hoặc `X-User-Roles: staff`
- **Controller:** `Catalog.Api/Controllers/Public/AdminProductReportsController.cs` (hoặc `AdminProductsController.cs`)
- **Application Contract:** `Catalog.Application/Services/Report/IProductReportService.cs` (`GetAdminReportsAsync`)

---

## 2. Đầu vào & Query Parameters (Input & Query Schema)

### 2.1 Query Parameters
| Tham số | Kiểu C# | Mặc định | Giới hạn | Mô tả |
| :--- | :--- | :---: | :--- | :--- |
| `page` | `int` | `1` | $\ge 1$ | Số trang hiện tại |
| `pageSize` | `int` | `20` | $1 - 100$ | Số lượng đơn tố cáo trên một trang |
| `status` | `string?` | `null` | `Pending`, `Investigating`, `Resolved`, `Dismissed` | Lọc theo trạng thái xử lý của đơn |
| `reason` | `string?` | `null` | `Counterfeit`, `ProhibitedItem`, `IncorrectDescription`, `OffensiveContent`, `Other` | Lọc theo lý do tố cáo |
| `productId` | `Guid?` | `null` | UUIDv4 hợp lệ | Lọc tất cả đơn tố cáo của một sản phẩm cụ thể |
| `sellerId` | `Guid?` | `null` | UUIDv4 hợp lệ | Lọc các đơn tố cáo nhắm vào các sản phẩm của một Seller |
| `sortBy` | `string?` | `"createdAt"` | `createdAt`, `status` | Trường sắp xếp dữ liệu |
| `sortDirection` | `string?` | `"desc"` | `asc`, `desc` | Chiều sắp xếp (Mặc định đơn mới nhất lên đầu) |

---

## 3. Quy tắc Truy vấn Nghiệp vụ (Business Query Rules)

1. **Truy vấn CSDL & Soft Delete:**
   - Chỉ lấy các đơn tố cáo chưa bị xóa mềm (`!r.IsDeleted`).
   - Kết nối với thông tin sản phẩm (`Include(r => r.Product)`) để lấy `ProductName` và `SellerId`.
2. **Hàng đợi Ưu tiên:**
   - Mặc định khi không truyền `status`, API trả về tất cả các đơn, ưu tiên sắp xếp các đơn `Pending` và thời gian tạo gần nhất lên trước (`OrderByDescending(r => r.CreatedAt)`).
3. **Phân trang DB:**
   - Thực hiện `CountAsync` để lấy tổng số bản ghi và phân trang thông qua `Skip((page - 1) * pageSize).Take(pageSize)`.

---

## 4. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)

- **Mã HTTP thành công:** `200 OK`
- **Kiểu trả về:** `PagedData<ProductReportListItemDto>`
- **Khai báo C# Record:**
```csharp
public sealed record ProductReportListItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    Guid SellerId,
    Guid ReporterId,
    string Reason,
    string Description,
    List<string>? EvidenceImages,
    string Status,
    DateTimeOffset CreatedAt,
    Guid? ReviewedBy = null,
    DateTimeOffset? ReviewedAt = null,
    string? ResolutionNote = null
);
```

- **JSON Response mẫu:**
```json
{
  "items": [
    {
      "id": "e1111111-2222-3333-4444-555555555555",
      "productId": "77777777-7777-7777-7777-777777777777",
      "productName": "Điện thoại Phone 16 Pro Max 256GB Hàng Nhái",
      "sellerId": "33333333-3333-3333-3333-333333333333",
      "reporterId": "99999999-9999-9999-9999-999999999999",
      "reason": "Counterfeit",
      "description": "Sản phẩm có dấu hiệu làm giả logo, không có tem chống giả.",
      "evidenceImages": [
        "https://storage.nexus.com/evidence/2026/report_img_1.jpg"
      ],
      "status": "Pending",
      "createdAt": "2026-11-10T09:30:00Z",
      "reviewedBy": null,
      "reviewedAt": null,
      "resolutionNote": null
    }
  ],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "totalCount": 1,
    "totalPages": 1,
    "hasNextPage": false,
    "hasPreviousPage": false
  }
}
```

---

## 5. Ma trận Mã lỗi RFC 9110 (Error Matrix)

| HTTP Status | Error Code | Detail Message (Sentence case) | Điều kiện phát sinh |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_PAGINATION` | `Page must be greater than or equal to 1, and pageSize must be between 1 and 100.` | `page < 1` hoặc `pageSize < 1` hoặc `pageSize > 100` |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Invalid status filter '{0}'. Allowed values: Pending, Investigating, Resolved, Dismissed.` | Giá trị lọc `status` không hợp lệ |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required to access this resource.` | Không gửi kèm token hoặc thiếu `X-User-Id` |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to access or modify this resource.` | Token không có role `admin` hoặc `staff` |

---

## 6. Bảng Ánh xạ Enum `ReportStatus`

| C# Enum Member | Giá trị số (DB) | Giá trị JSON API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `ReportStatus.Pending` | `0` | `"Pending"` | Đơn mới gửi, chờ Staff tiếp nhận |
| `ReportStatus.Investigating` | `1` | `"Investigating"` | Staff đã mở đơn và đang kiểm tra đối soát |
| `ReportStatus.Resolved` | `2` | `"Resolved"` | Đã giải quyết (chấp thuận tố cáo, cấm sản phẩm) |
| `ReportStatus.Dismissed` | `3` | `"Dismissed"` | Đơn bị bác bỏ (tố cáo sai sự thật, không đủ chứng cứ) |
