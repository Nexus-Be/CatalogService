# CATALOG SERVICE - POST /api/v1/products/{id}/reports

> **Feature / Use Case:** Khách hàng (Buyer) hoặc Đối tác gửi đơn tố cáo sản phẩm vi phạm chính sách kèm upload trực tiếp ảnh bằng chứng thật [CAT-57]  
> **Trạng thái:** ✅ VERIFIED  
> **Ngày cập nhật:** 2026-11-12  
> **Tác giả:** Nexus Team & AI Agent  

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `POST`
- **Route Endpoint:** `/api/v1/products/{id}/reports`
- **Content-Type:** `multipart/form-data` (Hỗ trợ upload trực tiếp file ảnh thật từ máy khách)
- **Quyền hạn (RBAC Policy):** `[RequireRole("buyer", "seller")]` (Bắt buộc người dùng đã đăng nhập)
- **Header bắt buộc:**
  - `X-User-Id: <Guid>` (UUID của người dùng gửi đơn tố cáo)
  - `X-User-Roles: buyer` hoặc `seller`
- **Controller:** `Catalog.Api/Controllers/Public/ProductReportsController.cs`
- **Application Contract:** `Catalog.Application/Services/Report/IProductReportService.cs` (`SubmitProductReportAsync`)
- **Tương tác Lưu trữ Ảnh (Cloudinary Storage):** Backend tự động kiểm tra định dạng và dung lượng ảnh, sau đó tải các file ảnh trong `evidenceFiles` lên Cloudinary vào thư mục `reports/evidence` và lưu các URL kết quả vào database.

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)

### 2.1 Route Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `id` | Route | `Guid` | Có | UUIDv4 format | Khóa chính của sản phẩm bị tố cáo |

### 2.2 Form Data Parameters (`multipart/form-data`)
| Trường Form | Kiểu | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :---: | :--- | :--- |
| `reason` | `string` | Có | `Counterfeit`, `ProhibitedItem`, `IncorrectDescription`, `OffensiveContent`, `Other` | Lý do tố cáo theo danh mục chuẩn |
| `description` | `string` | Có | Độ dài từ 10 đến 2000 ký tự | Mô tả chi tiết hành vi vi phạm |
| `evidenceFiles` | `List<IFormFile>` | **Có (Bắt buộc $\ge$ 1 ảnh)** | Từ 1 đến 5 file; $\le$ 5MB/file; định dạng `.jpg`, `.jpeg`, `.png`, `.webp` | Bắt buộc đính kèm tối thiểu 1 ảnh bằng chứng thật để tránh tố cáo ảo |

- **Ví dụ gọi bằng cURL (gửi file ảnh thật):**
```bash
curl -X POST "http://localhost:8002/api/v1/products/f3f47581-6036-4fa1-b9fa-803770308c71/reports" \
  -H "x-user-id: dddddddd-4444-4444-4444-444444444444" \
  -H "x-user-roles: buyer" \
  -F "reason=Counterfeit" \
  -F "description=Sản phẩm nhận được có dấu hiệu làm giả logo và chất lượng rất kém." \
  -F "evidenceFiles=@/path/to/evidence_tag.png" \
  -F "evidenceFiles=@/path/to/evidence_fabric.png"
```

---

## 3. Quy tắc Nghiệp vụ & Rào chắn (Business Rules & Anti-Spam)

1. **Kiểm tra Sản phẩm:**
   - Sản phẩm có `Id == id` phải tồn tại trong CSDL và chưa bị xóa mềm (`!IsDeleted`). Nếu không tồn tại $\to$ Trả về `404 PRODUCT_NOT_FOUND`.
   - Nếu sản phẩm đã ở trạng thái cấm (`Status == ProductStatus.Banned`) $\to$ Trả về `400 PRODUCT_ALREADY_BANNED` ("This product has already been permanently banned.").
2. **Quy tắc Chống Spam (Anti-Spam & Deduplication):**
   - Một người dùng (`ReporterId`) **không được phép gửi nhiều báo cáo liên tiếp** cho cùng một sản phẩm khi đơn báo cáo trước đó vẫn đang trong quá trình xử lý (`Pending` hoặc `Investigating`):
     ```csharp
     var hasActiveReport = await _dbContext.ProductReports
         .AnyAsync(r => r.ProductId == id 
                     && r.ReporterId == reporterId 
                     && (r.Status == ReportStatus.Pending || r.Status == ReportStatus.Investigating)
                     && !r.IsDeleted, cancellationToken);
     ```
   - Nếu vi phạm $\to$ Trả về `409 Conflict` với mã lỗi `ALREADY_REPORTED` ("You have already submitted a report for this product that is currently under review.").
