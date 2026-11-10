# CATALOG SERVICE - POST /api/v1/products/{id}/reports

> **Feature / Use Case:** Khách hàng (Buyer) hoặc Đối tác gửi đơn tố cáo sản phẩm vi phạm chính sách [CAT-57]  
> **Trạng thái:** ⏳ DRAFT (Pre-Code Specification)  
> **Ngày tạo:** 2026-11-10  
> **Tác giả:** Nexus Team & AI Agent  

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `POST`
- **Route Endpoint:** `/api/v1/products/{id}/reports`
- **Quyền hạn (RBAC Policy):** `[RequireRole("buyer", "seller")]` (Bắt buộc người dùng đã đăng nhập)
- **Header bắt buộc:**
  - `X-User-Id: <Guid>` (UUID của người dùng gửi đơn tố cáo)
  - `X-User-Roles: buyer` hoặc `seller`
- **Controller:** `Catalog.Api/Controllers/Public/ProductReportsController.cs` (hoặc `ProductsController.cs`)
- **Application Contract:** `Catalog.Application/Services/Report/IProductReportService.cs` (`SubmitProductReportAsync`)

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)

### 2.1 Route Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `id` | Route | `Guid` | Có | UUIDv4 format | Khóa chính của sản phẩm bị tố cáo |

### 2.2 Request Body Record DTO (`CreateProductReportRequest`)
- **Vị trí file:** `Catalog.Application/Services/Report/ReportRequests.cs`
- **Khai báo C# Record:**
```csharp
public sealed record CreateProductReportRequest(
    [Required(ErrorMessage = "Report reason is required.")]
    string Reason,

    [Required(ErrorMessage = "Description is required.")]
    [StringLength(2000, MinimumLength = 10, ErrorMessage = "Description must be between 10 and 2000 characters.")]
    string Description,

    [MaxLength(5, ErrorMessage = "Maximum 5 evidence images are allowed.")]
    List<string>? EvidenceImages = null
);
```

- **JSON Request Body mẫu:**
```json
{
  "reason": "Counterfeit",
  "description": "Sản phẩm nhận được có dấu hiệu làm giả logo, không có tem chống giả và nhãn mác trùng khớp với hãng.",
  "evidenceImages": [
    "https://storage.nexus.com/evidence/2026/report_img_1.jpg",
    "https://storage.nexus.com/evidence/2026/report_img_2.jpg"
  ]
}
```

---

## 3. Quy tắc Nghiệp vụ & Rào chắn (Business Rules & Anti-Spam)

1. **Kiểm tra Sản phẩm:**
   - Sản phẩm có `Id == id` phải tồn tại trong CSDL và chưa bị xóa mềm (`!IsDeleted`). Nếu không tồn tại $\to$ Trả về `404 PRODUCT_NOT_FOUND`.
   - Nếu sản phẩm đã ở trạng thái cấm (`Status == ProductStatus.Banned`) $\to$ Trả về `400 PRODUCT_ALREADY_BANNED` với thông điệp: `"This product has already been permanently banned."`.
2. **Quy tắc Chống Spam (Anti-Spam & Deduplication):**
   - Một người dùng (`ReporterId`) **không được phép gửi nhiều báo cáo liên tiếp** cho cùng một sản phẩm khi đơn báo cáo trước đó vẫn đang trong quá trình xử lý:
     ```csharp
     var hasActiveReport = await _dbContext.ProductReports
         .AnyAsync(r => r.ProductId == id 
                     && r.ReporterId == reporterId 
                     && (r.Status == ReportStatus.Pending || r.Status == ReportStatus.Investigating)
                     && !r.IsDeleted, cancellationToken);
     ```
   - Nếu vi phạm $\to$ Trả về `409 Conflict` với mã lỗi `ALREADY_REPORTED` và thông điệp: `"You have already submitted a report for this product that is currently under review."`.
3. **Lưu trữ & Trạng thái Ban đầu:**
   - Tạo bản ghi mới trong bảng `ProductReports` với `Status = ReportStatus.Pending`.
   - Thuộc tính `EvidenceImages` được serialize dưới dạng `jsonb` lưu vào PostgreSQL.
   - Ghi nhận `AuditLog` với `Action = AuditAction.ProductReport`.

---

## 4. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)

- **Mã HTTP thành công:** `201 Created`
- **Location Header:** `/api/v1/products/{id}/reports/{reportId}`
- **Khai báo C# Record:**
```csharp
public sealed record ProductReportDto(
    Guid Id,
    Guid ProductId,
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
  "id": "e1111111-2222-3333-4444-555555555555",
  "productId": "77777777-7777-7777-7777-777777777777",
  "reporterId": "99999999-9999-9999-9999-999999999999",
  "reason": "Counterfeit",
  "description": "Sản phẩm nhận được có dấu hiệu làm giả logo, không có tem chống giả và nhãn mác trùng khớp với hãng.",
  "evidenceImages": [
    "https://storage.nexus.com/evidence/2026/report_img_1.jpg",
    "https://storage.nexus.com/evidence/2026/report_img_2.jpg"
  ],
  "status": "Pending",
  "createdAt": "2026-11-10T09:30:00Z",
  "reviewedBy": null,
  "reviewedAt": null,
  "resolutionNote": null
}
```

---

## 5. Ma trận Mã lỗi RFC 9110 (Error Matrix)

| HTTP Status | Error Code | Detail Message (Sentence case) | Điều kiện phát sinh |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Report reason is required.` | Trường `reason` rỗng |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Invalid report reason '{0}'. Allowed values: Counterfeit, ProhibitedItem, IncorrectDescription, OffensiveContent, Other.` | Lý do tố cáo không nằm trong enum |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Description must be between 10 and 2000 characters.` | Độ dài mô tả vi phạm vi phạm giới hạn |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Maximum 5 evidence images are allowed.` | Mảng ảnh bằng chứng vượt quá 5 URL |
| `400 Bad Request` | `PRODUCT_ALREADY_BANNED` | `This product has already been permanently banned.` | Báo cáo sản phẩm đã bị cấm trước đó |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required to access this resource.` | Không gửi kèm token hoặc thiếu `X-User-Id` |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to access or modify this resource.` | Token không có role `buyer` hoặc `seller` |
| `404 Not Found` | `PRODUCT_NOT_FOUND` | `Product not found in the system.` | Sản phẩm không tồn tại hoặc đã bị xóa |
| `409 Conflict` | `ALREADY_REPORTED` | `You have already submitted a report for this product that is currently under review.` | Người dùng đã có đơn báo cáo chưa được xử lý |

---

## 6. Bảng Ánh xạ Enum `ReportReason`

| C# Enum Member | Giá trị số (DB) | Giá trị JSON API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `ReportReason.Counterfeit` | `0` | `"Counterfeit"` | Hàng giả, hàng nhái, vi phạm quyền SHTT |
| `ReportReason.ProhibitedItem` | `1` | `"ProhibitedItem"` | Hàng cấm buôn bán theo quy định pháp luật |
| `ReportReason.IncorrectDescription` | `2` | `"IncorrectDescription"` | Mô tả sai lệch, lừa dối người tiêu dùng |
| `ReportReason.OffensiveContent` | `3` | `"OffensiveContent"` | Nội dung/hình ảnh phản cảm, bạo lực, khiêu dâm |
| `ReportReason.Other` | `4` | `"Other"` | Lý do vi phạm khác kèm giải thích |
