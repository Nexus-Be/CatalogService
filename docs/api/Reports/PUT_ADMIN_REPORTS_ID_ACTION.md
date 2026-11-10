# CATALOG SERVICE - PUT /api/v1/admin/reports/products/{reportId}/action

> **Feature / Use Case:** Quản trị viên và Nhân viên đưa ra phán quyết xử lý đơn tố cáo vi phạm sản phẩm [CAT-59]  
> **Trạng thái:** ⏳ DRAFT (Pre-Code Specification)  
> **Ngày tạo:** 2026-11-10  
> **Tác giả:** Nexus Team & AI Agent  

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `PUT`
- **Route Endpoint:** `/api/v1/admin/reports/products/{reportId}/action` (Canonical route duy nhất)
- **Quyền hạn (RBAC Policy):** `[RequireRole("admin", "staff")]`
- **Header bắt buộc:**
  - `X-User-Id: <Guid>` (UUID của Quản trị viên hoặc Nhân viên thực hiện duyệt)
  - `X-User-Roles: admin` hoặc `X-User-Roles: staff`
- **Controller:** `Catalog.Api/Controllers/Public/AdminProductReportsController.cs`
- **Application Contract:** `Catalog.Application/Services/Report/IProductReportService.cs` (`ModerateReportAsync`)

---

## 2. Đầu vào & Cấu trúc DTO (Request Body Contract)

### 2.1 Route Parameters
| Tham số | Kiểu C# | Bắt buộc | Ràng buộc | Mô tả |
| :--- | :--- | :---: | :--- | :--- |
| `reportId` | `Guid` | Có | UUIDv4 hợp lệ | Mã định danh duy nhất của đơn tố cáo cần xử lý |

### 2.2 Request Body Record DTO (`ModerateProductReportRequest`)
- **Vị trí file:** `Catalog.Application/Services/Report/ReportRequests.cs`
- **Khai báo C# Record:**
```csharp
public sealed record ModerateProductReportRequest(
    [Required(ErrorMessage = "Action is required.")]
    string Action, // BAN_PRODUCT, DISMISS, INVESTIGATE

    [Required(ErrorMessage = "Resolution note is required.")]
    [StringLength(2000, MinimumLength = 5, ErrorMessage = "Resolution note must be between 5 and 2000 characters.")]
    string ResolutionNote
);
```

- **JSON Request Body mẫu (Hành động Cấm sản phẩm):**
```json
{
  "action": "BAN_PRODUCT",
  "resolutionNote": "Đã xác minh sản phẩm vi phạm bản quyền thương hiệu, không có chứng nhận nguồn gốc xuất xứ. Cấm vĩnh viễn khỏi sàn."
}
```

- **JSON Request Body mẫu (Hành động Bác bỏ đơn):**
```json
{
  "action": "DISMISS",
  "resolutionNote": "Người bán đã cung cấp đầy đủ hóa đơn VAT và giấy chứng nhận ủy quyền chính hãng. Bác bỏ tố cáo sai sự thật."
}
```

---

## 3. Quy tắc Nghiệp vụ & Chuyển đổi Trạng thái (State Machine)

1. **Kiểm tra Đơn Báo cáo:**
   - Đơn báo cáo có `Id == reportId` phải tồn tại trong CSDL và `!IsDeleted`. Nếu không tìm thấy $\to$ Trả về `404 REPORT_NOT_FOUND`.
   - Nếu đơn đã ở trạng thái đóng (`Resolved` hoặc `Dismissed`) $\to$ Trả về `400 REPORT_ALREADY_CLOSED` với thông điệp: `"This report has already been closed and cannot be re-moderated."`.