3. **Quy định Bắt buộc về Ảnh Bằng chứng (Mandatory Evidence Images):**
   - Đơn tố cáo **bắt buộc phải có ít nhất 1 ảnh bằng chứng thật** (`evidenceFiles`). Nếu không có ảnh hoặc danh sách rỗng $\to$ Trả về `400 Bad Request` với mã lỗi `VAL_INVALID_INPUT` ("At least one evidence image is required to submit a report.").
   - Backend duyệt qua từng file trong `evidenceFiles` (tối đa 5 file, mỗi file $\le$ 5MB, hỗ trợ `.jpg`, `.jpeg`, `.png`, `.webp`).
   - Gọi `_imageStorageService.UploadAsync(stream, fileName, folder: "reports/evidence", ct)` tải lên Cloudinary.
   - Thu thập danh sách các link URL an toàn và lưu vào CSDL cột `EvidenceImages` dưới dạng chuỗi `jsonb`.
4. **Lưu trữ & Trạng thái Ban đầu:**
   - Tạo bản ghi mới trong bảng `ProductReports` với `Status = ReportStatus.Pending`.
   - Ghi nhận `AuditLog` với `Action = AuditAction.ProductReport`.
5. **Tự động gửi thông báo In-App cho Người gửi tố cáo (Buyer):**
   - Khi đơn nộp thành công, hệ thống tự động gọi NotificationService (`POST api/v1/internal/notifications/in-app`) để gửi thông báo xác nhận cho Buyer:
     - **NotificationType:** `REPORT_SUBMITTED`
     - **Tiêu đề:** `"Đã tiếp nhận đơn tố cáo sản phẩm"`
     - **Nội dung:** `"Chúng tôi đã tiếp nhận đơn tố cáo của bạn đối với sản phẩm '{product.Name}'. Đội ngũ Quản trị viên sẽ tiến hành xác minh trong 24-48 giờ."`

---

## 4. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)

- **Mã HTTP thành công:** `201 Created`
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

- **JSON Response Mẫu:**
```json
{
  "id": "d40de46c-7194-415a-a670-c7a220c85d6a",
  "productId": "f3f47581-6036-4fa1-b9fa-803770308c71",
  "reporterId": "dddddddd-4444-4444-4444-444444444444",
  "reason": "Counterfeit",
  "description": "Sản phẩm nhận được có dấu hiệu làm giả logo và chất lượng rất kém.",
  "evidenceImages": [
    "https://res.cloudinary.com/u6yxkkop/image/upload/v1794457058/nexus_catalog/reports/evidence/oydlbxzwputlseoy8jz5.png",
    "https://res.cloudinary.com/u6yxkkop/image/upload/v1794457059/nexus_catalog/reports/evidence/yaw7uduqufixc9ipuig8.png"
  ],
  "status": "Pending",
  "createdAt": "2026-11-12T04:17:39Z",
  "reviewedBy": null,
  "reviewedAt": null,
  "resolutionNote": null
}
```

---

## 5. Ma trận Lỗi (RFC 9110 Error Matrix)

| HTTP Status | Error Code | Điều kiện kích hoạt | Error Message chuẩn |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | Không đính kèm ảnh bằng chứng hoặc danh sách file rỗng | `"At least one evidence image is required to submit a report."` |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `reason` rỗng hoặc không thuộc enum; `description` < 10 hoặc > 2000 ký tự | `"Report reason is required."` / `"Description must be between 10 and 2000 characters."` |
| `400 Bad Request` | `VAL_INVALID_INPUT` | Quá 5 file ảnh hoặc định dạng không phải `.jpg, .jpeg, .png, .webp` | `"Maximum 5 evidence images are allowed."` / `"File '{fileName}' has unsupported extension '{ext}'. Only .jpg, .jpeg, .png, .webp are allowed."` |
| `400 Bad Request` | `VAL_INVALID_INPUT` | File ảnh vượt quá dung lượng 5MB | `"File '{fileName}' exceeds 5 MB limit."` |
| `400 Bad Request` | `PRODUCT_ALREADY_BANNED` | Sản phẩm đã bị cấm vĩnh viễn trên hệ thống | `"This product has already been permanently banned."` |
| `401 Unauthorized` | `UNAUTHORIZED` | Không có Header định danh hoặc token | `"Authentication is required to perform this action."` |
| `404 Not Found` | `PRODUCT_NOT_FOUND` | Không tìm thấy sản phẩm cần tố cáo | `"Product not found in the system."` |
| `409 Conflict` | `ALREADY_REPORTED` | Người dùng đã có đơn tố cáo đang chờ xử lý cho sản phẩm này | `"You have already submitted a report for this product that is currently under review."` |
