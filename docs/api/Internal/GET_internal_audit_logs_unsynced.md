# CATALOG SERVICE - GET /api/v1/internal/audit-logs/unsynced

> **Feature / Use Case:** Lấy danh sách nhật ký kiểm toán chưa đồng bộ [CAT-67]  
> **Trạng thái:** ✅ VERIFIED  
> **Ngày tạo:** 2026-11-27  
> **Tác giả:** Nexus Team & AI Agent  

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/api/v1/internal/audit-logs/unsynced`
- **Quyền hạn (RBAC Policy):** `Internal` (Yêu cầu Header bảo mật `X-Internal-Service-Key` khớp với cấu hình hệ thống).
- **Controller:** `Catalog.Api/Controllers/Internal/InternalAuditLogsController.cs`
- **Application Service:** `Catalog.Application/Services/Audit/IAuditSyncSourceService.cs`

---

## 2. Đầu vào & Tham số (Parameters)
### 2.1 Query Parameters
| Tham số | Vị trí | Kiểu C# | Mặc định | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :---: | :--- | :--- |
| `limit` | Query | `int` | `200` | Không | `1` $\le$ `limit` $\le$ `500` | Số lượng bản ghi tối đa lấy trong một lượt kéo |

### 2.2 Headers
| Header | Kiểu | Bắt buộc | Mô tả |
| :--- | :--- | :---: | :--- |
| `X-Internal-Service-Key` | `string` | Có | Khóa bảo mật dịch vụ nội bộ để xác thực quyền truy cập |

---

## 3. Cấu trúc Phản hồi (Response Schema)

### 3.1 DTO Phản hồi (`AuditSyncPage`)
* **HTTP Status:** `200 OK`
* **JSON Response Mẫu:**
```json
{
  "items": [
    {
      "id": "e9c1d9b3-1f2a-4a6c-8c1e-3f5a7b9c1d3e",
      "serviceName": "CatalogService",
      "traceId": "00-7952b1d50e4b335d3fb46cd130ddaabb-015c8cdb0ff8db32-01",
      "userId": "97b3ae37-9ebc-4d15-b812-35cf6971ea76",
      "userName": "seller_demo",
      "action": "ProductUpdated",
      "entityName": "Product",
      "entityId": "d25074e2-9ca8-41fa-a5e0-31bbbfd3c0eb",
      "oldValues": "{\"Status\": \"Draft\"}",
      "newValues": "{\"Status\": \"PendingApproval\"}",
      "changedColumns": "Status",
      "ipAddress": "127.0.0.1",
      "userAgent": "Mozilla/5.0",
      "createdAt": "2026-11-27T08:00:00+00:00"
    }
  ],
  "unsyncedCount": 1
}
```

---

## 4. Ma trận Mã Lỗi (Error Matrix RFC 9110)
| HTTP Status | Error Code (`errorCode`) | Error Message (`detail` / `message`) | Nguyên nhân |
| :--- | :--- | :--- | :--- |
| `403 Forbidden` | `FORBIDDEN` | Access denied to internal service resource. | Header `X-Internal-Service-Key` không đúng hoặc bị thiếu. |
| `500 InternalServerError` | `INTERNAL_CONFIGURATION_ERROR` | Internal service authentication is not configured. | Service chưa cấu hình `INTERNAL_SERVICE_SECRET`. |