2. **Xử lý Hành động `BAN_PRODUCT`:**
   - Cập nhật đơn hiện tại: `Status = ReportStatus.Resolved`, `ReviewedBy = adminId`, `ReviewedAt = DateTimeOffset.UtcNow`, `ResolutionNote = request.ResolutionNote`.
   - **Tác động lên Sản phẩm bị tố cáo:**
     1. Chuyển `product.Status = ProductStatus.Banned`.
     2. Đặt `product.StatusReason = null`.
     3. Tự động vô hiệu hóa toàn bộ biến thể:
        ```csharp
        foreach (var v in product.Variants.Where(v => !v.IsDeleted))
        {
            v.Status = VariantStatus.Inactive;
            v.IsActive = false;
        }
        ```
     4. **Tự động đóng tất cả các đơn tố cáo khác đang chờ xử lý của sản phẩm này:**
        ```csharp
        var otherReports = await _dbContext.ProductReports
            .Where(r => r.ProductId == product.Id 
                     && r.Id != reportId 
                     && (r.Status == ReportStatus.Pending || r.Status == ReportStatus.Investigating)
                     && !r.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var r in otherReports)
        {
            r.Status = ReportStatus.Resolved;
            r.ReviewedBy = adminId;
            r.ReviewedAt = DateTimeOffset.UtcNow;
            r.ResolutionNote = $"Product permanently banned via report {reportId}: {request.ResolutionNote}";
        }
        ```
     5. Ghi nhật ký kiểm toán vào `AuditLogs` với `Action = AuditAction.ProductBan`.
3. **Xử lý Hành động `DISMISS` (Bác bỏ tố cáo):**
   - Cập nhật đơn: `Status = ReportStatus.Dismissed`, `ReviewedBy = adminId`, `ReviewedAt = DateTimeOffset.UtcNow`, `ResolutionNote = request.ResolutionNote`.
   - Sản phẩm vẫn giữ nguyên trạng thái hiện tại (không bị ảnh hưởng).
   - Ghi nhận `AuditLog`.
4. **Xử lý Hành động `INVESTIGATE` (Tiếp nhận điều tra):**
   - Chuyển `Status = ReportStatus.Investigating`.
   - Lưu `ReviewedBy = adminId`, ghi chú điều tra nội bộ.

---

## 4. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)

- **Mã HTTP thành công:** `200 OK`
- **Kiểu trả về:** `ProductReportDto`
- **JSON Response mẫu:**
```json
{
  "id": "e1111111-2222-3333-4444-555555555555",
  "productId": "77777777-7777-7777-7777-777777777777",
  "reporterId": "99999999-9999-9999-9999-999999999999",
  "reason": "Counterfeit",
  "description": "Sản phẩm có dấu hiệu làm giả logo, không có tem chống giả.",
  "evidenceImages": [
    "https://storage.nexus.com/evidence/2026/report_img_1.jpg"
  ],
  "status": "Resolved",
  "createdAt": "2026-11-10T09:30:00Z",
  "reviewedBy": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
  "reviewedAt": "2026-11-10T10:15:00Z",
  "resolutionNote": "Đã xác minh sản phẩm vi phạm bản quyền thương hiệu, không có chứng nhận nguồn gốc xuất xứ. Cấm vĩnh viễn khỏi sàn."
}
```

---

## 5. Ma trận Mã lỗi RFC 9110 (Error Matrix)

| HTTP Status | Error Code | Detail Message (Sentence case) | Điều kiện phát sinh |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Action is required. Allowed values: BAN_PRODUCT, DISMISS, INVESTIGATE.` | Trường `action` rỗng hoặc không nằm trong danh mục cho phép |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Resolution note is required.` | Trường `resolutionNote` rỗng |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Resolution note must be between 5 and 2000 characters.` | Độ dài ghi chú giải quyết không hợp lệ |
| `400 Bad Request` | `REPORT_ALREADY_CLOSED` | `This report has already been closed and cannot be re-moderated.` | Đơn tố cáo đã ở trạng thái Resolved hoặc Dismissed |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required to access this resource.` | Không gửi kèm token hoặc thiếu `X-User-Id` |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to access or modify this resource.` | Token không có role `admin` hoặc `staff` |
| `404 Not Found` | `REPORT_NOT_FOUND` | `Product report not found in the system.` | Không tìm thấy đơn tố cáo với `reportId` tương ứng |
