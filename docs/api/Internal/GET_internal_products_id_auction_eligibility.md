# CATALOG SERVICE - GET /api/v1/internal/products/{id}/auction-eligibility

> **Feature / Use Case:** Tiền kiểm tra điều kiện sản phẩm trước khi tạo hoặc mở phiên đấu giá [CAT-52]
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-09
> **Tác giả:** Nexus Team & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/api/v1/internal/products/{id}/auction-eligibility`
- **Quyền hạn (RBAC Policy):** `Internal` (Gọi trực tiếp liên service qua Docker network giữa `AuctionService` và `CatalogService`, không yêu cầu Bearer JWT).
- **Controller:** `Catalog.Api/Controllers/Internal/InternalProductsController.cs`
- **Application Service:** `Catalog.Application/Services/Product/IProductService.cs`

---

## 2. Đầu vào & Tham số (Parameters)
### 2.1 Route Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `id` | Route | `Guid` | Có | UUIDv4 format | Mã ID của sản phẩm đấu giá cần kiểm tra |

---

## 3. Cấu trúc Phản hồi (Response Schema)

### 3.1 DTO Phản hồi (`AuctionEligibilityResponse`)
```csharp
public sealed record AuctionEligibilityResponse(
    Guid ProductId,
    bool IsEligible,
    string? Reason,
    string Status,
    string? SkuCode,
    int? WeightGrams
);
```

### 3.2 JSON Response Mẫu (200 OK)
#### Trường hợp đủ điều kiện (`isEligible: true`):
```json
{
  "productId": "d25074e2-9ca8-41fa-a5e0-31bbbfd3c0eb",
  "isEligible": true,
  "reason": null,
  "status": "Active",
  "skuCode": "AUC-058001D9",
  "weightGrams": 350
}
```

#### Trường hợp không đủ điều kiện (ví dụ: đang trong phiên đấu giá khác):
```json
{
  "productId": "d25074e2-9ca8-41fa-a5e0-31bbbfd3c0eb",
  "isEligible": false,
  "reason": "Product is already locked in an ongoing auction.",
  "status": "InAuction",
  "skuCode": "AUC-058001D9",
  "weightGrams": 350
}
```

---

## 4. Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `ProductStatus.Draft` | `0` | `"Draft"` | Bản nháp |
| `ProductStatus.Active` | `1` | `"Active"` | Đang hoạt động, đủ điều kiện đưa vào phiên |
| `ProductStatus.Inactive` | `2` | `"Inactive"` | Tạm ngưng bán |
| `ProductStatus.Auctioned` | `3` | `"Auctioned"` | Đã bán xong trong phiên trước đó |
| `ProductStatus.InAuction` | `4` | `"InAuction"` | Đang trong một phiên đấu giá đang mở |

