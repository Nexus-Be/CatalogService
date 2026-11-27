# CATALOG SERVICE - POST /api/v1/internal/audit-logs/mark-synced

> **Feature / Use Case:** Đánh dấu các bản ghi nhật ký kiểm toán đã đồng bộ thành công [CAT-68]  
> **Trạng thái:** ✅ VERIFIED  
> **Ngày tạo:** 2026-11-27  
> **Tác giả:** Nexus Team & AI Agent  

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `POST`
- **Route Endpoint:** `/api/v1/internal/audit-logs/mark-synced`
- **Quyền hạn (RBAC Policy):** `Internal` (Yêu cầu Header bảo mật `X-Internal-Service-Key` khớp với cấu hình hệ thống).
- **Controller:** `Catalog.Api/Controllers/Internal/InternalAuditLogsController.cs`
- **Application Service:** `Catalog.Application/Services/Audit/IAuditSyncSourceService.cs`

---

## 2. Đầu vào & Tham số (Parameters)
### 2.1 Headers
| Header | Kiểu | Bắt buộc | Mô tả |
| :--- | :--- | :---: | :--- |
| `X-Internal-Service-Key` | `string` | Có | Khóa bảo mật dịch vụ nội bộ để xác thực quyền truy cập |

### 2.2 Request Body (`MarkAuditSyncedRequest`)
| Trường | Kiểu C# | Bắt buộc | Giới hạn | Ghi chú |
| :--- | :--- | :---: | :--- | :--- |
| `ids` | `IReadOnlyList<Guid>` | Có | Tối đa 500 GUID | Danh sách mã ID bản ghi nhật ký kiểm toán cần xác nhận |

* **JSON Request Mẫu:**
```json
{
  "ids": [
    "e9c1d9b3-1f2a-4a6c-8c1e-3f5a7b9c1d3e"
  ]
}
```

---

## 3. Cấu trúc Phản hồi (Response Schema)

### 3.1 DTO Phản hồi (`MarkAuditSyncedResponse`)
* **HTTP Status:** `200 OK`
* **JSON Response Mẫu:**
```json
{
  "updated": 1
}
```

---

## 4. Ma trận Mã Lỗi (Error Matrix RFC 9110)
| HTTP Status | Error Code (`errorCode`) | Error Message (`detail` / `message`) | Nguyên nhân |
| :--- | :--- | :--- | :--- |
| `403 Forbidden` | `FORBIDDEN` | Access denied to internal service resource. | Header `X-Internal-Service-Key` không đúng hoặc bị thiếu. |
| `500 InternalServerError` | `INTERNAL_CONFIGURATION_ERROR` | Internal service authentication is not configured. | Service chưa cấu hình `INTERNAL_SERVICE_SECRET`. |
